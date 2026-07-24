using System.ComponentModel.DataAnnotations;

namespace AeroFly.Web.ViewModels;

public class BookingCreateVM
{
    public int FlightId { get; set; }
    public int ClassId { get; set; }
    [Range(1, 10, ErrorMessage = "Passengers must be between 1 and 10")]
    public int PassengerCount { get; set; }

    [Display(Name = "Flight Number")]
    public string? FlightNumber { get; set; }

    [Display(Name = "Route")]
    public string? Route { get; set; }

    [Display(Name = "Departure")]
    public DateTime? DepartureTime { get; set; }

    [Display(Name = "Arrival")]
    public DateTime? ArrivalTime { get; set; }

    [Display(Name = "Price per Passenger")]
    public decimal? PricePerPassenger { get; set; }

    [Display(Name = "Total Price")]
    public decimal TotalPrice => (PricePerPassenger ?? 0) * PassengerCount;

    [Display(Name = "Seat Class")]
    public string? SeatClassName { get; set; }

    public List<PassengerDetailsVM> Passengers { get; set; } = new();
}

public class PassengerDetailsVM
{
    [Required(ErrorMessage = "Full name is required")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Full name must be between 3 and 100 characters")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = null!;

    [Required(ErrorMessage = "Passport number is required")]
    [StringLength(20, MinimumLength = 6, ErrorMessage = "Passport number must be between 6 and 20 characters")]
    [Display(Name = "Passport Number")]
    public string PassportNumber { get; set; } = null!;

    [Required(ErrorMessage = "Age is required")]
    [Range(0, 120, ErrorMessage = "Age must be between 0 and 120")]
    public int Age { get; set; }

    public string? SeatNumber { get; set; }
    public string? TicketToken { get; set; }
}

public class BookingConfirmationVM
{
    public int BookingId { get; set; }
    public string PNR => $"AF{BookingId:D6}";
    public string FlightNumber { get; set; } = null!;
    public string Route { get; set; } = null!;
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public int PassengerCount { get; set; }
    public decimal TotalPrice { get; set; }
    public string SeatClass { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime BookingDate { get; set; }
    public List<PassengerDetailsVM> Passengers { get; set; } = new();

    // ===== NEW: Properties for Email =====
    public string DepartureCity { get; set; } = null!;
    public string ArrivalCity { get; set; } = null!;
    public string DepartureIata { get; set; } = null!;
    public string ArrivalIata { get; set; } = null!;
}
