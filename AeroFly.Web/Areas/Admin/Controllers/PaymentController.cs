using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AeroFly.Web.Data;
using AeroFly.Web.ViewModels;
using AeroFly.Web.Models;
using System.Data;
using AeroFly.Web.Services;

namespace AeroFly.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOperations")]
public class PaymentController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IBookingWorkflowService _bookingWorkflow;

    public PaymentController(
        ApplicationDbContext context,
        IBookingWorkflowService bookingWorkflow)
    {
        _context = context;
        _bookingWorkflow = bookingWorkflow;
    }

     
    // 1. INDEX - List all Payments with filters
     
    public async Task<IActionResult> Index(
        string? searchTerm,
        string? status,
        string? paymentMethod,
        DateTime? fromDate,
        DateTime? toDate)
    {
        ViewData["Title"] = "Payments Management";

        var query = _context.Payments
            .Include(p => p.Booking)
                .ThenInclude(b => b.User)
            .Include(p => p.Booking)
                .ThenInclude(b => b.Flight)
                    .ThenInclude(f => f.DepartureAirport)
            .Include(p => p.Booking)
                .ThenInclude(b => b.Flight)
                    .ThenInclude(f => f.ArrivalAirport)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(searchTerm))
        {
            searchTerm = searchTerm.ToLower();
            query = query.Where(p =>
                p.TransactionRef.ToLower().Contains(searchTerm) ||
                p.Booking.User.FName.ToLower().Contains(searchTerm) ||
                p.Booking.User.LName.ToLower().Contains(searchTerm) ||
                p.Booking.User.Email.ToLower().Contains(searchTerm) ||
                p.Booking.Flight.FlightNum.ToLower().Contains(searchTerm) ||
                p.Booking.PNR.ToLower().Contains(searchTerm));
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(p => p.PayStatus == status);
        }

        if (!string.IsNullOrEmpty(paymentMethod))
        {
            query = query.Where(p => p.PayMethod == paymentMethod);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(p => p.PayDate.Date >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(p => p.PayDate.Date <= toDate.Value.Date);
        }

        var payments = await query
            .OrderByDescending(p => p.PayDate)
            .Select(p => new PaymentListVM
            {
                PayId = p.PayId,
                TransactionRef = p.TransactionRef,
                BookingId = p.BookingId,
                PassengerName = p.Booking.User.FName + " " + p.Booking.User.LName,
                Email = p.Booking.User.Email,
                FlightNumber = p.Booking.Flight.FlightNum,
                Amount = p.Amount,
                PayMethod = p.PayMethod,
                PayStatus = p.PayStatus,
                PayDate = p.PayDate
            })
            .ToListAsync();

        // Get filter data
        ViewBag.Statuses = new List<string> { "Pending", "Completed", "Failed", "Refunded" };
        ViewBag.PaymentMethods = new List<string> { "CreditCard", "DebitCard", "PayPal", "RewardPoints" };

        return View(payments);
    }

     
    // 2. DETAILS - View payment details
     
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        ViewData["Title"] = "Payment Details";

        var payment = await _context.Payments
            .Include(p => p.Booking)
                .ThenInclude(b => b.User)
            .Include(p => p.Booking)
                .ThenInclude(b => b.Flight)
                    .ThenInclude(f => f.DepartureAirport)
            .Include(p => p.Booking)
                .ThenInclude(b => b.Flight)
                    .ThenInclude(f => f.ArrivalAirport)
            .Include(p => p.Booking)
                .ThenInclude(b => b.Passengers)
            .FirstOrDefaultAsync(p => p.PayId == id);

        if (payment == null)
        {
            TempData["Error"] = "Payment not found!";
            return RedirectToAction(nameof(Index));
        }

        var model = new PaymentDetailsVM
        {
            PayId = payment.PayId,
            TransactionRef = payment.TransactionRef,
            BookingId = payment.BookingId,
            PassengerName = $"{payment.Booking.User.FName} {payment.Booking.User.LName}",
            Email = payment.Booking.User.Email,
            FlightNumber = payment.Booking.Flight.FlightNum,
            DepartureIata = payment.Booking.Flight.DepartureAirport.IataCode,
            ArrivalIata = payment.Booking.Flight.ArrivalAirport.IataCode,
            DepartureTime = payment.Booking.Flight.DepartureTime,
            Amount = payment.Amount,
            PayMethod = payment.PayMethod,
            PayStatus = payment.PayStatus,
            PayDate = payment.PayDate,
            BookingStatus = payment.Booking.Status,
            PassengerCount = payment.Booking.Passengers.Count,
            PassengerNames = payment.Booking.Passengers.Select(p => p.FullName).ToList()
        };

        return View(model);
    }

     
    // 3. REFUND - Refund payment
     
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refund(int id)
    {
        var bookingId = await _context.Payments
            .Where(p => p.PayId == id)
            .Select(p => (int?)p.BookingId)
            .FirstOrDefaultAsync();
        if (!bookingId.HasValue)
        {
            TempData["Error"] = "Payment not found!";
            return RedirectToAction(nameof(Index));
        }

        var result = await _bookingWorkflow.CancelAndRefundAsync(bookingId.Value);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));

