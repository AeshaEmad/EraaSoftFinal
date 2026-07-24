using AeroFly.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace AeroFly.Web.Services;

public class SeatHoldCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SeatHoldCleanupService> _logger;

    public SeatHoldCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<SeatHoldCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var workflow = scope.ServiceProvider.GetRequiredService<IBookingWorkflowService>();
                var expiredIds = await db.Bookings
                    .Where(b => b.Status == "Pending" &&
                                b.SeatsReserved &&
                                b.SeatHoldExpiresAt <= DateTime.UtcNow)
                    .Select(b => b.BookingId)
                    .ToListAsync(stoppingToken);

                foreach (var bookingId in expiredIds)
                {
                    await workflow.ReleaseExpiredHoldAsync(bookingId, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to release expired seat holds.");
            }
        }
    }
}
