using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AeroFly.Web.Data;
using AeroFly.Web.ViewModels;
using AdminModel = AeroFly.Web.Models.Admin;

namespace AeroFly.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "SuperAdminOnly")]
public class UserController : Controller
{
    private readonly ApplicationDbContext _context;

    public UserController(ApplicationDbContext context)
    {
        _context = context;
    }

     
    // 1. INDEX - List all Users with filters
     
    public async Task<IActionResult> Index(
        string? searchTerm,
        string? status,
        bool? isAdmin,
        string? sortBy = "joined")
    {
        ViewData["Title"] = "Users Management";

        var query = _context.Users
            .Include(u => u.Admin)
            .Include(u => u.RewardAccount)
            .Include(u => u.Bookings)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(searchTerm))
        {
            searchTerm = searchTerm.ToLower();
            query = query.Where(u =>
                u.FName.ToLower().Contains(searchTerm) ||
                u.LName.ToLower().Contains(searchTerm) ||
                u.Email.ToLower().Contains(searchTerm) ||
                (u.FName + " " + u.LName).ToLower().Contains(searchTerm));
        }

        if (!string.IsNullOrEmpty(status))
        {
            if (status == "Active")
                query = query.Where(u => u.EmailConfirmed && !u.LockoutEnd.HasValue);
            else if (status == "Locked")
                query = query.Where(u => u.LockoutEnd.HasValue && u.LockoutEnd > DateTime.UtcNow);
            else if (status == "Unverified")
                query = query.Where(u => !u.EmailConfirmed);
        }

        if (isAdmin.HasValue)
        {
            if (isAdmin.Value)
                query = query.Where(u => u.Admin != null);
            else
                query = query.Where(u => u.Admin == null);
        }

        // Apply sorting
        query = sortBy switch
        {
            "name" => query.OrderBy(u => u.FName).ThenBy(u => u.LName),
            "email" => query.OrderBy(u => u.Email),
            "bookings" => query.OrderByDescending(u => u.Bookings.Count),
            "points" => query.OrderByDescending(u => u.RewardAccount != null ? u.RewardAccount.PointsBalance : 0),
            _ => query.OrderByDescending(u => u.CreatedYear)
             .ThenByDescending(u => u.CreatedMonth)
             .ThenByDescending(u => u.CreatedDay)
        };

        var filteredUsers = await query.ToListAsync();

        var users = filteredUsers.Select(u => new UserListVM
        {
            UserId = u.UserId,
            FName = u.FName,
            LName = u.LName,
            FullName = u.FName + " " + u.LName,
            Email = u.Email,
            CreatedDay = u.CreatedDay,
            CreatedMonth = u.CreatedMonth,
            CreatedYear = u.CreatedYear,
            EmailConfirmed = u.EmailConfirmed,
            OtpVerified = u.OtpVerified,
            IsAdmin = u.Admin != null,
            IsLockedOut = u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTime.UtcNow,
            LockoutEnd = u.LockoutEnd,
            LastLoginDate = u.LastLoginDate,
            AccessFailedCount = u.AccessFailedCount,
            RewardPoints = u.RewardAccount != null ? u.RewardAccount.PointsBalance : 0,
            TotalBookings = u.Bookings.Count,
            TotalSpent = u.Bookings.Where(b => b.Status != "Cancelled").Sum(b => b.TotalPrice)
        }).ToList();

        // Get filter data
        ViewBag.Statuses = new List<string> { "Active", "Locked", "Unverified" };