#pragma warning disable CS0162
        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        var payment = await _context.Payments
            .Include(p => p.Booking)
                .ThenInclude(b => b.Flight)
            .Include(p => p.Booking)
                .ThenInclude(b => b.Passengers)
            .Include(p => p.Booking)
                .ThenInclude(b => b.Tickets)
            .Include(p => p.Booking)
                .ThenInclude(b => b.User)
                    .ThenInclude(u => u.RewardAccount)
            .FirstOrDefaultAsync(p => p.PayId == id);

        if (payment == null)
        {
            TempData["Error"] = "Payment not found!";
            return RedirectToAction(nameof(Index));
        }

        if (payment.PayStatus != "Completed")
        {
            TempData["Warning"] = "Only completed payments can be refunded!";
            return RedirectToAction(nameof(Index));
        }

        if (payment.Booking.Status == "Cancelled")
        {
            TempData["Warning"] = "This payment is already refunded!";
            return RedirectToAction(nameof(Index));
        }

        var booking = payment.Booking;

        if (booking.Status == "Confirmed")
        {
            booking.Flight.AvailableSeats += booking.Passengers.Count;

            foreach (var passengerGroup in booking.Passengers.GroupBy(p => p.ClassId))
            {
                var flightSeatClass = await _context.FlightSeatClasses
                    .FirstOrDefaultAsync(fsc =>
                        fsc.FlightId == booking.FlightId && fsc.ClassId == passengerGroup.Key);

                if (flightSeatClass != null)
                {
                    flightSeatClass.AvailableSeats += passengerGroup.Count();
                }
            }
        }

        if (booking.PointsUsed > 0 && booking.User.RewardAccount != null)
        {
            booking.User.RewardAccount.PointsBalance += booking.PointsUsed;
            _context.PointsTransactions.Add(new PointsTransaction
            {
                AccountId = booking.User.RewardAccount.AccountId,
                Points = booking.PointsUsed,
                Type = "Refunded",
                Date = DateTime.Now,
                Description = $"Refunded {booking.PointsUsed} redeemed points after refunding {booking.PNR}"
            });
        }

        if (booking.Tickets.Any())
        {
            _context.Tickets.RemoveRange(booking.Tickets);
        }

        // Update payment
        payment.PayStatus = "Refunded";
        payment.PayDate = DateTime.Now;

        // Update booking
        payment.Booking.Status = "Cancelled";

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["Success"] = $"Payment {payment.TransactionRef} has been refunded successfully!";
        return RedirectToAction(nameof(Index));
