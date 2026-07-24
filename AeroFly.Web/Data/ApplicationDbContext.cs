

using Microsoft.EntityFrameworkCore;
using AeroFly.Web.Models;

namespace AeroFly.Web.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Admin> Admins { get; set; }
    public DbSet<RewardAccount> RewardAccounts { get; set; }
    public DbSet<PointsTransaction> PointsTransactions { get; set; }
    public DbSet<Airport> Airports { get; set; }
    public DbSet<Flight> Flights { get; set; }
    public DbSet<SeatClass> SeatClasses { get; set; }
    public DbSet<FlightSeatClass> FlightSeatClasses { get; set; }
    public DbSet<PriceRule> PriceRules { get; set; }
    public DbSet<FlightRule> FlightRules { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<Passenger> Passengers { get; set; }
    public DbSet<StripeWebhookEvent> StripeWebhookEvents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Composite Keys
        modelBuilder.Entity<FlightSeatClass>()
            .HasKey(f => new { f.FlightId, f.ClassId });

        modelBuilder.Entity<FlightRule>()
            .HasKey(f => new { f.FlightId, f.RuleId });

        // Relationships
        modelBuilder.Entity<Flight>()
            .HasOne(f => f.DepartureAirport)
            .WithMany(a => a.DepartureFlights)
            .HasForeignKey(f => f.DepartureAirportId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Flight>()
            .HasOne(f => f.ArrivalAirport)
            .WithMany(a => a.ArrivalFlights)
            .HasForeignKey(f => f.ArrivalAirportId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Passenger>()
            .HasOne(p => p.FlightSeatClass)
            .WithMany(f => f.Passengers)
            .HasForeignKey(p => new { p.FlightId, p.ClassId });

        modelBuilder.Entity<Admin>()
            .HasOne(a => a.User)
            .WithOne(u => u.Admin)
            .HasForeignKey<Admin>(a => a.UserId);

        modelBuilder.Entity<RewardAccount>()
            .HasOne(r => r.User)
            .WithOne(u => u.RewardAccount)
            .HasForeignKey<RewardAccount>(r => r.UserId);

        modelBuilder.Entity<PointsTransaction>()
            .HasOne(t => t.RewardAccount)
            .WithMany(r => r.Transactions)
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PointsTransaction>()
            .HasOne(t => t.Booking)
            .WithMany()
            .HasForeignKey(t => t.BookingId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<PointsTransaction>()
            .HasIndex(t => t.BookingId)
            .HasFilter("[BookingId] IS NOT NULL");

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.Booking)
            .WithMany(b => b.Tickets)
            .HasForeignKey(t => t.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.Passenger)
            .WithOne(p => p.Ticket)
            .HasForeignKey<Ticket>(t => t.PassengerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Ticket>()
            .HasIndex(t => t.QrCode)
            .IsUnique();

        modelBuilder.Entity<Ticket>()
            .HasIndex(t => new { t.FlightId, t.SeatNum })
            .IsUnique();

        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.Flight)
            .WithMany()
            .HasForeignKey(t => t.FlightId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.BookingId)
            .IsUnique();

        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.TransactionRef)
            .IsUnique();

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Flight)
            .WithMany()
            .HasForeignKey(b => b.FlightId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.User)
            .WithMany(u => u.Bookings)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Precision for decimals
        modelBuilder.Entity<Flight>().Property(f => f.BasePrice).HasPrecision(10, 2);
        modelBuilder.Entity<FlightSeatClass>().Property(f => f.FinalPrice).HasPrecision(10, 2);
        modelBuilder.Entity<Booking>().Property(b => b.TotalPrice).HasPrecision(10, 2);
        modelBuilder.Entity<Payment>().Property(p => p.Amount).HasPrecision(10, 2);
        modelBuilder.Entity<PriceRule>().Property(p => p.Multiplier).HasPrecision(5, 2);
        modelBuilder.Entity<SeatClass>().Property(s => s.ClassMultiplier).HasPrecision(5, 2);

        // Seed data for SeatClass
        modelBuilder.Entity<SeatClass>().HasData(
            new SeatClass { ClassId = 1, ClassName = "Economy", ClassMultiplier = 1.0m },
            new SeatClass { ClassId = 2, ClassName = "Business", ClassMultiplier = 2.0m },
            new SeatClass { ClassId = 3, ClassName = "First Class", ClassMultiplier = 4.0m }
        );
    }
}
