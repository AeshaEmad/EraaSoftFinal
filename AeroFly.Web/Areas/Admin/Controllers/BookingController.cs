using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AeroFly.Web.Data;
using AeroFly.Web.Models;
using AeroFly.Web.ViewModels;
using System.Data;
using AeroFly.Web.Services;

namespace AeroFly.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOperations")]
public class BookingController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IBookingWorkflowService _bookingWorkflow;

    public BookingController(
        ApplicationDbContext context,
        IBookingWorkflowService bookingWorkflow)
    {
        _context = context;
        _bookingWorkflow = bookingWorkflow;
    }

     
    // 1. INDEX - List all Bookings with filters
     
    public async Task<IActionResult> Index(
        string? searchTerm,
        string? status,
        string? paymentStatus,
        DateTime? fromDate,
        DateTime? toDate)
    {
        ViewData["Title"] = "Bookings Management";

        var query = _context.Bookings
            .Include(b => b.User)
            .Include(b => b.Flight)
                .ThenInclude(f => f.DepartureAirport)
            .Include(b => b.Flight)
                .ThenInclude(f => f.ArrivalAirport)
            .Include(b => b.Payment)
            .Include(b => b.Passengers)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(searchTerm))
        {
            searchTerm = searchTerm.ToLower();
            query = query.Where(b =>
                b.User.FName.ToLower().Contains(searchTerm) ||
                b.User.LName.ToLower().Contains(searchTerm) ||
                b.User.Email.ToLower().Contains(searchTerm) ||
                b.Flight.FlightNum.ToLower().Contains(searchTerm) ||
                b.PNR.ToLower().Contains(searchTerm));
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(b => b.Status == status);
        }

        if (!string.IsNullOrEmpty(paymentStatus))
        {
            query = query.Where(b => b.Payment != null && b.Payment.PayStatus == paymentStatus);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(b => b.BookingDate.Date >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(b => b.BookingDate.Date <= toDate.Value.Date);
        }

        var bookings = await query
            .OrderByDescending(b => b.BookingDate)
            .Select(b => new BookingListVM
            {
                BookingId = b.BookingId,
                PassengerName = $"{b.User.FName} {b.User.LName}",
                Email = b.User.Email,
                FlightNumber = b.Flight.FlightNum,
                DepartureIata = b.Flight.DepartureAirport.IataCode,
                ArrivalIata = b.Flight.ArrivalAirport.IataCode,
                DepartureTime = b.Flight.DepartureTime,
                BookingDate = b.BookingDate,
                PassengerCount = b.Passengers.Count,
                TotalPrice = b.TotalPrice,
                Status = b.Status,
                PaymentStatus = b.Payment != null ? b.Payment.PayStatus : "Not Paid",
                PointsUsed = b.PointsUsed,
                DiscountApplied = b.DiscountApplied
            })
            .ToListAsync();

        // Get filter data for dropdowns
        ViewBag.Statuses = new List<string> { "Pending", "Confirmed", "Cancelled", "Completed" };
        ViewBag.PaymentStatuses = new List<string> { "Pending", "Completed", "Failed", "Refunded" };

        return View(bookings);
    }

     
    // 2. DETAILS - View booking details
     
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        ViewData["Title"] = "Booking Details";

        var booking = await _context.Bookings
            .Include(b => b.User)
            .Include(b => b.Flight)
                .ThenInclude(f => f.DepartureAirport)
            .Include(b => b.Flight)
                .ThenInclude(f => f.ArrivalAirport)
            .Include(b => b.Payment)
            .Include(b => b.Passengers)
                .ThenInclude(p => p.FlightSeatClass)
                    .ThenInclude(fsc => fsc.SeatClass)
            .Include(b => b.Tickets)
            .FirstOrDefaultAsync(b => b.BookingId == id);

        if (booking == null)
        {
            TempData["Error"] = "Booking not found!";
            return RedirectToAction(nameof(Index));
        }

        var model = new BookingDetailsVM
        {
            BookingId = booking.BookingId,
            BookingDate = booking.BookingDate,
            Status = booking.Status,
            TotalPrice = booking.TotalPrice,
            PointsUsed = booking.PointsUsed,
            DiscountApplied = booking.DiscountApplied,

            UserFullName = $"{booking.User.FName} {booking.User.LName}",
            UserEmail = booking.User.Email,

            FlightNumber = booking.Flight.FlightNum,
            DepartureAirport = booking.Flight.DepartureAirport.Name,
            ArrivalAirport = booking.Flight.ArrivalAirport.Name,
            DepartureIata = booking.Flight.DepartureAirport.IataCode,
            ArrivalIata = booking.Flight.ArrivalAirport.IataCode,
            DepartureTime = booking.Flight.DepartureTime,
            ArrivalTime = booking.Flight.ArrivalTime,
            AvailableSeats = booking.Flight.AvailableSeats,

            PaymentStatus = booking.Payment?.PayStatus,
            PaymentMethod = booking.Payment?.PayMethod,
            PaymentDate = booking.Payment?.PayDate,

            Passengers = booking.Passengers.Select(p => new PassengerInfoVM
            {
                FullName = p.FullName,
                PassportNumber = p.PassportNumber,
                Age = p.Age,
                SeatClass = p.FlightSeatClass?.SeatClass?.ClassName ?? "Not Assigned",
                SeatNumber = p.Ticket?.SeatNum,
                TicketNumber = p.Ticket != null ? $"AF{p.Ticket.TicketId:D8}" : null
            }).ToList()
        };

        return View(model);
    }

     
    // 3. CANCEL - POST (Cancel booking)
     
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await _bookingWorkflow.CancelAndRefundAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));

