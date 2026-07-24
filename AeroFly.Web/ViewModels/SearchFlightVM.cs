using System.ComponentModel.DataAnnotations;

namespace AeroFly.Web.ViewModels;

public class SearchFlightVM
{
    [Display(Name = "Departure Airport")]
    public int? DepartureAirportId { get; set; }

    [Display(Name = "Arrival Airport")]
    public int? ArrivalAirportId { get; set; }

    [Display(Name = "Departure Date")]
    [DataType(DataType.Date)]
    public DateTime? DepartureDate { get; set; }

    [Display(Name = "Return Date")]
    [DataType(DataType.Date)]
    public DateTime? ReturnDate { get; set; }

    [Display(Name = "Passengers")]
    [Range(1, 10, ErrorMessage = "Passengers must be between 1 and 10")]
    public int PassengerCount { get; set; } = 1;

    [Display(Name = "Seat Class")]
    public int? SeatClassId { get; set; }

    [Display(Name = "Trip Type")]
    public string TripType { get; set; } = "OneWay";

    // Lists for dropdowns
    public List<AirportVM>? Airports { get; set; }
    public List<SeatClassVM>? SeatClasses { get; set; }
}

public class FlightResultVM
{
    public int FlightId { get; set; }
    public string FlightNum { get; set; } = null!;
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public decimal BasePrice { get; set; }
    public int AvailableSeats { get; set; }
    public string Status { get; set; } = null!;

    public int DepartureAirportId { get; set; }
    public string DepartureAirportName { get; set; } = null!;
    public string DepartureCity { get; set; } = null!;
    public string DepartureIata { get; set; } = null!;
    public string DepartureCountry { get; set; } = null!;

    public int ArrivalAirportId { get; set; }
    public string ArrivalAirportName { get; set; } = null!;
    public string ArrivalCity { get; set; } = null!;
    public string ArrivalIata { get; set; } = null!;
    public string ArrivalCountry { get; set; } = null!;

    public TimeSpan Duration => ArrivalTime - DepartureTime;
    public string DurationHours => $"{(int)Duration.TotalHours}h {Duration.Minutes}m";

    public string DepartureDisplay => $"{DepartureCity} ({DepartureIata})";
    public string ArrivalDisplay => $"{ArrivalCity} ({ArrivalIata})";

    public decimal PricePerPassenger => BasePrice;
    public decimal TotalPrice => BasePrice;

    public bool IsAvailable => AvailableSeats > 0 && (Status == "Scheduled" || Status == "Delayed");
}

public class FlightDetailsVM : FlightResultVM
{
    public List<SeatClassVM> SeatClasses { get; set; } = new();
}