        return View(users);
    }

     
    // 2. DETAILS - View user details
     
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        ViewData["Title"] = "User Details";

        var user = await _context.Users
            .Include(u => u.Admin)
            .Include(u => u.RewardAccount)
            .Include(u => u.Bookings)
                .ThenInclude(b => b.Flight)
                    .ThenInclude(f => f.DepartureAirport)
            .Include(u => u.Bookings)
                .ThenInclude(b => b.Flight)
                    .ThenInclude(f => f.ArrivalAirport)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
        {
            TempData["Error"] = "User not found!";
            return RedirectToAction(nameof(Index));
        }

        var model = new UserDetailsVM
        {
            UserId = user.UserId,
            FName = user.FName,
            LName = user.LName,
            FullName = $"{user.FName} {user.LName}",
            Email = user.Email,
            CreatedDay = user.CreatedDay,
            CreatedMonth = user.CreatedMonth,
            CreatedYear = user.CreatedYear,
            EmailConfirmed = user.EmailConfirmed,
            OtpVerified = user.OtpVerified,
            IsAdmin = user.Admin != null,
            AdminLevel = user.Admin?.AdminLevel,
            IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow,
            LockoutEnd = user.LockoutEnd,
            LastLoginDate = user.LastLoginDate,
            AccessFailedCount = user.AccessFailedCount,
            RewardPoints = user.RewardAccount?.PointsBalance ?? 0,
            TotalBookings = user.Bookings.Count,
            TotalSpent = user.Bookings.Where(b => b.Status != "Cancelled").Sum(b => b.TotalPrice),
            RecentBookings = user.Bookings
                .OrderByDescending(b => b.BookingDate)
                .Take(5)
                .Select(b => new UserBookingSummaryVM
                {
                    BookingId = b.BookingId,
                    FlightNumber = b.Flight.FlightNum,
                    DepartureIata = b.Flight.DepartureAirport.IataCode,
                    ArrivalIata = b.Flight.ArrivalAirport.IataCode,
                    DepartureTime = b.Flight.DepartureTime,
                    BookingDate = b.BookingDate,
                    TotalPrice = b.TotalPrice,
                    Status = b.Status
                }).ToList()
        };

        return View(model);
    }

     
    // 3. BLOCK - Block user
     
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Block(int id, int? minutes = 15)
    {
        var user = await _context.Users
            .Include(u => u.Admin)
            .FirstOrDefaultAsync(u => u.UserId == id);
        if (user == null)
        {
            TempData["Error"] = "User not found!";
            return RedirectToAction(nameof(Index));
        }

        if (user.Admin != null)
        {
            TempData["Error"] = "Cannot block an admin user!";
            return RedirectToAction(nameof(Index));
        }

        user.LockoutEnd = DateTime.UtcNow.AddMinutes(minutes ?? 15);
        user.AccessFailedCount = 5;
        user.SecurityStamp = Guid.NewGuid().ToString("N");

        await _context.SaveChangesAsync();

        TempData["Success"] = $"User {user.FName} {user.LName} has been blocked for {minutes ?? 15} minutes!";
        return RedirectToAction(nameof(Index));
    }

     
    // 4. UNBLOCK - Unblock user
     
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unblock(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            TempData["Error"] = "User not found!";
            return RedirectToAction(nameof(Index));
        }

        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        user.SecurityStamp = Guid.NewGuid().ToString("N");

        await _context.SaveChangesAsync();

        TempData["Success"] = $"User {user.FName} {user.LName} has been unblocked successfully!";
        return RedirectToAction(nameof(Index));
    }

     
    // 5. DELETE - GET (Confirmation)
     
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        ViewData["Title"] = "Delete User";

        var user = await _context.Users
            .Include(u => u.Admin)
            .Include(u => u.RewardAccount)
            .Include(u => u.Bookings)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
        {
            TempData["Error"] = "User not found!";
            return RedirectToAction(nameof(Index));
        }

        if (user.Admin != null)
        {
            TempData["Error"] = "Cannot delete an admin user!";
            return RedirectToAction(nameof(Index));
        }

        var model = new UserListVM
        {
            UserId = user.UserId,
            FullName = $"{user.FName} {user.LName}",
            FName = user.FName,
            LName = user.LName,
            Email = user.Email,
            CreatedDay = user.CreatedDay,
            CreatedMonth = user.CreatedMonth,
            CreatedYear = user.CreatedYear,
            EmailConfirmed = user.EmailConfirmed,
            IsAdmin = user.Admin != null,
            TotalBookings = user.Bookings.Count,
            TotalSpent = user.Bookings.Where(b => b.Status != "Cancelled").Sum(b => b.TotalPrice),
            RewardPoints = user.RewardAccount?.PointsBalance ?? 0
        };

        return View(model);
    }

     
    // 6. DELETE - POST (Confirm)
     
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var user = await _context.Users
            .Include(u => u.Admin)
            .Include(u => u.RewardAccount)
            .Include(u => u.Bookings)
                .ThenInclude(b => b.Passengers)
            .Include(u => u.Bookings)
                .ThenInclude(b => b.Tickets)
            .Include(u => u.Bookings)
                .ThenInclude(b => b.Payment)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
        {
            TempData["Error"] = "User not found!";
            return RedirectToAction(nameof(Index));
        }

        if (user.Admin != null)
        {
            TempData["Error"] = "Cannot delete an admin user!";
            return RedirectToAction(nameof(Index));
        }

        var userName = $"{user.FName} {user.LName}";

        // Delete all related data
        foreach (var booking in user.Bookings)
        {
            if (booking.Payment != null)
                _context.Payments.Remove(booking.Payment);

            if (booking.Tickets.Any())
                _context.Tickets.RemoveRange(booking.Tickets);

            if (booking.Passengers.Any())
                _context.Passengers.RemoveRange(booking.Passengers);
        }

        if (user.Bookings.Any())
            _context.Bookings.RemoveRange(user.Bookings);

        if (user.RewardAccount != null)
        {
            var transactions = await _context.PointsTransactions
                .Where(t => t.AccountId == user.RewardAccount.AccountId)
                .ToListAsync();

            if (transactions.Any())
                _context.PointsTransactions.RemoveRange(transactions);

            _context.RewardAccounts.Remove(user.RewardAccount);
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"User {userName} has been deleted successfully! 🗑️";
        return RedirectToAction(nameof(Index));
    }

    
    // 7. MAKE ADMIN - Promote user to admin
   
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MakeAdmin(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            TempData["Error"] = "User not found!";
            return RedirectToAction(nameof(Index));
        }

        var existingAdmin = await _context.Admins.AnyAsync(a => a.UserId == id);
        if (existingAdmin)
        {
            TempData["Warning"] = "User is already an admin!";
            return RedirectToAction(nameof(Index));
        }

        var admin = new AeroFly.Web.Models.Admin
        {
            UserId = user.UserId,
            AdminLevel = "Admin",
            Permissions = "Read,Write"
        };

        _context.Admins.Add(admin);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await _context.SaveChangesAsync();

        TempData["Success"] = $"User {user.FName} {user.LName} has been promoted to Admin!";
        return RedirectToAction(nameof(Index));
    }

   
    // 8. REMOVE ADMIN - Demote admin to user

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAdmin(int id)
    {
        var admin = await _context.Admins.FirstOrDefaultAsync(a => a.UserId == id);
        if (admin == null)
        {
            TempData["Error"] = "User is not an admin!";
            return RedirectToAction(nameof(Index));
        }

        var superAdminCount = await _context.Admins
            .CountAsync(a => a.AdminLevel == "SuperAdmin");

        if (admin.AdminLevel == "SuperAdmin" && superAdminCount <= 1)
        {
            TempData["Error"] = "Cannot remove the last SuperAdmin!";
            return RedirectToAction(nameof(Index));
        }

        _context.Admins.Remove(admin);
        var user = await _context.Users.FindAsync(id);
        if (user != null)
        {
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        }
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Admin privileges removed from {user?.FName} {user?.LName}!";
        return RedirectToAction(nameof(Index));
    }
}
