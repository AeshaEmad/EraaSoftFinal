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
    }

    private static string GetRandomLetter()
    {
        var letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        return letters[Random.Shared.Next(letters.Length)].ToString();
    }
}
