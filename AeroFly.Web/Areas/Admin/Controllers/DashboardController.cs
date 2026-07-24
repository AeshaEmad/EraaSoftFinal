using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AeroFly.Web.Data;
using AeroFly.Web.ViewModels;

namespace AeroFly.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Staff")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Dashboard";

        var completedPaymentAmounts = await _context.Payments
            .Where(p => p.PayStatus == "Completed")
            .Select(p => p.Amount)
            .ToListAsync();

        var model = new DashboardVM
        {
            TotalFlights = await _context.Flights.CountAsync(),
            TotalBookings = await _context.Bookings.CountAsync(),
            ActiveUsers = await _context.Users.CountAsync(u => !u.LockoutEnd.HasValue || u.LockoutEnd <= DateTime.UtcNow),
            Revenue = completedPaymentAmounts.Sum()
        };

        return View(model);
    }
}
