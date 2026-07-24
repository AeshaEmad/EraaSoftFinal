using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AeroFly.Web.Data;
using AeroFly.Web.Models;
using AeroFly.Web.ViewModels;
using AeroFly.Web.Services;

namespace AeroFly.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOperations")]
public class FlightController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IPricingService _pricingService;

    public FlightController(ApplicationDbContext context, IPricingService pricingService)
    {
        _context = context;
        _pricingService = pricingService;
    }

     
    // 1. INDEX - List all Flights with filters
     
    public async Task<IActionResult> Index(
        string? searchTerm,
        string? status,
        int? departureAirportId,
        int? arrivalAirportId,
        DateTime? fromDate,
        DateTime? toDate)
    {
        ViewData["Title"] = "Flights Management";

        var query = _context.Flights
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(searchTerm))
        {
            searchTerm = searchTerm.ToLower();
            query = query.Where(f =>
                f.FlightNum.ToLower().Contains(searchTerm) ||
                f.DepartureAirport.City.ToLower().Contains(searchTerm) ||
                f.ArrivalAirport.City.ToLower().Contains(searchTerm) ||
                f.DepartureAirport.IataCode.ToLower().Contains(searchTerm) ||
                f.ArrivalAirport.IataCode.ToLower().Contains(searchTerm));
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(f => f.Status == status);
        }

        if (departureAirportId.HasValue)
        {
            query = query.Where(f => f.DepartureAirportId == departureAirportId.Value);
        }

        if (arrivalAirportId.HasValue)
        {
            query = query.Where(f => f.ArrivalAirportId == arrivalAirportId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(f => f.DepartureTime.Date >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(f => f.DepartureTime.Date <= toDate.Value.Date);
        }

        var flights = await query
            .OrderBy(f => f.DepartureTime)
            .Select(f => new FlightVM
            {
                FlightId = f.FlightId,
                FlightNum = f.FlightNum,
                DepartureTime = f.DepartureTime,
                ArrivalTime = f.ArrivalTime,
                BasePrice = f.BasePrice,
                Status = f.Status,
                DepartureAirportId = f.DepartureAirportId,
                ArrivalAirportId = f.ArrivalAirportId,
                AvailableSeats = f.AvailableSeats,
                DepartureAirportName = f.DepartureAirport.Name,
                ArrivalAirportName = f.ArrivalAirport.Name,
                DepartureIataCode = f.DepartureAirport.IataCode,
                ArrivalIataCode = f.ArrivalAirport.IataCode
            })
            .ToListAsync();

        // Get filter data for dropdowns
        ViewBag.Airports = await _context.Airports
            .OrderBy(a => a.City)
            .Select(a => new { a.AirportId, a.Name, a.City, a.IataCode })
            .ToListAsync();

        ViewBag.Statuses = new List<string> { "Scheduled", "Delayed", "Cancelled", "Completed" };

        return View(flights);
    }


    // 2. CREATE - GET
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "Add New Flight";

        var seatClasses = await _context.SeatClasses.ToListAsync();

        var model = new FlightCreateVM
        {
            DepartureTime = DateTime.Now.AddHours(2),
            ArrivalTime = DateTime.Now.AddHours(4),
            Airports = await GetAirportListAsync(),

            SeatClasses = seatClasses.Select(c => new SeatClassVM
            {
                ClassId = c.ClassId,
                ClassName = c.ClassName,
                ClassMultiplier = c.ClassMultiplier,
                Price = 0,
                AvailableSeats = 0
            }).ToList()
        };

        return View(model);
    }


    // 3. CREATE - POST

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FlightCreateVM model)
    {
        if (!ModelState.IsValid)
        {
            model.Airports = await GetAirportListAsync();
            ViewData["Title"] = "Add New Flight";
            return View(model);
        }

        // Validate departure time < arrival time
        if (model.DepartureTime >= model.ArrivalTime)
        {
            ModelState.AddModelError("ArrivalTime", "Arrival time must be after departure time.");
            model.Airports = await GetAirportListAsync();
            ViewData["Title"] = "Add New Flight";
            return View(model);
        }

        // Validate departure and arrival airports are different
        if (model.DepartureAirportId == model.ArrivalAirportId)
        {
            ModelState.AddModelError("ArrivalAirportId", "Departure and arrival airports must be different.");
            model.Airports = await GetAirportListAsync();
            ViewData["Title"] = "Add New Flight";
            return View(model);
        }

        var flight = new Flight
        {
            FlightNum = model.FlightNum.ToUpper(),
            DepartureTime = model.DepartureTime,
            ArrivalTime = model.ArrivalTime,
            BasePrice = model.BasePrice,
            Status = model.Status,
            DepartureAirportId = model.DepartureAirportId,
            ArrivalAirportId = model.ArrivalAirportId,
            AvailableSeats = model.AvailableSeats
        };

        await using var transaction = await _context.Database.BeginTransactionAsync();

        _context.Flights.Add(flight);
        await _context.SaveChangesAsync();
        await SynchronizeSeatClassesAsync(flight, updateAvailability: true);
        await transaction.CommitAsync();

        TempData["Success"] = $"Flight {flight.FlightNum} has been created successfully! ✈️";
        return RedirectToAction(nameof(Index));
    }

     
    // 4. EDIT - GET
     
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        ViewData["Title"] = "Edit Flight";

        var flight = await _context.Flights
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .FirstOrDefaultAsync(f => f.FlightId == id);

        if (flight == null)
        {
            TempData["Error"] = "Flight not found!";
            return RedirectToAction(nameof(Index));
        }

        var model = new FlightCreateVM
        {
            FlightNum = flight.FlightNum,
            DepartureTime = flight.DepartureTime,
            ArrivalTime = flight.ArrivalTime,
            BasePrice = flight.BasePrice,
            Status = flight.Status,
            DepartureAirportId = flight.DepartureAirportId,
            ArrivalAirportId = flight.ArrivalAirportId,
            AvailableSeats = flight.AvailableSeats,
            Airports = await GetAirportListAsync()
        };

        ViewBag.FlightId = id;
        return View(model);
    }

     
    // 5. EDIT - POST
     
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FlightCreateVM model)
    {
        if (!ModelState.IsValid)
        {
            model.Airports = await GetAirportListAsync();
            ViewBag.FlightId = id;
            ViewData["Title"] = "Edit Flight";
            return View(model);
        }

        var flight = await _context.Flights
            .FirstOrDefaultAsync(f => f.FlightId == id);

        if (flight == null)
        {
            TempData["Error"] = "Flight not found!";
            return RedirectToAction(nameof(Index));
        }

        // Validate departure time < arrival time
        if (model.DepartureTime >= model.ArrivalTime)
        {
            ModelState.AddModelError("ArrivalTime", "Arrival time must be after departure time.");
            model.Airports = await GetAirportListAsync();
            ViewBag.FlightId = id;
            ViewData["Title"] = "Edit Flight";
            return View(model);
        }

        // Validate departure and arrival airports are different
        if (model.DepartureAirportId == model.ArrivalAirportId)
        {
            ModelState.AddModelError("ArrivalAirportId", "Departure and arrival airports must be different.");
            model.Airports = await GetAirportListAsync();
            ViewBag.FlightId = id;
            ViewData["Title"] = "Edit Flight";
            return View(model);
        }

        var availableSeatsChanged = flight.AvailableSeats != model.AvailableSeats;
        if (availableSeatsChanged && await _context.Bookings.AnyAsync(
                b => b.FlightId == id &&
                     (b.SeatsReserved || b.Status == "Confirmed" || b.Status == "Completed")))
        {
            ModelState.AddModelError(
                nameof(model.AvailableSeats),
                "Seat capacity cannot be changed after seats have been reserved or confirmed.");
            model.Airports = await GetAirportListAsync();
            ViewBag.FlightId = id;
            return View(model);
        }

        flight.FlightNum = model.FlightNum.ToUpper();
        flight.DepartureTime = model.DepartureTime;
        flight.ArrivalTime = model.ArrivalTime;
        flight.BasePrice = model.BasePrice;
        flight.Status = model.Status;
        flight.DepartureAirportId = model.DepartureAirportId;
        flight.ArrivalAirportId = model.ArrivalAirportId;
        flight.AvailableSeats = model.AvailableSeats;

        await using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.SaveChangesAsync();
        await SynchronizeSeatClassesAsync(flight, availableSeatsChanged);
        await transaction.CommitAsync();

        TempData["Success"] = $"Flight {flight.FlightNum} has been updated successfully! ✈️";
        return RedirectToAction(nameof(Index));
    }

     
    // 6. DELETE - GET (Confirmation)
     
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        ViewData["Title"] = "Delete Flight";

        var flight = await _context.Flights
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .FirstOrDefaultAsync(f => f.FlightId == id);

        if (flight == null)
        {
            TempData["Error"] = "Flight not found!";
            return RedirectToAction(nameof(Index));
        }

        var model = new FlightVM
        {
            FlightId = flight.FlightId,
            FlightNum = flight.FlightNum,
            DepartureTime = flight.DepartureTime,
            ArrivalTime = flight.ArrivalTime,
            BasePrice = flight.BasePrice,
            Status = flight.Status,
            DepartureAirportId = flight.DepartureAirportId,
            ArrivalAirportId = flight.ArrivalAirportId,
            AvailableSeats = flight.AvailableSeats,
            DepartureAirportName = flight.DepartureAirport.Name,
            ArrivalAirportName = flight.ArrivalAirport.Name,
            DepartureIataCode = flight.DepartureAirport.IataCode,
            ArrivalIataCode = flight.ArrivalAirport.IataCode
        };

        return View(model);
    }

     
    // 7. DELETE - POST (Confirm)
     
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var flight = await _context.Flights
            .Include(f => f.FlightSeatClasses)
            .Include(f => f.FlightRules)
            .FirstOrDefaultAsync(f => f.FlightId == id);

        if (flight == null)
        {
            TempData["Error"] = "Flight not found!";
            return RedirectToAction(nameof(Index));
        }

        // Check if flight has bookings
        var hasBookings = await _context.Bookings
            .AnyAsync(b => b.FlightId == id);

        if (hasBookings)
        {
            TempData["Error"] = $"Cannot delete flight '{flight.FlightNum}' because it has bookings!";
            return RedirectToAction(nameof(Index));
        }

        var flightNum = flight.FlightNum;

        // Remove related FlightSeatClasses
        if (flight.FlightSeatClasses.Any())
        {
            _context.FlightSeatClasses.RemoveRange(flight.FlightSeatClasses);
        }

        // Remove related FlightRules
        if (flight.FlightRules.Any())
        {
            _context.FlightRules.RemoveRange(flight.FlightRules);
        }

        _context.Flights.Remove(flight);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Flight {flightNum} has been deleted successfully! 🗑️";
        return RedirectToAction(nameof(Index));
    }

     
    // Helper Methods
     
    private async Task<List<AirportVM>> GetAirportListAsync()
    {
        return await _context.Airports
            .OrderBy(a => a.City)
            .Select(a => new AirportVM
            {
                AirportId = a.AirportId,
                Name = a.Name,
                City = a.City,
                Country = a.Country,
                IataCode = a.IataCode
            })
            .ToListAsync();
    }

    private async Task SynchronizeSeatClassesAsync(Flight flight, bool updateAvailability)
    {
        var departureAirport = await _context.Airports.FindAsync(flight.DepartureAirportId);
        var arrivalAirport = await _context.Airports.FindAsync(flight.ArrivalAirportId);
        var seatClasses = (await _context.SeatClasses.ToListAsync())
            .OrderBy(s => s.ClassMultiplier)
            .ToList();
        var existingClasses = await _context.FlightSeatClasses
            .Where(fsc => fsc.FlightId == flight.FlightId)
            .ToListAsync();

        if (departureAirport == null || arrivalAirport == null || !seatClasses.Any())
        {
            return;
        }

        var seatsPerClass = flight.AvailableSeats / seatClasses.Count;
        var remainingSeats = flight.AvailableSeats % seatClasses.Count;

        for (var index = 0; index < seatClasses.Count; index++)
        {
            var seatClass = seatClasses[index];
            var flightSeatClass = existingClasses.FirstOrDefault(fsc => fsc.ClassId == seatClass.ClassId);

            if (flightSeatClass == null)
            {
                flightSeatClass = new FlightSeatClass
                {
                    FlightId = flight.FlightId,
                    ClassId = seatClass.ClassId
                };
                _context.FlightSeatClasses.Add(flightSeatClass);
                flightSeatClass.AvailableSeats = seatsPerClass + (index < remainingSeats ? 1 : 0);
            }
            else if (updateAvailability)
            {
                flightSeatClass.AvailableSeats = seatsPerClass + (index < remainingSeats ? 1 : 0);
            }
            flightSeatClass.FinalPrice = _pricingService.CalculateFinalPrice(
                flight.BasePrice,
                departureAirport,
                arrivalAirport,
                seatClass.ClassMultiplier);
        }

        await _context.SaveChangesAsync();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var flight = await _context.Flights.FindAsync(id);
        if (flight == null)
        {
            return Json(new { success = false, message = "Flight not found" });
        }

        var statuses = new[] { "Scheduled", "Delayed", "Cancelled", "Completed" };
        var currentIndex = Array.IndexOf(statuses, flight.Status);
        var nextIndex = (currentIndex + 1) % statuses.Length;
        flight.Status = statuses[nextIndex];

        await _context.SaveChangesAsync();

        return Json(new { success = true, newStatus = flight.Status });
    }
}
