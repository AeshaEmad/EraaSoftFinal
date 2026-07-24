using AeroFly.Web.Data;
using AeroFly.Web.Models;
using AeroFly.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace AeroFly.Web.Tests;

public class BookingWorkflowServiceTests
{
    [Fact]
    public async Task ConfirmPaidBooking_IsIdempotent_AndDoesNotConsumeHeldSeatsTwice()
    {
        await using var fixture = await WorkflowFixture.CreateAsync();

        var first = await fixture.Workflow.ConfirmPaidBookingAsync(
            fixture.BookingId, "pi_test", 100m);
        var second = await fixture.Workflow.ConfirmPaidBookingAsync(
            fixture.BookingId, "pi_test", 100m);

        Assert.True(first.Success);
        Assert.True(first.Changed);
        Assert.True(second.Success);
        Assert.False(second.Changed);
        Assert.Equal(1, await fixture.Db.Tickets.CountAsync());
        Assert.Equal(4, await fixture.Db.FlightSeatClasses.Select(x => x.AvailableSeats).SingleAsync());
        Assert.Equal(4, await fixture.Db.Flights.Select(x => x.AvailableSeats).SingleAsync());
    }

    [Fact]
    public async Task CancelConfirmedBooking_UsesStripeRefund_ThenReleasesSeat()
    {
        await using var fixture = await WorkflowFixture.CreateAsync();
        await fixture.Workflow.ConfirmPaidBookingAsync(fixture.BookingId, "pi_test", 100m);

        var result = await fixture.Workflow.CancelAndRefundAsync(fixture.BookingId);
        var payment = await fixture.Db.Payments.SingleAsync();
        var booking = await fixture.Db.Bookings.SingleAsync();

        Assert.True(result.Success);
        Assert.Equal("Cancelled", booking.Status);
        Assert.Equal("Refunded", payment.PayStatus);
        Assert.Equal("re_test", payment.StripeRefundId);
        Assert.Equal(5, await fixture.Db.FlightSeatClasses.Select(x => x.AvailableSeats).SingleAsync());
    }

    private sealed class WorkflowFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ApplicationDbContext Db { get; }
        public BookingWorkflowService Workflow { get; }
        public int BookingId { get; private set; }

        private WorkflowFixture(SqliteConnection connection, ApplicationDbContext db)
        {
            _connection = connection;
            Db = db;
            Workflow = new BookingWorkflowService(db, new FakeStripeService());
        }

        public static async Task<WorkflowFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlite(connection)
                    .Options);
            await db.Database.EnsureCreatedAsync();

            var from = new Airport
            {
                Name = "From", City = "Cairo", Country = "EG", IataCode = "CAI"
            };
            var to = new Airport
            {
                Name = "To", City = "Dubai", Country = "AE", IataCode = "DXB"
            };
            var seatClass = await db.SeatClasses.FirstAsync();
            var user = new User
            {
                FName = "Test", LName = "User", Email = "workflow@example.com",
                Password = "hash", CreatedDay = 1, CreatedMonth = 1, CreatedYear = 2026,
                EmailConfirmed = true, OtpVerified = true
            };
            var flight = new Flight
            {
                FlightNum = "AF900", DepartureAirport = from, ArrivalAirport = to,
                DepartureTime = DateTime.UtcNow.AddDays(1),
                ArrivalTime = DateTime.UtcNow.AddDays(1).AddHours(2),
                BasePrice = 100m, AvailableSeats = 4
            };
            var availability = new FlightSeatClass
            {
                Flight = flight, SeatClass = seatClass, AvailableSeats = 4, FinalPrice = 100m
            };
            var booking = new Booking
            {
                User = user, Flight = flight, TotalPrice = 100m, Status = "Pending",
                SeatsReserved = true, SeatHoldExpiresAt = DateTime.UtcNow.AddMinutes(15)
            };
            booking.Passengers.Add(new Passenger
            {
                FullName = "Test Passenger", PassportNumber = "P123456",
                Age = 30, FlightSeatClass = availability
            });
            booking.Payment = new Payment
            {
                Amount = 100m, PayMethod = "CreditCard", PayStatus = "Pending",
                TransactionRef = "pi_test"
            };
            db.Bookings.Add(booking);
            await db.SaveChangesAsync();

            return new WorkflowFixture(connection, db) { BookingId = booking.BookingId };
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class FakeStripeService : IStripeService
    {
        public Task<PaymentIntent> CreatePaymentIntentAsync(
            decimal amount, int bookingId, int userId, string currency = "usd") =>
            throw new NotSupportedException();
        public Task<PaymentIntent> GetPaymentIntentAsync(string paymentIntentId) =>
            throw new NotSupportedException();
        public Task<PaymentIntent> ConfirmPaymentIntentAsync(string paymentIntentId) =>
            throw new NotSupportedException();
        public Task CancelPaymentIntentAsync(string paymentIntentId) => Task.CompletedTask;
        public Task<Refund> CreateRefundAsync(string paymentIntentId, int bookingId) =>
            Task.FromResult(new Refund
            {
                Id = "re_test",
                Status = "succeeded",
                PaymentIntentId = paymentIntentId
            });
        public Event ConstructWebhookEvent(string json, string signature) =>
            throw new NotSupportedException();
    }
}