#pragma warning disable CS0162
        var booking = await _context.Bookings
            .Include(b => b.Payment)
            .Include(b => b.Tickets)
            .Include(b => b.Passengers)
            .Include(b => b.User)
                .ThenInclude(u => u.RewardAccount)
            .Include(b => b.Flight)
            .FirstOrDefaultAsync(b => b.BookingId == id);

        if (booking == null)
        {
            TempData["Error"] = "Booking not found!";
            return RedirectToAction(nameof(Index));
        }

        // Check if booking can be cancelled
        if (booking.Status == "Cancelled")
        {
            TempData["Warning"] = "This booking is already cancelled.";
            return RedirectToAction(nameof(Index));
        }

        if (booking.Status == "Completed")
        {
            TempData["Error"] = "Cannot cancel a completed booking.";
            return RedirectToAction(nameof(Index));
        }

        // Check if flight already departed
        if (booking.Flight.DepartureTime < DateTime.Now)
        {
            TempData["Error"] = "Cannot cancel a booking for a flight that has already departed.";
            return RedirectToAction(nameof(Index));
        }

        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        // Refund points if used
        if (booking.PointsUsed > 0 && booking.User.RewardAccount != null)
        {
            booking.User.RewardAccount.PointsBalance += booking.PointsUsed;
            _context.PointsTransactions.Add(new PointsTransaction
            {
                AccountId = booking.User.RewardAccount.AccountId,
                Points = booking.PointsUsed,
                Type = "Refunded",
                Date = DateTime.Now,
                Description = $"Refunded {booking.PointsUsed} redeemed points after cancelling {booking.PNR}"
            });
        }

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

        // Update payment status if exists
        if (booking.Payment != null && booking.Payment.PayStatus == "Completed")
        {
            booking.Payment.PayStatus = "Refunded";
            booking.Payment.PayDate = DateTime.Now;
        }

        // Cancel tickets
        foreach (var ticket in booking.Tickets)
        {
            // Mark ticket as cancelled (or remove)
            _context.Tickets.Remove(ticket);
        }

        // Update booking status
        booking.Status = "Cancelled";

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["Success"] = $"Booking {booking.PNR} has been cancelled and refunded successfully!";
        return RedirectToAction(nameof(Index));
