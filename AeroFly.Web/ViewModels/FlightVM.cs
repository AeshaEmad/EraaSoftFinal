using System.ComponentModel.DataAnnotations;

namespace AeroFly.Web.ViewModels;

public class FlightVM
{
    public int FlightId { get; set; }

    [Required(ErrorMessage = "Flight number is required")]
    [StringLength(10, MinimumLength = 3, ErrorMessage = "Flight number must be between 3 and 10 characters")]
    [Display(Name = "Flight Number")]
    public string FlightNum { get; set; } = null!;

    [Required(ErrorMessage = "Departure time is required")]
    [Display(Name = "Departure Time")]
    public DateTime DepartureTime { get; set; }

    [Required(ErrorMessage = "Arrival time is required")]
    [Display(Name = "Arrival Time")]
    public DateTime ArrivalTime { get; set; }

    [Required(ErrorMessage = "Base price is required")]
    [Range(0, 100000, ErrorMessage = "Base price must be between 0 and 100,000")]
    [DataType(DataType.Currency)]
    [Display(Name = "Base Price")]
    public decimal BasePrice { get; set; }

    [Required(ErrorMessage = "Status is required")]
    [RegularExpression("^(Scheduled|Delayed|Cancelled|Completed)$", ErrorMessage = "Invalid status")]
    public string Status { get; set; } = "Scheduled";

    [Required]
    [Display(Name = "Departure Airport")]
    public int DepartureAirportId { get; set; }

    [Required]
    [Display(Name = "Arrival Airport")]
    public int ArrivalAirportId { get; set; }

    [Required]
    [Range(0, 500, ErrorMessage = "Available seats must be between 0 and 500")]
    [Display(Name = "Available Seats")]
    public int AvailableSeats { get; set; }

    // Display properties
    [Display(Name = "Departure Airport")]
    public string? DepartureAirportName { get; set; }

    [Display(Name = "Arrival Airport")]
    public string? ArrivalAirportName { get; set; }

    [Display(Name = "Departure IATA")]
    public string? DepartureIataCode { get; set; }

    [Display(Name = "Arrival IATA")]
    public string? ArrivalIataCode { get; set; }

    [Display(Name = "Duration")]
    public TimeSpan Duration => ArrivalTime - DepartureTime;

    [Display(Name = "Duration (hours)")]
    public string DurationHours => Duration.TotalHours >= 24
        ? $"{(int)Duration.TotalHours}h {Duration.Minutes:D2}m"
        : Duration.ToString(@"hh\:mm");

    [Display(Name = "Full Route")]
    public string FullRoute => $"{DepartureIataCode} → {ArrivalIataCode}";
}