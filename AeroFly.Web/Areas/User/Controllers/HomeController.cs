using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AeroFly.Web.Data;
using AeroFly.Web.ViewModels;

namespace AeroFly.Web.Areas.User.Controllers;

[Area("User")]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    // HOME PAGE - Show all available flights with search filters
    [HttpGet]
    public async Task<IActionResult> Index(
        int? departureAirportId,
        int? arrivalAirportId,
        DateTime? departureDate,
        int? seatClassId,
        int? passengers = 1)
    {
        var model = new SearchFlightVM
        {
            Airports = await _context.Airports
                .OrderBy(a => a.City)
                .Select(a => new AirportVM
                {
                    AirportId = a.AirportId,
                    Name = a.Name,
                    City = a.City,
                    Country = a.Country,
                    IataCode = a.IataCode,
                    Latitude = a.Latitude,
                    Longitude = a.Longitude
                })
                .ToListAsync(),
            DepartureAirportId = departureAirportId,
            ArrivalAirportId = arrivalAirportId,
            DepartureDate = departureDate ?? DateTime.Now.AddDays(1),
            SeatClassId = seatClassId,
            PassengerCount = passengers ?? 1,
            TripType = "OneWay",
            SeatClasses = (await _context.SeatClasses
                .Where(s => s.ClassName == "Economy" || s.ClassName == "Business")
                .Select(s => new SeatClassVM
                {
                    ClassId = s.ClassId,
                    ClassName = s.ClassName,
                    ClassMultiplier = s.ClassMultiplier,
                    Description = s.ClassName
                })
                .ToListAsync())
                .OrderBy(s => s.ClassMultiplier)
                .ToList()
        };

        // Build query for available flights
        var query = _context.Flights
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .Where(f => (f.Status == "Scheduled" || f.Status == "Delayed") &&
                        f.DepartureTime > DateTime.Now)
            .AsQueryable();

        // Apply filters
        if (departureAirportId.HasValue)
        {
            query = query.Where(f => f.DepartureAirportId == departureAirportId.Value);
        }

        if (arrivalAirportId.HasValue)
        {
            query = query.Where(f => f.ArrivalAirportId == arrivalAirportId.Value);
        }

        if (departureDate.HasValue)
        {
            var date = departureDate.Value.Date;
            query = query.Where(f => f.DepartureTime.Date == date);
        }

        var passengerCount = passengers ?? 1;
        query = query.Where(f => f.AvailableSeats >= passengerCount);

        if (seatClassId.HasValue)
        {
            query = query.Where(f => f.FlightSeatClasses.Any(fsc =>
                fsc.ClassId == seatClassId.Value && fsc.AvailableSeats >= passengerCount));
        }

        var results = await query
            .OrderBy(f => f.DepartureTime)
            .Select(f => new FlightResultVM
            {
                FlightId = f.FlightId,
                FlightNum = f.FlightNum,
                DepartureTime = f.DepartureTime,
                ArrivalTime = f.ArrivalTime,
                BasePrice = f.BasePrice,
                AvailableSeats = f.AvailableSeats,
                Status = f.Status,

                DepartureAirportId = f.DepartureAirportId,
                DepartureAirportName = f.DepartureAirport.Name,
                DepartureCity = f.DepartureAirport.City,
                DepartureIata = f.DepartureAirport.IataCode,
                DepartureCountry = f.DepartureAirport.Country,

                ArrivalAirportId = f.ArrivalAirportId,
                ArrivalAirportName = f.ArrivalAirport.Name,
                ArrivalCity = f.ArrivalAirport.City,
                ArrivalIata = f.ArrivalAirport.IataCode,
                ArrivalCountry = f.ArrivalAirport.Country
            })
            .ToListAsync();

        ViewData["Results"] = results;
        ViewData["PassengerCount"] = passengerCount;
        ViewData["DepartureAirportId"] = departureAirportId;
        ViewData["ArrivalAirportId"] = arrivalAirportId;
        ViewData["DepartureDate"] = departureDate;
        ViewData["SeatClassId"] = seatClassId;

        return View(model);
    }

    // SEARCH - POST (Process and redirect to GET)
    [HttpPost]
    public IActionResult Search(SearchFlightVM model)
    {
        // Build query string with proper parameters
        var queryParams = new List<string>();

        if (model.DepartureAirportId.HasValue)
            queryParams.Add($"departureAirportId={model.DepartureAirportId.Value}");

        if (model.ArrivalAirportId.HasValue)
            queryParams.Add($"arrivalAirportId={model.ArrivalAirportId.Value}");

        if (model.DepartureDate.HasValue)
            queryParams.Add($"departureDate={model.DepartureDate.Value:yyyy-MM-dd}");

        if (model.SeatClassId.HasValue)
            queryParams.Add($"seatClassId={model.SeatClassId.Value}");

        queryParams.Add($"passengers={model.PassengerCount}");

        //  Direct redirect with query parameters
        return RedirectToAction("Index", new
        {
            departureAirportId = model.DepartureAirportId,
            arrivalAirportId = model.ArrivalAirportId,
            departureDate = model.DepartureDate?.ToString("yyyy-MM-dd"),
            seatClassId = model.SeatClassId,
            passengers = model.PassengerCount
        });
    }

    // FLIGHT DETAILS
    [HttpGet]
    public async Task<IActionResult> Details(int id, int passengers = 1)
    {
        var flight = await _context.Flights
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .Include(f => f.FlightSeatClasses)
                .ThenInclude(fsc => fsc.SeatClass)
            .FirstOrDefaultAsync(f => f.FlightId == id);

        if (flight == null)
        {
            TempData["Error"] = "Flight not found!";
            return RedirectToAction("Index");
        }

        if (flight.Status != "Scheduled" && flight.Status != "Delayed")
        {
            TempData["Error"] = "This flight is not available for booking.";
            return RedirectToAction("Index");
        }

        var model = new FlightDetailsVM
        {
            FlightId = flight.FlightId,
            FlightNum = flight.FlightNum,
            DepartureTime = flight.DepartureTime,
            ArrivalTime = flight.ArrivalTime,
            BasePrice = flight.BasePrice,
            AvailableSeats = flight.AvailableSeats,
            Status = flight.Status,

            DepartureAirportId = flight.DepartureAirportId,
            DepartureAirportName = flight.DepartureAirport.Name,
            DepartureCity = flight.DepartureAirport.City,
            DepartureIata = flight.DepartureAirport.IataCode,
            DepartureCountry = flight.DepartureAirport.Country,

            ArrivalAirportId = flight.ArrivalAirportId,
            ArrivalAirportName = flight.ArrivalAirport.Name,
            ArrivalCity = flight.ArrivalAirport.City,
            ArrivalIata = flight.ArrivalAirport.IataCode,
            ArrivalCountry = flight.ArrivalAirport.Country,

            SeatClasses = flight.FlightSeatClasses.Select(fsc => new SeatClassVM
            {
                ClassId = fsc.ClassId,
                ClassName = fsc.SeatClass.ClassName,
                Price = fsc.FinalPrice,
                AvailableSeats = fsc.AvailableSeats,
                ClassMultiplier = fsc.SeatClass.ClassMultiplier,
                Description = fsc.SeatClass.ClassName == "Economy" ? "Standard seating with great value" :
                             fsc.SeatClass.ClassName == "Business" ? "Premium seating with extra legroom and amenities" :
                             "Luxury seating with exclusive service"
            }).ToList()
        };

        ViewBag.Passengers = passengers;

        return View(model);
    }
}