#pragma warning restore CS0162
    }

     
    // 4. DELETE - GET (Confirmation)
     
    [HttpGet]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> Delete(int id)
    {
        ViewData["Title"] = "Delete Payment";

        var payment = await _context.Payments
            .Include(p => p.Booking)
                .ThenInclude(b => b.User)
            .Include(p => p.Booking)
                .ThenInclude(b => b.Flight)
            .FirstOrDefaultAsync(p => p.PayId == id);

        if (payment == null)
        {
            TempData["Error"] = "Payment not found!";
            return RedirectToAction(nameof(Index));
        }

        var model = new PaymentListVM
        {
            PayId = payment.PayId,
            TransactionRef = payment.TransactionRef,
            BookingId = payment.BookingId,
            PassengerName = $"{payment.Booking.User.FName} {payment.Booking.User.LName}",
            Email = payment.Booking.User.Email,
            FlightNumber = payment.Booking.Flight.FlightNum,
            Amount = payment.Amount,
            PayMethod = payment.PayMethod,
            PayStatus = payment.PayStatus,
            PayDate = payment.PayDate
        };

        return View(model);
    }

     
    // 5. DELETE - POST (Confirm)
     
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.PayId == id);

        if (payment == null)
        {
            TempData["Error"] = "Payment not found!";
            return RedirectToAction(nameof(Index));
        }

        if (payment.PayStatus is "Completed" or "Refunded" || payment.RefundStatus == "Pending")
        {
            TempData["Error"] = "Financial audit records for paid or refunded transactions cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        var transactionRef = payment.TransactionRef;
        _context.Payments.Remove(payment);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Payment {transactionRef} has been deleted successfully! 🗑️";
        return RedirectToAction(nameof(Index));
    }

     
    // 6. MARK AS PAID - Update payment status to Completed
     
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsPaid(int id)
    {
        TempData["Error"] = "Card payments can only be completed by Stripe verification.";
        await Task.CompletedTask;
        return RedirectToAction(nameof(Index));

#pragma warning disable CS0162
        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        var payment = await _context.Payments
            .Include(p => p.Booking)
                .ThenInclude(b => b.Flight)
            .Include(p => p.Booking)
                .ThenInclude(b => b.Passengers)
            .Include(p => p.Booking)
                .ThenInclude(b => b.User)
                    .ThenInclude(u => u.RewardAccount)
            .FirstOrDefaultAsync(p => p.PayId == id);

        if (payment == null)
        {
            TempData["Error"] = "Payment not found!";
            return RedirectToAction(nameof(Index));
        }

        if (payment.PayStatus == "Completed")
        {
            TempData["Warning"] = "This payment is already completed!";
            return RedirectToAction(nameof(Index));
        }

        if (payment.Booking.Status != "Pending")
        {
            TempData["Warning"] = "Only pending bookings can be marked as paid.";
            return RedirectToAction(nameof(Index));
        }

        var booking = payment.Booking;

        if (!booking.Passengers.Any() || booking.Flight.AvailableSeats < booking.Passengers.Count)
        {
            TempData["Error"] = "Not enough seats are available to confirm this booking.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var passengerGroup in booking.Passengers.GroupBy(p => p.ClassId))
        {
            var flightSeatClass = await _context.FlightSeatClasses
                .FirstOrDefaultAsync(fsc =>
                    fsc.FlightId == booking.FlightId && fsc.ClassId == passengerGroup.Key);

            if (flightSeatClass == null || flightSeatClass.AvailableSeats < passengerGroup.Count())
            {
                TempData["Error"] = "Not enough seats are available in the selected class.";
                return RedirectToAction(nameof(Index));
            }

            flightSeatClass.AvailableSeats -= passengerGroup.Count();
        }

        booking.Flight.AvailableSeats -= booking.Passengers.Count;
        booking.Status = "Confirmed";
        payment.PayStatus = "Completed";
        payment.PayDate = DateTime.Now;
        payment.Amount = booking.TotalPrice;

        var rewardAccount = booking.User.RewardAccount;
        if (rewardAccount == null)
        {
            rewardAccount = new RewardAccount
            {
                UserId = booking.UserId,
                PointsBalance = 0
            };
            _context.RewardAccounts.Add(rewardAccount);
            await _context.SaveChangesAsync();
        }

        if (!await _context.PointsTransactions.AnyAsync(t => t.BookingId == booking.BookingId))
        {
            var points = (int)Math.Floor(booking.TotalPrice);
            rewardAccount.PointsBalance += points;
            _context.PointsTransactions.Add(new PointsTransaction
            {
                AccountId = rewardAccount.AccountId,
                BookingId = booking.BookingId,
                Points = points,
                Type = "Earned",
                Date = DateTime.Now,
                Description = $"Earned {points} points from booking {booking.PNR}"
            });
        }

        var existingPassengerIds = await _context.Tickets
            .Where(t => t.BookingId == booking.BookingId)
            .Select(t => t.PassengerId)
            .ToListAsync();

        var seatCounter = existingPassengerIds.Count + 1;
        foreach (var passenger in booking.Passengers.Where(p => !existingPassengerIds.Contains(p.PassengerId)))
        {
            _context.Tickets.Add(new Ticket
            {
                BookingId = booking.BookingId,
                FlightId = booking.FlightId,
                PassengerId = passenger.PassengerId,
                IssueDate = DateTime.Now,
                SeatNum = $"{seatCounter:D2}{GetRandomLetter()}",
                QrCode = Guid.NewGuid().ToString()
            });
            seatCounter++;
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["Success"] = $"Payment {payment.TransactionRef} has been marked as paid!";
        return RedirectToAction(nameof(Index));
#pragma warning restore CS0162
    }

    private string GetRandomLetter()
    {
        var letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var random = new Random();
        return letters[random.Next(letters.Length)].ToString();
    }
}
