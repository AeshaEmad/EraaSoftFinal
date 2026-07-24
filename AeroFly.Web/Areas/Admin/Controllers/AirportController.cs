using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AeroFly.Web.Data;
using AeroFly.Web.Models;
using AeroFly.Web.ViewModels;

namespace AeroFly.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "AdminOperations")]
public class AirportController : Controller
{
    private readonly ApplicationDbContext _context;

    public AirportController(ApplicationDbContext context)
    {
        _context = context;
    }

     
    // 1. INDEX - List all Airports
     
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Airports Management";

        var airports = await _context.Airports
            .OrderBy(a => a.Country)
            .ThenBy(a => a.City)
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
            .ToListAsync();

        return View(airports);
    }

     
    // 2. CREATE - GET
     
    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "Add New Airport";
        return View();
    }

     
    // 3. CREATE - POST
     
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AirportVM model)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Add New Airport";
            return View(model);
        }

        // Check if IATA code already exists
        var exists = await _context.Airports
            .AnyAsync(a => a.IataCode == model.IataCode);

        if (exists)
        {
            ModelState.AddModelError("IataCode", "This IATA code is already used by another airport.");
            ViewData["Title"] = "Add New Airport";
            return View(model);
        }

        var airport = new Airport
        {
            Name = model.Name.Trim(),
            City = model.City.Trim(),
            Country = model.Country.Trim(),
            IataCode = model.IataCode.ToUpper(),
            Latitude = model.Latitude,
            Longitude = model.Longitude
        };

        _context.Airports.Add(airport);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Airport '{airport.Name}' has been created successfully! ✈️";
        return RedirectToAction(nameof(Index));
    }

     
    // 4. EDIT - GET
     
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        ViewData["Title"] = "Edit Airport";

        var airport = await _context.Airports
            .FirstOrDefaultAsync(a => a.AirportId == id);

        if (airport == null)
        {
            TempData["Error"] = "Airport not found!";
            return RedirectToAction(nameof(Index));
        }

        var model = new AirportVM
        {
            AirportId = airport.AirportId,
            Name = airport.Name,
            City = airport.City,
            Country = airport.Country,
            IataCode = airport.IataCode,
            Latitude = airport.Latitude,
            Longitude = airport.Longitude
        };

        return View(model);
    }

     
    // 5. EDIT - POST
     
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AirportVM model)
    {
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Edit Airport";
            return View(model);
        }

        var airport = await _context.Airports
            .FirstOrDefaultAsync(a => a.AirportId == model.AirportId);

        if (airport == null)
        {
            TempData["Error"] = "Airport not found!";
            return RedirectToAction(nameof(Index));
        }

        // Check if IATA code is taken by another airport
        var exists = await _context.Airports
            .AnyAsync(a => a.IataCode == model.IataCode && a.AirportId != model.AirportId);

        if (exists)
        {
            ModelState.AddModelError("IataCode", "This IATA code is already used by another airport.");
            ViewData["Title"] = "Edit Airport";
            return View(model);
        }

        airport.Name = model.Name.Trim();
        airport.City = model.City.Trim();
        airport.Country = model.Country.Trim();
        airport.IataCode = model.IataCode.ToUpper();
        airport.Latitude = model.Latitude;
        airport.Longitude = model.Longitude;

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Airport '{airport.Name}' has been updated successfully! ✈️";
        return RedirectToAction(nameof(Index));
    }

     
    // 6. DELETE - GET (Confirmation)
     
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        ViewData["Title"] = "Delete Airport";

        var airport = await _context.Airports
            .FirstOrDefaultAsync(a => a.AirportId == id);

        if (airport == null)
        {
            TempData["Error"] = "Airport not found!";
            return RedirectToAction(nameof(Index));
        }

        var model = new AirportVM
        {
            AirportId = airport.AirportId,
            Name = airport.Name,
            City = airport.City,
            Country = airport.Country,
            IataCode = airport.IataCode,
            Latitude = airport.Latitude,
            Longitude = airport.Longitude
        };

        return View(model);
    }

     
    // 7. DELETE - POST (Confirm)
     
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var airport = await _context.Airports
            .FirstOrDefaultAsync(a => a.AirportId == id);

        if (airport == null)
        {
            TempData["Error"] = "Airport not found!";
            return RedirectToAction(nameof(Index));
        }

        // Check if airport has any flights
        var hasDepartureFlights = await _context.Flights
            .AnyAsync(f => f.DepartureAirportId == id);

        var hasArrivalFlights = await _context.Flights
            .AnyAsync(f => f.ArrivalAirportId == id);

        if (hasDepartureFlights || hasArrivalFlights)
        {
            TempData["Error"] = $"Cannot delete airport '{airport.Name}' because it has associated flights! ✈️";
            return RedirectToAction(nameof(Index));
        }

        var airportName = airport.Name;
        _context.Airports.Remove(airport);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Airport '{airportName}' has been deleted successfully! 🗑️";
        return RedirectToAction(nameof(Index));
    }
}
