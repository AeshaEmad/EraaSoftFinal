
using AeroFly.Web.Data;
using AeroFly.Web.Models;
using AeroFly.Web.Services;
using AeroFly.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace AeroFly.Web.Areas.User.Controllers;

[Area("User")]
[Authorize]
public class BookingController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IStripeService _stripeService;
    private readonly IBookingWorkflowService _bookingWorkflow;
    private readonly IConfiguration _config; 

    public BookingController(
        ApplicationDbContext context,
        IEmailService emailService,
        IStripeService stripeService,
        IBookingWorkflowService bookingWorkflow,
        IConfiguration config) 
    {
        _context = context;
        _emailService = emailService;
        _stripeService = stripeService;
        _bookingWorkflow = bookingWorkflow;
        _config = config; 
    }

    
    // 1. CREATE - Step 1: Select Flight & Enter Passenger Details
   
    [HttpGet]
    public async Task<IActionResult> Create(int flightId, int classId, int passengers)
    {
        if (passengers < 1 || passengers > 10)
        {
            TempData["Error"] = "Passengers must be between 1 and 10.";
            return RedirectToAction("Details", "Home", new { id = flightId, passengers = 1 });
        }

        var flight = await _context.Flights
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .Include(f => f.FlightSeatClasses)
                .ThenInclude(fsc => fsc.SeatClass)
            .FirstOrDefaultAsync(f => f.FlightId == flightId);

        if (flight == null)
        {
            TempData["Error"] = "Flight not found!";
            return RedirectToAction("Index", "Home");
        }

        if ((flight.Status != "Scheduled" && flight.Status != "Delayed") || flight.DepartureTime <= DateTime.Now)
        {
            TempData["Error"] = "This flight is not available for booking.";
            return RedirectToAction("Index", "Home");
        }

        var seatClass = flight.FlightSeatClasses
            .FirstOrDefault(fsc => fsc.ClassId == classId);

        if (seatClass == null || seatClass.AvailableSeats < passengers)
        {
            TempData["Error"] = "Not enough seats available!";
            return RedirectToAction("Details", "Home", new { id = flightId, passengers });
        }

        var model = new BookingCreateVM
        {
            FlightId = flightId,
            ClassId = classId,
            PassengerCount = passengers,
            FlightNumber = flight.FlightNum,
            Route = $"{flight.DepartureAirport.City} ({flight.DepartureAirport.IataCode}) → {flight.ArrivalAirport.City} ({flight.ArrivalAirport.IataCode})",
            DepartureTime = flight.DepartureTime,
            ArrivalTime = flight.ArrivalTime,
            PricePerPassenger = seatClass.FinalPrice,
            SeatClassName = seatClass.SeatClass.ClassName,
            Passengers = Enumerable.Range(0, passengers)
                .Select(i => new PassengerDetailsVM())
                .ToList()
        };

        return View(model);
    }


    // 2. CREATE - Step 2: Review & Confirm
   
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BookingCreateVM model)
    {
        if (model.Passengers.Count != model.PassengerCount)
        {
            ModelState.AddModelError("Passengers", "Passenger details must match the selected passenger count.");
        }

        if (!ModelState.IsValid)
        {
            // Reload flight details
            var flight = await _context.Flights
                .Include(f => f.DepartureAirport)
                .Include(f => f.ArrivalAirport)
                .Include(f => f.FlightSeatClasses)
                    .ThenInclude(fsc => fsc.SeatClass)
                .FirstOrDefaultAsync(f => f.FlightId == model.FlightId);

            if (flight != null)
            {
                var seatClass = flight.FlightSeatClasses
                    .FirstOrDefault(fsc => fsc.ClassId == model.ClassId);

                model.FlightNumber = flight.FlightNum;
                model.Route = $"{flight.DepartureAirport.City} ({flight.DepartureAirport.IataCode}) → {flight.ArrivalAirport.City} ({flight.ArrivalAirport.IataCode})";
                model.DepartureTime = flight.DepartureTime;
                model.ArrivalTime = flight.ArrivalTime;
                model.PricePerPassenger = seatClass?.FinalPrice ?? 0;
                model.SeatClassName = seatClass?.SeatClass.ClassName ?? "Economy";
            }

            return View(model);
        }

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        var flightEntity = await _context.Flights
            .Include(f => f.FlightSeatClasses)
            .FirstOrDefaultAsync(f => f.FlightId == model.FlightId);

        if (flightEntity == null)
        {
            TempData["Error"] = "Flight not found!";
            return RedirectToAction("Index", "Home");
        }

        if ((flightEntity.Status != "Scheduled" && flightEntity.Status != "Delayed") ||
            flightEntity.DepartureTime <= DateTime.Now)
        {
            TempData["Error"] = "This flight is not available for booking.";
            return RedirectToAction("Index", "Home");
        }

        var seatClassEntity = flightEntity.FlightSeatClasses
            .FirstOrDefault(fsc => fsc.ClassId == model.ClassId);

        if (seatClassEntity == null || seatClassEntity.AvailableSeats < model.PassengerCount)
        {
            TempData["Error"] = "Not enough seats available!";
            return RedirectToAction("Details", "Home", new { id = model.FlightId, passengers = model.PassengerCount });
        }

        // Create Booking
        var booking = new Booking
        {
            UserId = userId,
            FlightId = model.FlightId,
            BookingDate = DateTime.Now,
            Status = "Pending",
            TotalPrice = (seatClassEntity.FinalPrice * model.PassengerCount),
            DiscountApplied = false,
            PointsUsed = 0,
            SeatsReserved = true,
            SeatHoldExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };

        seatClassEntity.AvailableSeats -= model.PassengerCount;
        flightEntity.AvailableSeats = flightEntity.FlightSeatClasses.Sum(f => f.AvailableSeats);

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        // Create Passengers
        var passengers = new List<Passenger>();
        foreach (var p in model.Passengers)
        {
            passengers.Add(new Passenger
            {
                FullName = p.FullName,
                PassportNumber = p.PassportNumber,
                Age = p.Age,
                BookingId = booking.BookingId,
                FlightId = model.FlightId,
                ClassId = model.ClassId
            });
        }

        _context.Passengers.AddRange(passengers);
        await _context.SaveChangesAsync();

        // Create Payment (pending)
        var payment = new Payment
        {
            BookingId = booking.BookingId,
            Amount = booking.TotalPrice,
            PayMethod = "CreditCard",
            PayStatus = "Pending",
            PayDate = DateTime.Now,
            TransactionRef = Guid.NewGuid().ToString()
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        // Redirect to Payment
        return RedirectToAction("Payment", new { bookingId = booking.BookingId });
    }

    
    // 3. PAYMENT - Step 3: Payment (GET) - With Stripe
    
    [HttpGet]
    public async Task<IActionResult> Payment(int bookingId)
    {
        await _bookingWorkflow.ReleaseExpiredHoldAsync(bookingId);
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var booking = await _context.Bookings
            .Include(b => b.Flight)
            .Include(b => b.Passengers)
                .ThenInclude(p => p.FlightSeatClass)
                    .ThenInclude(fsc => fsc.SeatClass)
            .Include(b => b.Passengers)
                .ThenInclude(p => p.Ticket)
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);

        if (booking == null)
        {
            TempData["Error"] = "Booking not found!";
            return RedirectToAction("Index", "Home");
        }

        //  Create Stripe Payment Intent
        if (booking.Status != "Pending" || booking.Payment?.PayStatus == "Completed")
        {
            return RedirectToAction("Confirmation", new { bookingId });
        }

        Stripe.PaymentIntent? paymentIntent = null;
        if (booking.Payment != null && booking.Payment.TransactionRef.StartsWith("pi_", StringComparison.Ordinal))
        {
            var existingIntent = await _stripeService.GetPaymentIntentAsync(booking.Payment.TransactionRef);
            var matchesBooking = existingIntent.Metadata.TryGetValue("booking_id", out var intentBookingId) &&
                                 intentBookingId == booking.BookingId.ToString() &&
                                 existingIntent.Metadata.TryGetValue("user_id", out var intentUserId) &&
                                 intentUserId == booking.UserId.ToString() &&
                                 existingIntent.Amount == (long)(booking.TotalPrice * 100) &&
                                 string.Equals(existingIntent.Currency, "usd", StringComparison.OrdinalIgnoreCase) &&
                                 existingIntent.Status != "canceled";

            if (matchesBooking)
            {
                if (existingIntent.Status == "succeeded")
                {
                    await _bookingWorkflow.ConfirmPaidBookingAsync(
                        booking.BookingId,
                        existingIntent.Id,
                        existingIntent.Amount / 100m);
                    return RedirectToAction("Confirmation", new { bookingId });
                }
                paymentIntent = existingIntent;
            }
        }

        if (paymentIntent == null)
        {
            paymentIntent = await _stripeService.CreatePaymentIntentAsync(
                booking.TotalPrice,
                booking.BookingId,
                booking.UserId);

            if (booking.Payment != null)
            {
                booking.Payment.TransactionRef = paymentIntent.Id;
                await _context.SaveChangesAsync();
            }
        }

        var model = new PaymentVM
        {
            BookingId = bookingId,
            Amount = booking.TotalPrice,
            AvailablePoints = await _context.RewardAccounts
                .Where(r => r.UserId == userId)
                .Select(r => r.PointsBalance)
                .FirstOrDefaultAsync(),
            StripeClientSecret = paymentIntent.ClientSecret,
            StripePublishableKey = _config["Stripe:PublishableKey"]
        };

        ViewBag.Booking = booking;
        return View(model);
    }

    
    // 4. PAYMENT - Legacy POST route (secure Stripe flow is required)
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Payment(PaymentVM model)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var bookingExists = await _context.Bookings
            .AnyAsync(b => b.BookingId == model.BookingId && b.UserId == userId);

        if (!bookingExists)
        {
            TempData["Error"] = "Booking not found!";
            return RedirectToAction("Index", "Home");
        }

        TempData["Error"] = "Payment must be completed through the secure Stripe payment form.";
        return RedirectToAction("Payment", new { bookingId = model.BookingId });
    }

    [HttpGet]
    public async Task<IActionResult> PaymentSuccess(string paymentIntentId, int bookingId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Verify payment with Stripe
        var paymentIntent = await _stripeService.GetPaymentIntentAsync(paymentIntentId);

        var hasValidMetadata = paymentIntent.Metadata.TryGetValue("booking_id", out var metadataBookingId) &&
                               metadataBookingId == bookingId.ToString() &&
                               paymentIntent.Metadata.TryGetValue("user_id", out var metadataUserId) &&
                               metadataUserId == userId.ToString();

        var bookingAmount = await _context.Bookings
            .Where(b => b.BookingId == bookingId && b.UserId == userId)
            .Select(b => (decimal?)b.TotalPrice)
            .FirstOrDefaultAsync();

        if (paymentIntent.Status != "succeeded" ||
            !hasValidMetadata ||
            !bookingAmount.HasValue ||
            paymentIntent.Amount != (long)(bookingAmount.Value * 100) ||
            !string.Equals(paymentIntent.Currency, "usd", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Payment verification failed. Please try again.";
            return RedirectToAction("Payment", new { bookingId });
        }

        var result = await _bookingWorkflow.ConfirmPaidBookingAsync(
            bookingId,
            paymentIntentId,
            bookingAmount.Value);

        if (!result.Success)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction("Payment", new { bookingId });
        }

        if (result.Booking != null && result.Changed)
        {
            await SendConfirmationEmailAsync(result.Booking);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction("Confirmation", new { bookingId });
    }

    // 5. CONFIRMATION - Booking Confirmation
    [HttpGet]
    public async Task<IActionResult> Confirmation(int bookingId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var booking = await _context.Bookings
            .Include(b => b.User)
            .Include(b => b.Flight)
                .ThenInclude(f => f.DepartureAirport)
            .Include(b => b.Flight)
                .ThenInclude(f => f.ArrivalAirport)
            .Include(b => b.Passengers)
                .ThenInclude(p => p.FlightSeatClass)
                    .ThenInclude(fsc => fsc.SeatClass)
            .Include(b => b.Passengers)
                .ThenInclude(p => p.Ticket)
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);

        if (booking == null)
        {
            TempData["Error"] = "Booking not found!";
            return RedirectToAction("Index", "Home");
        }

        var model = new BookingConfirmationVM
        {
            BookingId = booking.BookingId,
            FlightNumber = booking.Flight.FlightNum,
            Route = $"{booking.Flight.DepartureAirport.City} ({booking.Flight.DepartureAirport.IataCode}) → {booking.Flight.ArrivalAirport.City} ({booking.Flight.ArrivalAirport.IataCode})",
            DepartureTime = booking.Flight.DepartureTime,
            ArrivalTime = booking.Flight.ArrivalTime,
            DepartureCity = booking.Flight.DepartureAirport.City,
            DepartureIata = booking.Flight.DepartureAirport.IataCode,
            ArrivalCity = booking.Flight.ArrivalAirport.City,
            ArrivalIata = booking.Flight.ArrivalAirport.IataCode,
            PassengerCount = booking.Passengers.Count,
            TotalPrice = booking.TotalPrice,
            SeatClass = booking.Passengers.FirstOrDefault()?.FlightSeatClass.SeatClass.ClassName ?? "Economy",
            Status = booking.Status,
            BookingDate = booking.BookingDate,
            Passengers = booking.Passengers.Select(p => new PassengerDetailsVM
            {
                FullName = p.FullName,
                PassportNumber = p.PassportNumber,
                Age = p.Age,
                SeatNumber = p.Ticket?.SeatNum,
                TicketToken = p.Ticket?.QrCode
            }).ToList()
        };

        return View(model);
    }
    // 6. MY BOOKINGS - User's booking history
    [HttpGet]
    public async Task<IActionResult> MyBookings()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var bookings = await _context.Bookings
            .Include(b => b.User)
            .Include(b => b.Flight)
                .ThenInclude(f => f.DepartureAirport)
            .Include(b => b.Flight)
                .ThenInclude(f => f.ArrivalAirport)
            .Include(b => b.Passengers)
                .ThenInclude(p => p.FlightSeatClass)
                    .ThenInclude(fsc => fsc.SeatClass)
            .Include(b => b.Payment)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.BookingDate)
            .Select(b => new BookingListVM
            {
                BookingId = b.BookingId,
                PassengerName = b.User.FName + " " + b.User.LName,
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

        return View(bookings);
    }

    
    // 7. CANCEL - Cancel booking
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var booking = await _context.Bookings
            .Include(b => b.Flight)
            .FirstOrDefaultAsync(b => b.BookingId == id && b.UserId == userId);

        if (booking == null)
        {
            TempData["Error"] = "Booking not found!";
            return RedirectToAction("MyBookings");
        }

        if (booking.Status == "Completed")
        {
            TempData["Error"] = "Cannot cancel a completed booking.";
            return RedirectToAction("MyBookings");
        }

        // Check if flight already departed
        if (booking.Flight.DepartureTime < DateTime.Now)
        {
            TempData["Error"] = "Cannot cancel a booking for a flight that has already departed.";
            return RedirectToAction("MyBookings");
        }

        var result = await _bookingWorkflow.CancelAndRefundAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction("MyBookings");
    }
    // 8. DOWNLOAD TICKET - Generate PDF ticket
    public async Task<IActionResult> DownloadTicket(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var booking = await _context.Bookings
            .Include(b => b.User)
            .Include(b => b.Flight)
                .ThenInclude(f => f.DepartureAirport)
            .Include(b => b.Flight)
                .ThenInclude(f => f.ArrivalAirport)
            .Include(b => b.Passengers)
                .ThenInclude(p => p.FlightSeatClass)
                    .ThenInclude(fsc => fsc.SeatClass)
            .Include(b => b.Passengers)
                .ThenInclude(p => p.Ticket)
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.BookingId == id && b.UserId == userId);

        if (booking == null)
        {
            TempData["Error"] = "Booking not found!";
            return RedirectToAction("MyBookings");
        }

        // Check if booking is confirmed
        if (booking.Status != "Confirmed" && booking.Status != "Completed")
        {
            TempData["Error"] = "Ticket is only available for confirmed bookings.";
            return RedirectToAction("MyBookings");
        }

        var model = new BookingConfirmationVM
        {
            BookingId = booking.BookingId,
            FlightNumber = booking.Flight.FlightNum,
            Route = $"{booking.Flight.DepartureAirport.City} ({booking.Flight.DepartureAirport.IataCode}) → {booking.Flight.ArrivalAirport.City} ({booking.Flight.ArrivalAirport.IataCode})",
            DepartureTime = booking.Flight.DepartureTime,
            ArrivalTime = booking.Flight.ArrivalTime,
            DepartureCity = booking.Flight.DepartureAirport.City,
            DepartureIata = booking.Flight.DepartureAirport.IataCode,
            ArrivalCity = booking.Flight.ArrivalAirport.City,
            ArrivalIata = booking.Flight.ArrivalAirport.IataCode,
            PassengerCount = booking.Passengers.Count,
            TotalPrice = booking.TotalPrice,
            SeatClass = booking.Passengers.FirstOrDefault()?.FlightSeatClass.SeatClass.ClassName ?? "Economy",
            Status = booking.Status,
            BookingDate = booking.BookingDate,
            Passengers = booking.Passengers.Select(p => new PassengerDetailsVM
            {
                FullName = p.FullName,
                PassportNumber = p.PassportNumber,
                Age = p.Age,
                SeatNumber = p.Ticket?.SeatNum,
                TicketToken = p.Ticket?.QrCode
            }).ToList()
        };

        return new ViewAsPdf("Ticket", model)
        {
            FileName = $"Ticket_{model.PNR}.pdf",
            PageSize = Rotativa.AspNetCore.Options.Size.A4,
            PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
            CustomSwitches = "--margin-top 10 --margin-bottom 10 --margin-left 10 --margin-right 10"
        };
    }

    // Helper Methods
    private string GetRandomLetter()
    {
        var letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var random = new Random();
        return letters[random.Next(letters.Length)].ToString();
    }

    private async Task<(bool Success, bool WasConfirmed, string Message, Booking? Booking)> ConfirmBookingAsync(
        int bookingId,
        int userId,
        string paymentMethod,
        string transactionReference,
        decimal paidAmount)
    {
        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        var booking = await _context.Bookings
            .Include(b => b.Payment)
            .Include(b => b.User)
            .Include(b => b.Flight)
            .Include(b => b.Passengers)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);

        if (booking == null)
        {
            return (false, false, "Booking not found.", null);
        }

        if (booking.Status == "Confirmed" || booking.Status == "Completed")
        {
            return (true, false, "This booking is already confirmed.", booking);
        }

        if (booking.Status != "Pending" || booking.Payment == null)
        {
            return (false, false, "This booking cannot be confirmed.", booking);
        }

        if (paidAmount != booking.TotalPrice)
        {
            return (false, false, "The paid amount does not match the booking total.", booking);
        }

        if (!booking.Passengers.Any() || booking.Flight.AvailableSeats < booking.Passengers.Count)
        {
            return (false, false, "Not enough seats are available to confirm this booking.", booking);
        }

        foreach (var passengerGroup in booking.Passengers.GroupBy(p => p.ClassId))
        {
            var flightSeatClass = await _context.FlightSeatClasses
                .FirstOrDefaultAsync(fsc =>
                    fsc.FlightId == booking.FlightId && fsc.ClassId == passengerGroup.Key);

            if (flightSeatClass == null || flightSeatClass.AvailableSeats < passengerGroup.Count())
            {
                return (false, false, "Not enough seats are available in the selected class.", booking);
            }

            flightSeatClass.AvailableSeats -= passengerGroup.Count();
        }

        booking.Flight.AvailableSeats -= booking.Passengers.Count;
        booking.Status = "Confirmed";
        booking.Payment.Amount = booking.TotalPrice;
        booking.Payment.PayStatus = "Completed";
        booking.Payment.PayMethod = paymentMethod;
        booking.Payment.PayDate = DateTime.Now;
        booking.Payment.TransactionRef = transactionReference;

        await _context.SaveChangesAsync();
        await CreateTicketsAsync(booking.BookingId);
        await AddRewardPoints(booking.UserId, booking.BookingId, booking.TotalPrice);
        await transaction.CommitAsync();

        return (true, true, "Payment successful! Your booking has been confirmed. ✈️", booking);
    }

    private async Task AddRewardPoints(int userId, int bookingId, decimal amount)
    {
        var pointsAlreadyAdded = await _context.PointsTransactions
            .AnyAsync(t => t.BookingId == bookingId);

        if (pointsAlreadyAdded)
        {
            return;
        }

        var pointsToAdd = (int)Math.Floor(amount);

    
        var rewardAccount = await _context.RewardAccounts
            .FirstOrDefaultAsync(r => r.UserId == userId);

        if (rewardAccount == null)
        {
            rewardAccount = new RewardAccount
            {
                UserId = userId,
                PointsBalance = 0
            };
            _context.RewardAccounts.Add(rewardAccount);
            await _context.SaveChangesAsync(); // 
        }

      
        rewardAccount.PointsBalance += pointsToAdd;

       
        var transaction = new PointsTransaction
        {
            AccountId = rewardAccount.AccountId, 
            Points = pointsToAdd,
            Type = "Earned",
            Date = DateTime.Now,
            Description = $"Earned {pointsToAdd} points from booking AF{bookingId:D6}",
            BookingId = bookingId
        };

        _context.PointsTransactions.Add(transaction);
        await _context.SaveChangesAsync();
    }

    // Helper: Create Tickets
    private async Task CreateTicketsAsync(int bookingId)
    {
        
        var passengers = await _context.Passengers
            .Where(p => p.BookingId == bookingId)
            .ToListAsync();

        if (!passengers.Any())
            return;

       
        var existingTickets = await _context.Tickets
            .Where(t => t.BookingId == bookingId)
            .ToListAsync();

        var tickets = new List<Ticket>();
        int seatCounter = existingTickets.Count + 1;

        foreach (var passenger in passengers)
        {
            
            var exists = existingTickets.Any(t => t.PassengerId == passenger.PassengerId);

            if (!exists)
            {
                tickets.Add(new Ticket
                {
                    BookingId = bookingId,
                    FlightId = passenger.FlightId,
                    PassengerId = passenger.PassengerId,
                    IssueDate = DateTime.Now,
                    SeatNum = $"{seatCounter:D2}{GetRandomLetter()}",
                    QrCode = Guid.NewGuid().ToString()
                });
                seatCounter++;
            }
        }

        if (tickets.Any())
        {
            _context.Tickets.AddRange(tickets);
            await _context.SaveChangesAsync();
        }
    }
    // Helper: Send Confirmation Email
    private async Task SendConfirmationEmailAsync(Booking booking)
    {
       
        var fullBooking = await _context.Bookings
            .Include(b => b.User)
            .Include(b => b.Flight)
                .ThenInclude(f => f.DepartureAirport)
            .Include(b => b.Flight)
                .ThenInclude(f => f.ArrivalAirport)
            .Include(b => b.Passengers)
                .ThenInclude(p => p.FlightSeatClass)
                    .ThenInclude(fsc => fsc.SeatClass)
            .FirstOrDefaultAsync(b => b.BookingId == booking.BookingId);

        if (fullBooking == null)
        {
            return;
        }

        var confirmationModel = new BookingConfirmationVM
        {
            BookingId = fullBooking.BookingId,
            FlightNumber = fullBooking.Flight.FlightNum,
            Route = $"{fullBooking.Flight.DepartureAirport.City} ({fullBooking.Flight.DepartureAirport.IataCode}) → {fullBooking.Flight.ArrivalAirport.City} ({fullBooking.Flight.ArrivalAirport.IataCode})",
            DepartureTime = fullBooking.Flight.DepartureTime,
            ArrivalTime = fullBooking.Flight.ArrivalTime,
            DepartureCity = fullBooking.Flight.DepartureAirport.City,
            DepartureIata = fullBooking.Flight.DepartureAirport.IataCode,
            ArrivalCity = fullBooking.Flight.ArrivalAirport.City,
            ArrivalIata = fullBooking.Flight.ArrivalAirport.IataCode,
            PassengerCount = fullBooking.Passengers.Count,
            TotalPrice = fullBooking.TotalPrice,
            SeatClass = fullBooking.Passengers.FirstOrDefault()?.FlightSeatClass.SeatClass.ClassName ?? "Economy",
            Status = fullBooking.Status,
            BookingDate = fullBooking.BookingDate,
            Passengers = fullBooking.Passengers.Select(p => new PassengerDetailsVM
            {
                FullName = p.FullName,
                PassportNumber = p.PassportNumber,
                Age = p.Age
            }).ToList()
        };

        await _emailService.SendBookingConfirmationEmailAsync(
            fullBooking.User.Email,
            $"{fullBooking.User.FName} {fullBooking.User.LName}",
            confirmationModel
        );
    }
}
