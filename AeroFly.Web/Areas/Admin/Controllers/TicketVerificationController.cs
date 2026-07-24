using System.Data;
using AeroFly.Web.Data;
using AeroFly.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AeroFly.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Staff")]
public class TicketVerificationController : Controller
{
    private readonly ApplicationDbContext _db;

    public TicketVerificationController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Scan() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return View("Result", new TicketVerificationVM { Message = "Scan or enter a ticket token." });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var ticket = await _db.Tickets
            .Include(t => t.Passenger)
            .Include(t => t.Flight)
            .Include(t => t.Booking)
            .FirstOrDefaultAsync(t => t.QrCode == token.Trim(), cancellationToken);

        if (ticket == null)
        {
            return View("Result", new TicketVerificationVM { Message = "Ticket was not found or is counterfeit." });
        }

        var model = new TicketVerificationVM
        {
            PassengerName = ticket.Passenger.FullName,
            PassportNumber = ticket.Passenger.PassportNumber,
            FlightNumber = ticket.Flight.FlightNum,
            SeatNumber = ticket.SeatNum,
            BookingStatus = ticket.Booking.Status,
            UsedAt = ticket.UsedAt
        };

        if (ticket.Booking.Status is not ("Confirmed" or "Completed"))
        {
            model.Message = $"Ticket is invalid because the booking is {ticket.Booking.Status}.";
            return View("Result", model);
        }

        if (ticket.IsUsed)
        {
            model.WasAlreadyUsed = true;
            model.Message = "Ticket has already been used.";
            return View("Result", model);
        }

        ticket.IsUsed = true;
        ticket.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        model.IsValid = true;
        model.UsedAt = ticket.UsedAt;
        model.Message = "Ticket is valid. Passenger checked in successfully.";
        return View("Result", model);
    }
}
