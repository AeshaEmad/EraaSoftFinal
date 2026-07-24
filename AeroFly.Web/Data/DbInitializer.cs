using AeroFly.Web.Models;
using AeroFly.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AeroFly.Web.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var pricingService = scope.ServiceProvider.GetRequiredService<IPricingService>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        // Apply SQL Server migrations in production; create the schema directly for local SQLite runs.
        if (context.Database.IsSqlServer())
        {
            await context.Database.MigrateAsync();
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
        }

        // Only bootstrap a new admin if no admins exist yet.
        if (!await context.Admins.AnyAsync())
        {
            var email = configuration["Security:BootstrapAdminEmail"]?.Trim().ToLowerInvariant();
            var password = configuration["Security:BootstrapAdminPassword"];
            if (!string.IsNullOrWhiteSpace(email) && IsStrongBootstrapPassword(password))
            {
                // Check if the user already exists (e.g. old DbInitializer locked them out)
                var existingUser = await context.Users
                    .FirstOrDefaultAsync(u => u.Email == email);

                if (existingUser != null)
                {
                    // Re-enable the existing locked-out user
                    existingUser.Password = passwordHasher.HashPassword(existingUser, password!);
                    existingUser.LockoutEnd = null;
                    existingUser.AccessFailedCount = 0;
                    existingUser.SecurityStamp = Guid.NewGuid().ToString("N");
                    existingUser.EmailConfirmed = true;
                    existingUser.OtpVerified = true;
                    existingUser.MustChangePassword = true;
                    existingUser.FName = "Initial";
                    existingUser.LName = "Administrator";
                    await context.SaveChangesAsync();

                    context.Admins.Add(new Admin
                    {
                        UserId = existingUser.UserId,
                        AdminLevel = "SuperAdmin",
                        Permissions = "All"
                    });
                    await context.SaveChangesAsync();
                }
                else
                {
                    // Create brand-new admin user
                    var now = DateTime.UtcNow;
                    var adminUser = new User
                    {
                        FName = "Initial",
                        LName = "Administrator",
                        Email = email,
                        Password = string.Empty,
                        CreatedDay = now.Day,
                        CreatedMonth = now.Month,
                        CreatedYear = now.Year,
                        EmailConfirmed = true,
                        OtpVerified = true,
                        MustChangePassword = true
                    };
                    adminUser.Password = passwordHasher.HashPassword(adminUser, password);
                    context.Users.Add(adminUser);
                    await context.SaveChangesAsync();
                    context.Admins.Add(new Admin
                    {
                        UserId = adminUser.UserId,
                        AdminLevel = "SuperAdmin",
                        Permissions = "All"
                    });
                    context.RewardAccounts.Add(new RewardAccount { UserId = adminUser.UserId });
                    await context.SaveChangesAsync();
                }
            }
        }

        // ============================================================
        // 3. SEED AIRPORTS
        // ============================================================
        if (!await context.Airports.AnyAsync())
        {
            var airports = new List<Airport>
            {
                new Airport { Name = "Cairo International Airport", City = "Cairo", Country = "Egypt", IataCode = "CAI", Latitude = 30.1219, Longitude = 31.4056 },
                new Airport { Name = "Dubai International Airport", City = "Dubai", Country = "UAE", IataCode = "DXB", Latitude = 25.2532, Longitude = 55.3657 },
                new Airport { Name = "London Heathrow Airport", City = "London", Country = "UK", IataCode = "LHR", Latitude = 51.4700, Longitude = -0.4543 },
                new Airport { Name = "John F. Kennedy Airport", City = "New York", Country = "USA", IataCode = "JFK", Latitude = 40.6413, Longitude = -73.7781 },
                new Airport { Name = "Paris Charles de Gaulle", City = "Paris", Country = "France", IataCode = "CDG", Latitude = 49.0097, Longitude = 2.5479 },
                new Airport { Name = "Tokyo Haneda Airport", City = "Tokyo", Country = "Japan", IataCode = "HND", Latitude = 35.5494, Longitude = 139.7798 },
                new Airport { Name = "Rome Fiumicino Airport", City = "Rome", Country = "Italy", IataCode = "FCO", Latitude = 41.8003, Longitude = 12.2389 },
                new Airport { Name = "Singapore Changi Airport", City = "Singapore", Country = "Singapore", IataCode = "SIN", Latitude = 1.3644, Longitude = 103.9915 },
                new Airport { Name = "Istanbul Airport", City = "Istanbul", Country = "Turkey", IataCode = "IST", Latitude = 41.2600, Longitude = 28.7420 },
                new Airport { Name = "Frankfurt Airport", City = "Frankfurt", Country = "Germany", IataCode = "FRA", Latitude = 50.1109, Longitude = 8.6821 },
                new Airport { Name = "Amsterdam Schiphol", City = "Amsterdam", Country = "Netherlands", IataCode = "AMS", Latitude = 52.3105, Longitude = 4.7683 },
                new Airport { Name = "Barcelona Airport", City = "Barcelona", Country = "Spain", IataCode = "BCN", Latitude = 41.2974, Longitude = 2.0833 }
            };

            await context.Airports.AddRangeAsync(airports);
            await context.SaveChangesAsync();
        }

        // ============================================================
        // 4. SEED SEAT CLASSES
        // ============================================================
        if (!await context.SeatClasses.AnyAsync())
        {
            var seatClasses = new List<SeatClass>
            {
                new SeatClass { ClassName = "Economy", ClassMultiplier = 1.0m },
                new SeatClass { ClassName = "Business", ClassMultiplier = 2.0m },
                new SeatClass { ClassName = "First Class", ClassMultiplier = 4.0m }
            };

            await context.SeatClasses.AddRangeAsync(seatClasses);
            await context.SaveChangesAsync();
        }

        // ============================================================
        // 5. SEED FLIGHTS
        // ============================================================
        if (!await context.Flights.AnyAsync())
        {
            var airports = await context.Airports.ToListAsync();
            var cai = airports.First(a => a.IataCode == "CAI");
            var dxb = airports.First(a => a.IataCode == "DXB");
            var lhr = airports.First(a => a.IataCode == "LHR");
            var jfk = airports.First(a => a.IataCode == "JFK");
            var cdg = airports.First(a => a.IataCode == "CDG");
            var hnd = airports.First(a => a.IataCode == "HND");
            var fco = airports.First(a => a.IataCode == "FCO");
            var sin = airports.First(a => a.IataCode == "SIN");
            var ist = airports.First(a => a.IataCode == "IST");
            var fra = airports.First(a => a.IataCode == "FRA");
            var ams = airports.First(a => a.IataCode == "AMS");
            var bcn = airports.First(a => a.IataCode == "BCN");

            var flights = new List<Flight>
            {
                
                // Cairo → Dubai
                new Flight { FlightNum = "AF101", DepartureAirportId = cai.AirportId, ArrivalAirportId = dxb.AirportId, DepartureTime = DateTime.Now.AddDays(1).AddHours(8), ArrivalTime = DateTime.Now.AddDays(1).AddHours(12), BasePrice = 350, AvailableSeats = 150, Status = "Scheduled" },
                // Dubai → London
                new Flight { FlightNum = "AF202", DepartureAirportId = dxb.AirportId, ArrivalAirportId = lhr.AirportId, DepartureTime = DateTime.Now.AddDays(2).AddHours(10), ArrivalTime = DateTime.Now.AddDays(2).AddHours(14), BasePrice = 450, AvailableSeats = 120, Status = "Scheduled" },
                // London → New York
                new Flight { FlightNum = "AF303", DepartureAirportId = lhr.AirportId, ArrivalAirportId = jfk.AirportId, DepartureTime = DateTime.Now.AddDays(3).AddHours(9), ArrivalTime = DateTime.Now.AddDays(3).AddHours(13), BasePrice = 550, AvailableSeats = 100, Status = "Scheduled" },
                // New York → Paris
                new Flight { FlightNum = "AF404", DepartureAirportId = jfk.AirportId, ArrivalAirportId = cdg.AirportId, DepartureTime = DateTime.Now.AddDays(4).AddHours(7), ArrivalTime = DateTime.Now.AddDays(4).AddHours(11), BasePrice = 500, AvailableSeats = 130, Status = "Scheduled" },
                // Paris → Tokyo
                new Flight { FlightNum = "AF505", DepartureAirportId = cdg.AirportId, ArrivalAirportId = hnd.AirportId, DepartureTime = DateTime.Now.AddDays(5).AddHours(6), ArrivalTime = DateTime.Now.AddDays(5).AddHours(14), BasePrice = 800, AvailableSeats = 80, Status = "Scheduled" },
                // Rome → Singapore
                new Flight { FlightNum = "AF606", DepartureAirportId = fco.AirportId, ArrivalAirportId = sin.AirportId, DepartureTime = DateTime.Now.AddDays(6).AddHours(8), ArrivalTime = DateTime.Now.AddDays(6).AddHours(16), BasePrice = 750, AvailableSeats = 90, Status = "Scheduled" },
                // Singapore → Tokyo
                new Flight { FlightNum = "AF909", DepartureAirportId = sin.AirportId, ArrivalAirportId = hnd.AirportId, DepartureTime = DateTime.Now.AddDays(3).AddHours(12), ArrivalTime = DateTime.Now.AddDays(3).AddHours(18), BasePrice = 650, AvailableSeats = 95, Status = "Scheduled" },

                
                // Cairo → London (Delayed)
                new Flight { FlightNum = "AF707", DepartureAirportId = cai.AirportId, ArrivalAirportId = lhr.AirportId, DepartureTime = DateTime.Now.AddDays(1).AddHours(14), ArrivalTime = DateTime.Now.AddDays(1).AddHours(18), BasePrice = 400, AvailableSeats = 110, Status = "Delayed" },
                // Dubai → Rome (Completed)
                new Flight { FlightNum = "AF808", DepartureAirportId = dxb.AirportId, ArrivalAirportId = fco.AirportId, DepartureTime = DateTime.Now.AddDays(-1).AddHours(6), ArrivalTime = DateTime.Now.AddDays(-1).AddHours(10), BasePrice = 380, AvailableSeats = 45, Status = "Completed" },

               
                // Cairo → Istanbul
                new Flight { FlightNum = "AF110", DepartureAirportId = cai.AirportId, ArrivalAirportId = ist.AirportId, DepartureTime = DateTime.Now.AddDays(1).AddHours(6), ArrivalTime = DateTime.Now.AddDays(1).AddHours(8), BasePrice = 200, AvailableSeats = 160, Status = "Scheduled" },
                // Istanbul → Frankfurt
                new Flight { FlightNum = "AF111", DepartureAirportId = ist.AirportId, ArrivalAirportId = fra.AirportId, DepartureTime = DateTime.Now.AddDays(2).AddHours(9), ArrivalTime = DateTime.Now.AddDays(2).AddHours(11), BasePrice = 250, AvailableSeats = 140, Status = "Scheduled" },
                // Frankfurt → Amsterdam
                new Flight { FlightNum = "AF112", DepartureAirportId = fra.AirportId, ArrivalAirportId = ams.AirportId, DepartureTime = DateTime.Now.AddDays(3).AddHours(8), ArrivalTime = DateTime.Now.AddDays(3).AddHours(9), BasePrice = 150, AvailableSeats = 170, Status = "Scheduled" },
                // Amsterdam → Barcelona
                new Flight { FlightNum = "AF113", DepartureAirportId = ams.AirportId, ArrivalAirportId = bcn.AirportId, DepartureTime = DateTime.Now.AddDays(4).AddHours(10), ArrivalTime = DateTime.Now.AddDays(4).AddHours(12), BasePrice = 180, AvailableSeats = 155, Status = "Scheduled" },
                // Barcelona → Paris
                new Flight { FlightNum = "AF114", DepartureAirportId = bcn.AirportId, ArrivalAirportId = cdg.AirportId, DepartureTime = DateTime.Now.AddDays(5).AddHours(11), ArrivalTime = DateTime.Now.AddDays(5).AddHours(13), BasePrice = 190, AvailableSeats = 145, Status = "Scheduled" },
                // Paris → Rome
                new Flight { FlightNum = "AF115", DepartureAirportId = cdg.AirportId, ArrivalAirportId = fco.AirportId, DepartureTime = DateTime.Now.AddDays(6).AddHours(12), ArrivalTime = DateTime.Now.AddDays(6).AddHours(14), BasePrice = 220, AvailableSeats = 135, Status = "Scheduled" },
                // Rome → Dubai
                new Flight { FlightNum = "AF116", DepartureAirportId = fco.AirportId, ArrivalAirportId = dxb.AirportId, DepartureTime = DateTime.Now.AddDays(7).AddHours(13), ArrivalTime = DateTime.Now.AddDays(7).AddHours(17), BasePrice = 420, AvailableSeats = 125, Status = "Scheduled" },
                // Dubai → Singapore
                new Flight { FlightNum = "AF117", DepartureAirportId = dxb.AirportId, ArrivalAirportId = sin.AirportId, DepartureTime = DateTime.Now.AddDays(8).AddHours(14), ArrivalTime = DateTime.Now.AddDays(8).AddHours(22), BasePrice = 580, AvailableSeats = 105, Status = "Scheduled" },
                // Singapore → Tokyo
                new Flight { FlightNum = "AF118", DepartureAirportId = sin.AirportId, ArrivalAirportId = hnd.AirportId, DepartureTime = DateTime.Now.AddDays(9).AddHours(15), ArrivalTime = DateTime.Now.AddDays(9).AddHours(21), BasePrice = 600, AvailableSeats = 100, Status = "Scheduled" },
                // Tokyo → London
                new Flight { FlightNum = "AF119", DepartureAirportId = hnd.AirportId, ArrivalAirportId = lhr.AirportId, DepartureTime = DateTime.Now.AddDays(10).AddHours(16), ArrivalTime = DateTime.Now.AddDays(10).AddHours(20), BasePrice = 720, AvailableSeats = 110, Status = "Scheduled" },
                // London → New York
                new Flight { FlightNum = "AF120", DepartureAirportId = lhr.AirportId, ArrivalAirportId = jfk.AirportId, DepartureTime = DateTime.Now.AddDays(11).AddHours(17), ArrivalTime = DateTime.Now.AddDays(11).AddHours(21), BasePrice = 500, AvailableSeats = 115, Status = "Scheduled" },
            };

            await context.Flights.AddRangeAsync(flights);
            await context.SaveChangesAsync();

            // Add FlightSeatClasses
            var seatClasses = await context.SeatClasses.ToListAsync();
            var allFlights = await context.Flights.ToListAsync();
            var flightSeatClasses = new List<FlightSeatClass>();

            foreach (var flight in allFlights)
            {
                foreach (var seatClass in seatClasses)
                {
                    flightSeatClasses.Add(new FlightSeatClass
                    {
                        FlightId = flight.FlightId,
                        ClassId = seatClass.ClassId,
                        AvailableSeats = flight.AvailableSeats / 3,
                        FinalPrice = pricingService.CalculateFinalPrice(
                            flight.BasePrice,
                            airports.First(a => a.AirportId == flight.DepartureAirportId),
                            airports.First(a => a.AirportId == flight.ArrivalAirportId),
                            seatClass.ClassMultiplier)
                    });
                }
            }

            await context.FlightSeatClasses.AddRangeAsync(flightSeatClasses);
            await context.SaveChangesAsync();
        }

        // Keep existing flights aligned with the documented seat multipliers and distance pricing.
        var configuredSeatClasses = await context.SeatClasses.ToListAsync();
        var businessClass = configuredSeatClasses.FirstOrDefault(s => s.ClassName == "Business");
        if (businessClass != null)
        {
            businessClass.ClassMultiplier = 2.0m;
        }

        var configuredFlights = await context.Flights
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .Include(f => f.FlightSeatClasses)
            .ToListAsync();

        foreach (var flight in configuredFlights)
        {
            foreach (var seatClass in configuredSeatClasses)
            {
                var flightSeatClass = flight.FlightSeatClasses
                    .FirstOrDefault(fsc => fsc.ClassId == seatClass.ClassId);

                if (flightSeatClass == null)
                {
                    flightSeatClass = new FlightSeatClass
                    {
                        FlightId = flight.FlightId,
                        ClassId = seatClass.ClassId,
                        AvailableSeats = configuredSeatClasses.Count == 0
                            ? 0
                            : flight.AvailableSeats / configuredSeatClasses.Count
                    };
                    flight.FlightSeatClasses.Add(flightSeatClass);
                }

                flightSeatClass.FinalPrice = pricingService.CalculateFinalPrice(
                    flight.BasePrice,
                    flight.DepartureAirport,
                    flight.ArrivalAirport,
                    seatClass.ClassMultiplier);
            }

            // FlightSeatClass is the authoritative availability source.
            flight.AvailableSeats = flight.FlightSeatClasses.Sum(fsc => fsc.AvailableSeats);
        }

        await context.SaveChangesAsync();

        // ============================================================
        // 6. SEED BOOKINGS
        // ============================================================
        if (configuration.GetValue<bool>("SeedDemoData") &&
            !await context.Bookings.AnyAsync())
        {
            var adminUser = await context.Admins.Select(a => a.User).FirstOrDefaultAsync();
            var sampleUser = await context.Users.FirstOrDefaultAsync(u => u.Admin == null);
            var flights = await context.Flights.ToListAsync();
            var seatClasses = await context.SeatClasses.ToListAsync();

            var flight1 = flights.FirstOrDefault(f => f.FlightNum == "AF101");
            var flight2 = flights.FirstOrDefault(f => f.FlightNum == "AF202");
            var flight3 = flights.FirstOrDefault(f => f.FlightNum == "AF303");
            var flight4 = flights.FirstOrDefault(f => f.FlightNum == "AF404");
            var flight5 = flights.FirstOrDefault(f => f.FlightNum == "AF505");

            var economy = seatClasses.FirstOrDefault(s => s.ClassName == "Economy");
            var business = seatClasses.FirstOrDefault(s => s.ClassName == "Business");

            // ============================================================
            // BOOKING 1: Admin - Confirmed (AF101)
            // ============================================================
            if (adminUser != null && flight1 != null)
            {
                var booking1 = new Booking
                {
                    UserId = adminUser.UserId,
                    FlightId = flight1.FlightId,
                    BookingDate = DateTime.Now.AddDays(-2),
                    Status = "Confirmed",
                    TotalPrice = 350,
                    DiscountApplied = false,
                    PointsUsed = 0
                };
                context.Bookings.Add(booking1);
                await context.SaveChangesAsync();

                var p1 = new Passenger { FullName = "Ahmed Mohamed", PassportNumber = "A1234567", Age = 35, BookingId = booking1.BookingId, FlightId = flight1.FlightId, ClassId = economy?.ClassId ?? 1 };
                var p2 = new Passenger { FullName = "Sara Ahmed", PassportNumber = "A1234568", Age = 32, BookingId = booking1.BookingId, FlightId = flight1.FlightId, ClassId = economy?.ClassId ?? 1 };
                context.Passengers.AddRange(p1, p2);
                await context.SaveChangesAsync();

                var payment1 = new Payment { BookingId = booking1.BookingId, Amount = 350, PayMethod = "CreditCard", PayStatus = "Completed", PayDate = DateTime.Now.AddDays(-2), TransactionRef = Guid.NewGuid().ToString() };
                context.Payments.Add(payment1);
                await context.SaveChangesAsync();

                context.Tickets.AddRange(
                    new Ticket { BookingId = booking1.BookingId, FlightId = booking1.FlightId, PassengerId = p1.PassengerId, IssueDate = DateTime.Now.AddDays(-2), SeatNum = "12A", QrCode = Guid.NewGuid().ToString() },
                    new Ticket { BookingId = booking1.BookingId, FlightId = booking1.FlightId, PassengerId = p2.PassengerId, IssueDate = DateTime.Now.AddDays(-2), SeatNum = "12B", QrCode = Guid.NewGuid().ToString() }
                );
                await context.SaveChangesAsync();
            }

            // ============================================================
            // BOOKING 2: John Doe - Pending (AF202)
            // ============================================================
            if (sampleUser != null && flight2 != null)
            {
                var booking2 = new Booking
                {
                    UserId = sampleUser.UserId,
                    FlightId = flight2.FlightId,
                    BookingDate = DateTime.Now.AddDays(-1),
                    Status = "Pending",
                    TotalPrice = 1125,
                    DiscountApplied = false,
                    PointsUsed = 0
                };
                context.Bookings.Add(booking2);
                await context.SaveChangesAsync();

                var p3 = new Passenger { FullName = "John Doe", PassportNumber = "B9876543", Age = 40, BookingId = booking2.BookingId, FlightId = flight2.FlightId, ClassId = business?.ClassId ?? 2 };
                context.Passengers.Add(p3);
                await context.SaveChangesAsync();

                var payment2 = new Payment { BookingId = booking2.BookingId, Amount = 1125, PayMethod = "PayPal", PayStatus = "Pending", PayDate = DateTime.Now.AddDays(-1), TransactionRef = Guid.NewGuid().ToString() };
                context.Payments.Add(payment2);
                await context.SaveChangesAsync();
            }

            // ============================================================
            // BOOKING 3: John Doe - Cancelled (AF303)
            // ============================================================
            if (sampleUser != null && flight3 != null)
            {
                var booking3 = new Booking
                {
                    UserId = sampleUser.UserId,
                    FlightId = flight3.FlightId,
                    BookingDate = DateTime.Now.AddDays(-5),
                    Status = "Cancelled",
                    TotalPrice = 550,
                    DiscountApplied = false,
                    PointsUsed = 100
                };
                context.Bookings.Add(booking3);
                await context.SaveChangesAsync();

                var p4 = new Passenger { FullName = "Jane Doe", PassportNumber = "C4567890", Age = 28, BookingId = booking3.BookingId, FlightId = flight3.FlightId, ClassId = economy?.ClassId ?? 1 };
                context.Passengers.Add(p4);
                await context.SaveChangesAsync();

                var payment3 = new Payment { BookingId = booking3.BookingId, Amount = 550, PayMethod = "CreditCard", PayStatus = "Refunded", PayDate = DateTime.Now.AddDays(-4), TransactionRef = Guid.NewGuid().ToString() };
                context.Payments.Add(payment3);
                await context.SaveChangesAsync();
            }

            // ============================================================
            // BOOKING 4: Admin - Completed (AF404)
            // ============================================================
            if (adminUser != null && flight4 != null)
            {
                var booking4 = new Booking
                {
                    UserId = adminUser.UserId,
                    FlightId = flight4.FlightId,
                    BookingDate = DateTime.Now.AddDays(-10),
                    Status = "Completed",
                    TotalPrice = 500,
                    DiscountApplied = true,
                    PointsUsed = 200
                };
                context.Bookings.Add(booking4);
                await context.SaveChangesAsync();

                var p5 = new Passenger { FullName = "Khaled Hassan", PassportNumber = "D2468135", Age = 45, BookingId = booking4.BookingId, FlightId = flight4.FlightId, ClassId = economy?.ClassId ?? 1 };
                context.Passengers.Add(p5);
                await context.SaveChangesAsync();

                var payment4 = new Payment { BookingId = booking4.BookingId, Amount = 400, PayMethod = "CreditCard", PayStatus = "Completed", PayDate = DateTime.Now.AddDays(-10), TransactionRef = Guid.NewGuid().ToString() };
                context.Payments.Add(payment4);
                await context.SaveChangesAsync();
            }

            // ============================================================
            // BOOKING 5: John Doe - Confirmed (AF505)
            // ============================================================
            if (sampleUser != null && flight5 != null)
            {
                var booking5 = new Booking
                {
                    UserId = sampleUser.UserId,
                    FlightId = flight5.FlightId,
                    BookingDate = DateTime.Now.AddDays(-3),
                    Status = "Confirmed",
                    TotalPrice = 800,
                    DiscountApplied = false,
                    PointsUsed = 0
                };
                context.Bookings.Add(booking5);
                await context.SaveChangesAsync();

                var p6 = new Passenger { FullName = "John Doe", PassportNumber = "B9876543", Age = 40, BookingId = booking5.BookingId, FlightId = flight5.FlightId, ClassId = economy?.ClassId ?? 1 };
                var p7 = new Passenger { FullName = "Emily Doe", PassportNumber = "E1357924", Age = 34, BookingId = booking5.BookingId, FlightId = flight5.FlightId, ClassId = economy?.ClassId ?? 1 };
                context.Passengers.AddRange(p6, p7);
                await context.SaveChangesAsync();

                var payment5 = new Payment { BookingId = booking5.BookingId, Amount = 800, PayMethod = "CreditCard", PayStatus = "Completed", PayDate = DateTime.Now.AddDays(-3), TransactionRef = Guid.NewGuid().ToString() };
                context.Payments.Add(payment5);
                await context.SaveChangesAsync();
            }
        }
    }

    private static bool IsStrongBootstrapPassword(string? password) =>
        password is { Length: >= 14 } &&
        password.Any(char.IsUpper) &&
        password.Any(char.IsLower) &&
        password.Any(char.IsDigit) &&
        password.Any(c => !char.IsLetterOrDigit(c));
}