#pragma warning restore CS0162
    }

     
    // 4. CONFIRM - POST (Confirm booking)
     
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id)
    {
        TempData["Error"] = "Card bookings are confirmed only by a verified Stripe payment or webhook.";
        await Task.CompletedTask;
        return RedirectToAction(nameof(Index));

#pragma warning disable CS0162
        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        var booking = await _context.Bookings
            .Include(b => b.Payment)
            .Include(b => b.Flight)
            .Include(b => b.Passengers)
            .Include(b => b.User)
                .ThenInclude(u => u.RewardAccount)
            .FirstOrDefaultAsync(b => b.BookingId == id);

        if (booking == null)
        {
            TempData["Error"] = "Booking not found!";
            return RedirectToAction(nameof(Index));
        }

        if (booking.Status != "Pending")
        {
            TempData["Warning"] = $"Booking is already {booking.Status}.";
            return RedirectToAction(nameof(Index));
        }

        if (booking.Payment == null || booking.Payment.PayStatus != "Completed")
        {
            TempData["Error"] = "Cannot confirm booking without completed payment.";
            return RedirectToAction(nameof(Index));
        }

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

        TempData["Success"] = $"Booking {booking.PNR} has been confirmed successfully!";
        return RedirectToAction(nameof(Index));
#pragma warning restore CS0162
    }

     
    // 5. DELETE - GET (Confirmation)
     
    [HttpGet]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> Delete(int id)
    {
        ViewData["Title"] = "Delete Booking";

        var booking = await _context.Bookings
            .Include(b => b.User)
            .Include(b => b.Flight)
                .ThenInclude(f => f.DepartureAirport)
            .Include(b => b.Flight)
                .ThenInclude(f => f.ArrivalAirport)
            .Include(b => b.Passengers)
            .FirstOrDefaultAsync(b => b.BookingId == id);

        if (booking == null)
        {
            TempData["Error"] = "Booking not found!";
            return RedirectToAction(nameof(Index));
        }

        var model = new BookingListVM
        {
            BookingId = booking.BookingId,
            PassengerName = $"{booking.User.FName} {booking.User.LName}",
            Email = booking.User.Email,
            FlightNumber = booking.Flight.FlightNum,
            DepartureIata = booking.Flight.DepartureAirport.IataCode,
            ArrivalIata = booking.Flight.ArrivalAirport.IataCode,
            DepartureTime = booking.Flight.DepartureTime,
            BookingDate = booking.BookingDate,
            PassengerCount = booking.Passengers.Count,
            TotalPrice = booking.TotalPrice,
            Status = booking.Status,
            PointsUsed = booking.PointsUsed,
            DiscountApplied = booking.DiscountApplied
        };

        return View(model);
    }

     
    // 6. DELETE - POST (Confirm)
     
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var booking = await _context.Bookings
            .Include(b => b.Payment)
            .Include(b => b.Tickets)
            .Include(b => b.Passengers)
            .Include(b => b.Flight)
            .Include(b => b.User)
                .ThenInclude(u => u.RewardAccount)
            .FirstOrDefaultAsync(b => b.BookingId == id);

        if (booking == null)
        {
            TempData["Error"] = "Booking not found!";
            return RedirectToAction(nameof(Index));
        }

        var pnr = booking.PNR;

        if (booking.Payment is { PayStatus: "Completed" })
        {
            TempData["Error"] = "Refund the Stripe payment before deleting this booking.";
            return RedirectToAction(nameof(Index));
        }

        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

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

        var earnedTransaction = await _context.PointsTransactions
            .FirstOrDefaultAsync(t => t.BookingId == booking.BookingId);

        if (earnedTransaction != null)
        {
            if (booking.User.RewardAccount != null)
            {
                booking.User.RewardAccount.PointsBalance = Math.Max(
                    0,
                    booking.User.RewardAccount.PointsBalance - earnedTransaction.Points);
            }

            _context.PointsTransactions.Remove(earnedTransaction);
        }

        // Delete related data
        if (booking.Payment != null)
        {
            _context.Payments.Remove(booking.Payment);
        }

        if (booking.Tickets.Any())
        {
            _context.Tickets.RemoveRange(booking.Tickets);
        }

        if (booking.Passengers.Any())
        {
            _context.Passengers.RemoveRange(booking.Passengers);
        }

        _context.Bookings.Remove(booking);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["Success"] = $"Booking {pnr} has been deleted permanently! 🗑️";
        return RedirectToAction(nameof(Index));
    }

     
    // 7. ADD REWARD POINTS - Helper method (Admin can also add points)
     
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRewardPoints(int userId, int points)
    {
        if (points <= 0)
        {
            TempData["Error"] = "Points must be greater than zero.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _context.Users
            .Include(u => u.RewardAccount)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null)
        {
            TempData["Error"] = "User not found!";
            return RedirectToAction(nameof(Index));
        }

        var rewardAccount = user.RewardAccount;

        if (rewardAccount == null)
        {
            rewardAccount = new RewardAccount
            {
                UserId = userId,
                PointsBalance = 0
            };
            _context.RewardAccounts.Add(rewardAccount);
            await _context.SaveChangesAsync();
        }

        rewardAccount.PointsBalance += points;

        var transaction = new PointsTransaction
        {
            AccountId = rewardAccount.AccountId,
            Points = points,
            Type = "Earned",
            Date = DateTime.Now,
            Description = $"Admin added {points} points"
        };

        _context.PointsTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Added {points} points to user {user.FName} {user.LName}!";
        return RedirectToAction("Details", "User", new { id = userId });
    }

    private string GetRandomLetter()
    {
        var letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var random = new Random();
        return letters[random.Next(letters.Length)].ToString();
    }
}
