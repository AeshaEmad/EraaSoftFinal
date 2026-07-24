using AeroFly.Web.Data;
using AeroFly.Web.Models;
using AeroFly.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Data;
using AeroFly.Web.Services;

namespace AeroFly.Web.Areas.User.Controllers;

[Area("User")]
[Authorize]
public class RewardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IStripeService _stripe;

    public RewardController(ApplicationDbContext context, IStripeService stripe)
    {
        _context = context;
        _stripe = stripe;
    }

    // MY REWARDS - Show points and transaction history
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var rewardAccount = await _context.RewardAccounts
            .Include(r => r.Transactions)
            .FirstOrDefaultAsync(r => r.UserId == userId);

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

        var transactions = await _context.PointsTransactions
            .Where(t => t.AccountId == rewardAccount.AccountId)
            .OrderByDescending(t => t.Date)
            .ToListAsync();

        var model = new RewardViewModel
        {
            PointsBalance = rewardAccount.PointsBalance,
            Transactions = transactions.Select(t => new TransactionVM
            {
                Points = t.Points,
                Type = t.Type,
                Date = t.Date,
                Description = t.Description ?? $"{t.Type} points"
            }).ToList()
        };

        return View(model);
    }

    // USE POINTS - Apply points as discount
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UsePoints(int bookingId, int pointsToUse)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (pointsToUse <= 0)
        {
            TempData["Error"] = "Points to use must be greater than zero.";
            return RedirectToAction("Payment", "Booking", new { bookingId });
        }

        await using var dbTransaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        var booking = await _context.Bookings
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);

        if (booking == null)
        {
            TempData["Error"] = "Booking not found!";
            return RedirectToAction("MyBookings", "Booking");
        }

        if (booking.Status != "Pending")
        {
            TempData["Error"] = "Cannot use points on this booking.";
            return RedirectToAction("MyBookings", "Booking");
        }

        if (booking.DiscountApplied)
        {
            TempData["Error"] = "Reward points have already been applied to this booking.";
            return RedirectToAction("Payment", "Booking", new { bookingId });
        }

        var rewardAccount = await _context.RewardAccounts
            .FirstOrDefaultAsync(r => r.UserId == userId);

        if (rewardAccount == null || rewardAccount.PointsBalance < pointsToUse)
        {
            TempData["Error"] = "Not enough points. You can continue with the full price.";
            return RedirectToAction("Payment", "Booking", new { bookingId });
        }

        // 100 points = $10 discount
        var discount = pointsToUse * 0.10m;
        var newTotal = booking.TotalPrice - discount;

        if (newTotal <= 0)
        {
            TempData["Error"] = "Discount cannot exceed total price.";
            return RedirectToAction("Payment", "Booking", new { bookingId });
        }

        if (booking.Payment?.TransactionRef.StartsWith("pi_", StringComparison.Ordinal) == true)
        {
            try
            {
                await _stripe.CancelPaymentIntentAsync(booking.Payment.TransactionRef);
            }
            catch (Stripe.StripeException)
            {
                TempData["Error"] = "Could not cancel the previous payment session. No points were used.";
                return RedirectToAction("Payment", "Booking", new { bookingId });
            }
        }

        // Update points
        rewardAccount.PointsBalance -= pointsToUse;

        // Add transaction
        var transaction = new PointsTransaction
        {
            AccountId = rewardAccount.AccountId,
            Points = -pointsToUse,
            Type = "Redeemed",
            Date = DateTime.Now,
            Description = $"Redeemed {pointsToUse} points for ${discount} discount",
            BookingId = booking.BookingId
        };
        _context.PointsTransactions.Add(transaction);

        // Update booking
        booking.TotalPrice = newTotal;
        booking.PointsUsed = pointsToUse;
        booking.DiscountApplied = true;

        if (booking.Payment != null)
        {
            booking.Payment.Amount = newTotal;
            booking.Payment.TransactionRef = Guid.NewGuid().ToString();
        }

        await _context.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        TempData["Success"] = $"Used {pointsToUse} points and saved ${discount} on your booking!";
        return RedirectToAction("Payment", "Booking", new { bookingId });
    }
}
