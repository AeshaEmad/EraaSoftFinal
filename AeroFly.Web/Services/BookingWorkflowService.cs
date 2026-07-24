using System.Data;
using AeroFly.Web.Data;
using AeroFly.Web.Models;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace AeroFly.Web.Services;

public record BookingOperationResult(bool Success, bool Changed, string Message, Booking? Booking = null);

public interface IBookingWorkflowService
{
    Task<BookingOperationResult> ConfirmPaidBookingAsync(
        int bookingId,
        string paymentIntentId,
        decimal paidAmount,
        CancellationToken cancellationToken = default);

    Task<BookingOperationResult> CancelAndRefundAsync(
        int bookingId,
        CancellationToken cancellationToken = default);

    Task<BookingOperationResult> CompleteRefundAsync(
        string refundId,
        string paymentIntentId,
        CancellationToken cancellationToken = default);

    Task ReleaseExpiredHoldAsync(int bookingId, CancellationToken cancellationToken = default);
}

public class BookingWorkflowService : IBookingWorkflowService
{
    private readonly ApplicationDbContext _db;
    private readonly IStripeService _stripe;

    public BookingWorkflowService(ApplicationDbContext db, IStripeService stripe)
    {
        _db = db;
        _stripe = stripe;
    }

    public async Task<BookingOperationResult> ConfirmPaidBookingAsync(
        int bookingId,
        string paymentIntentId,
        decimal paidAmount,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var booking = await LoadBookingAsync(bookingId, cancellationToken);
        if (booking == null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(false, false, "Booking not found.");
        }

        if (booking.Status is "Confirmed" or "Completed")
        {
            var samePayment = booking.Payment?.TransactionRef == paymentIntentId;
            await transaction.CommitAsync(cancellationToken);
            return new(samePayment, false,
                samePayment ? "Booking is already confirmed." : "Booking was confirmed by another payment.",
                booking);
        }

        if (booking.Status != "Pending" || booking.Payment == null || paidAmount != booking.TotalPrice)
        {
            await transaction.RollbackAsync(cancellationToken);
            await RefundFailedConfirmationAsync(booking, paymentIntentId, cancellationToken);
            return new(false, false, "Payment was refunded because the booking could not be confirmed.", booking);
        }

        if (!booking.SeatsReserved)
        {
            var reserved = await ReserveSeatsAsync(booking, cancellationToken);
            if (!reserved)
            {
                await transaction.RollbackAsync(cancellationToken);
                await RefundFailedConfirmationAsync(booking, paymentIntentId, cancellationToken);
                return new(false, false, "Payment was refunded because the selected seats are no longer available.", booking);
            }
        }

        booking.Status = "Confirmed";
        booking.SeatHoldExpiresAt = null;
        booking.Payment.Amount = booking.TotalPrice;
        booking.Payment.PayStatus = "Completed";
        booking.Payment.PayMethod = booking.Payment.PayMethod ?? "CreditCard";
        booking.Payment.PayDate = DateTime.UtcNow;
        booking.Payment.TransactionRef = paymentIntentId;

        await CreateTicketsAsync(booking, cancellationToken);
        await AddEarnedPointsAsync(booking, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new(true, true, "Payment succeeded and the booking was confirmed.", booking);
    }

    public async Task<BookingOperationResult> CancelAndRefundAsync(
        int bookingId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.BookingId == bookingId, cancellationToken);

        if (payment == null)
        {
            return new(false, false, "Payment not found.");
        }

        if (payment.PayStatus == "Refunded")
        {
            return new(true, false, "This payment is already refunded.");
        }

        if (payment.PayStatus == "Completed")
        {
            try
            {
                payment = await _db.Payments.FirstAsync(p => p.PayId == payment.PayId, cancellationToken);
                payment.RefundStatus = "Pending";
                payment.RefundFailureReason = null;
                await _db.SaveChangesAsync(cancellationToken);

                var refund = await _stripe.CreateRefundAsync(payment.TransactionRef, bookingId);
                payment.StripeRefundId = refund.Id;
                payment.RefundStatus = refund.Status;
                await _db.SaveChangesAsync(cancellationToken);

                if (!string.Equals(refund.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    return new(true, true, $"Stripe refund is {refund.Status}; cancellation will finish after Stripe confirms it.");
                }

                return await CompleteRefundAsync(refund.Id, payment.TransactionRef, cancellationToken);
            }
            catch (StripeException ex)
            {
                payment = await _db.Payments.FirstAsync(p => p.BookingId == bookingId, cancellationToken);
                payment.RefundStatus = "Failed";
                payment.RefundFailureReason = ex.StripeError?.Message ?? ex.Message;
                await _db.SaveChangesAsync(cancellationToken);
                return new(false, true, "Stripe rejected the refund. The booking was not marked as refunded.");
            }
        }

        if (payment.TransactionRef.StartsWith("pi_", StringComparison.Ordinal))
        {
            try
            {
                await _stripe.CancelPaymentIntentAsync(payment.TransactionRef);
            }
            catch (StripeException)
            {
                return new(false, false, "Could not cancel the Stripe payment session. Please retry.");
            }
        }

        return await FinalizeCancellationAsync(bookingId, null, cancellationToken);
    }

    public async Task<BookingOperationResult> CompleteRefundAsync(
        string refundId,
        string paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        var bookingId = await _db.Payments
            .Where(p => p.TransactionRef == paymentIntentId)
            .Select(p => (int?)p.BookingId)
            .FirstOrDefaultAsync(cancellationToken);

        return bookingId.HasValue
            ? await FinalizeCancellationAsync(bookingId.Value, refundId, cancellationToken)
            : new(false, false, "Refund does not match a local payment.");
    }

    public async Task ReleaseExpiredHoldAsync(
        int bookingId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var booking = await LoadBookingAsync(bookingId, cancellationToken);
        string? expiredPaymentIntent = null;

        if (booking is { Status: "Pending", SeatsReserved: true } &&
            booking.SeatHoldExpiresAt <= DateTime.UtcNow &&
            booking.Payment?.PayStatus != "Completed")
        {
            ReleaseSeats(booking);
            booking.Status = "Cancelled";
            booking.Payment!.PayStatus = "Failed";
            expiredPaymentIntent = booking.Payment.TransactionRef;
            await _db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        if (expiredPaymentIntent?.StartsWith("pi_", StringComparison.Ordinal) == true)
        {
            try
            {
                await _stripe.CancelPaymentIntentAsync(expiredPaymentIntent);
            }
            catch (StripeException)
            {
                // A concurrent payment success is handled by the webhook and auto-refund path.
            }
        }
    }

    private async Task<BookingOperationResult> FinalizeCancellationAsync(
        int bookingId,
        string? refundId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var booking = await LoadBookingAsync(bookingId, cancellationToken);

        if (booking == null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(false, false, "Booking not found.");
        }

        if (booking.SeatsReserved)
        {
            ReleaseSeats(booking);
        }

        await ReversePointsAsync(booking, cancellationToken);
        booking.Status = "Cancelled";
        booking.SeatHoldExpiresAt = null;

        if (booking.Payment != null)
        {
            if (refundId != null)
            {
                booking.Payment.StripeRefundId = refundId;
                booking.Payment.RefundStatus = "Succeeded";
                booking.Payment.PayStatus = "Refunded";
                booking.Payment.RefundedAt = DateTime.UtcNow;
            }
            else if (booking.Payment.PayStatus == "Pending")
            {
                booking.Payment.PayStatus = "Failed";
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, true, "Booking cancelled successfully.", booking);
    }

    private async Task RefundFailedConfirmationAsync(
        Booking booking,
        string paymentIntentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var refund = await _stripe.CreateRefundAsync(paymentIntentId, booking.BookingId);
            var payment = await _db.Payments.FirstOrDefaultAsync(
                p => p.BookingId == booking.BookingId,
                cancellationToken);
            if (payment != null)
            {
                payment.TransactionRef = paymentIntentId;
                payment.StripeRefundId = refund.Id;
                payment.RefundStatus = refund.Status;
                if (refund.Status == "succeeded")
                {
                    payment.PayStatus = "Refunded";
                    payment.RefundedAt = DateTime.UtcNow;
                }
                await _db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (StripeException ex)
        {
            var payment = await _db.Payments.FirstOrDefaultAsync(
                p => p.BookingId == booking.BookingId,
                cancellationToken);
            if (payment != null)
            {
                payment.RefundStatus = "Failed";
                payment.RefundFailureReason = ex.StripeError?.Message ?? ex.Message;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private async Task<Booking?> LoadBookingAsync(int bookingId, CancellationToken cancellationToken) =>
        await _db.Bookings
            .Include(b => b.Payment)
            .Include(b => b.User).ThenInclude(u => u.RewardAccount)
            .Include(b => b.Flight)
            .Include(b => b.Passengers).ThenInclude(p => p.FlightSeatClass)
            .Include(b => b.Tickets)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);

    private async Task<bool> ReserveSeatsAsync(Booking booking, CancellationToken cancellationToken)
    {
        var passengerCount = booking.Passengers.Count;
        if (passengerCount == 0)
        {
            return false;
        }

        var passengerGroups = booking.Passengers.GroupBy(p => p.ClassId).ToList();
        foreach (var group in passengerGroups)
        {
            var seatClass = await _db.FlightSeatClasses.FirstOrDefaultAsync(
                f => f.FlightId == booking.FlightId && f.ClassId == group.Key,
                cancellationToken);
            if (seatClass == null || seatClass.AvailableSeats < group.Count())
            {
                return false;
            }
        }

        foreach (var group in passengerGroups)
        {
            var seatClass = await _db.FlightSeatClasses.FirstOrDefaultAsync(
                f => f.FlightId == booking.FlightId && f.ClassId == group.Key,
                cancellationToken);
            seatClass!.AvailableSeats -= group.Count();
        }

        booking.Flight.AvailableSeats -= passengerCount;
        booking.SeatsReserved = true;
        return true;
    }

    private static void ReleaseSeats(Booking booking)
    {
        foreach (var group in booking.Passengers.GroupBy(p => p.ClassId))
        {
            var seatClass = booking.Passengers
                .First(p => p.ClassId == group.Key)
                .FlightSeatClass;
            seatClass.AvailableSeats += group.Count();
        }

        booking.Flight.AvailableSeats += booking.Passengers.Count;
        booking.SeatsReserved = false;
    }

    private async Task AddEarnedPointsAsync(Booking booking, CancellationToken cancellationToken)
    {
        if (await _db.PointsTransactions.AnyAsync(
                t => t.BookingId == booking.BookingId && t.Type == "Earned",
                cancellationToken))
        {
            return;
        }

        var account = booking.User.RewardAccount;
        if (account == null)
        {
            account = new RewardAccount { UserId = booking.UserId };
            _db.RewardAccounts.Add(account);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var points = (int)Math.Floor(booking.TotalPrice);
        account.PointsBalance += points;
        _db.PointsTransactions.Add(new PointsTransaction
        {
            AccountId = account.AccountId,
            BookingId = booking.BookingId,
            Points = points,
            Type = "Earned",
            Date = DateTime.UtcNow,
            Description = $"Earned from {booking.PNR}"
        });
    }

    private async Task ReversePointsAsync(Booking booking, CancellationToken cancellationToken)
    {
        var account = booking.User.RewardAccount;
        if (account == null)
        {
            return;
        }

        var earned = await _db.PointsTransactions.FirstOrDefaultAsync(
            t => t.BookingId == booking.BookingId && t.Type == "Earned",
            cancellationToken);
        var alreadyReversed = await _db.PointsTransactions.AnyAsync(
            t => t.BookingId == booking.BookingId && t.Type == "Reversed",
            cancellationToken);
        if (earned != null && !alreadyReversed)
        {
            account.PointsBalance -= earned.Points;
            _db.PointsTransactions.Add(new PointsTransaction
            {
                AccountId = account.AccountId,
                BookingId = booking.BookingId,
                Points = -earned.Points,
                Type = "Reversed",
                Date = DateTime.UtcNow,
                Description = $"Reversed points from {booking.PNR}"
            });
        }

        var redeemedRefunded = await _db.PointsTransactions.AnyAsync(
            t => t.BookingId == booking.BookingId && t.Type == "Refunded",
            cancellationToken);
        if (booking.PointsUsed > 0 && !redeemedRefunded)
        {
            account.PointsBalance += booking.PointsUsed;
            _db.PointsTransactions.Add(new PointsTransaction
            {
                AccountId = account.AccountId,
                BookingId = booking.BookingId,
                Points = booking.PointsUsed,
                Type = "Refunded",
                Date = DateTime.UtcNow,
                Description = $"Returned redeemed points from {booking.PNR}"
            });
        }
    }

    private async Task CreateTicketsAsync(Booking booking, CancellationToken cancellationToken)
    {
        var usedSeats = await _db.Tickets
            .Where(t => t.FlightId == booking.FlightId && t.Booking.Status != "Cancelled")
            .Select(t => t.SeatNum)
            .ToListAsync(cancellationToken);

        var nextSeat = 1;
        foreach (var passenger in booking.Passengers.Where(
                     p => booking.Tickets.All(t => t.PassengerId != p.PassengerId)))
        {
            string seat;
            do
            {
                seat = $"{nextSeat++:D3}";
            } while (usedSeats.Contains(seat));

            usedSeats.Add(seat);
            _db.Tickets.Add(new Ticket
            {
                BookingId = booking.BookingId,
                FlightId = booking.FlightId,
                PassengerId = passenger.PassengerId,
                IssueDate = DateTime.UtcNow,
                SeatNum = seat,
                QrCode = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            });
        }
    }
}
