using System.ComponentModel.DataAnnotations;

namespace AeroFly.Web.ViewModels;

public class BookingListVM
{
    public int BookingId { get; set; }

    [Display(Name = "PNR")]
    public string PNR => $"AF{BookingId:D6}";

    [Display(Name = "Passenger Name")]
    public string PassengerName { get; set; } = null!;

    [Display(Name = "Email")]
    public string Email { get; set; } = null!;

    [Display(Name = "Flight Number")]
    public string FlightNumber { get; set; } = null!;

    [Display(Name = "Route")]
    public string Route => $"{DepartureIata} → {ArrivalIata}";

    [Display(Name = "Departure IATA")]
    public string DepartureIata { get; set; } = null!;

    [Display(Name = "Arrival IATA")]
    public string ArrivalIata { get; set; } = null!;

    [Display(Name = "Departure Time")]
    public DateTime DepartureTime { get; set; }

    [Display(Name = "Booking Date")]
    public DateTime BookingDate { get; set; }

    [Display(Name = "Passengers")]
    public int PassengerCount { get; set; }

    [Display(Name = "Total Price")]
    [DataType(DataType.Currency)]
    public decimal TotalPrice { get; set; }

    [Display(Name = "Status")]
    public string Status { get; set; } = null!;

    [Display(Name = "Payment Status")]
    public string? PaymentStatus { get; set; }

    [Display(Name = "Points Used")]
    public int PointsUsed { get; set; }

    [Display(Name = "Discount Applied")]
    public bool DiscountApplied { get; set; }

    public string StatusColor => Status switch
    {
        "Confirmed" => "success",
        "Pending" => "warning",
        "Cancelled" => "danger",
        "Completed" => "info",
        _ => "secondary"
    };
}

public class BookingDetailsVM
{
    public int BookingId { get; set; }

    [Display(Name = "PNR")]
    public string PNR => $"AF{BookingId:D6}";

    [Display(Name = "Booking Date")]
    public DateTime BookingDate { get; set; }

    [Display(Name = "Status")]
    public string Status { get; set; } = null!;

    [Display(Name = "Total Price")]
    [DataType(DataType.Currency)]
    public decimal TotalPrice { get; set; }

    [Display(Name = "Points Used")]
    public int PointsUsed { get; set; }

    [Display(Name = "Discount Applied")]
    public bool DiscountApplied { get; set; }

    // User Info
    [Display(Name = "User")]
    public string UserFullName { get; set; } = null!;

    [Display(Name = "Email")]
    public string UserEmail { get; set; } = null!;

    // Flight Info
    [Display(Name = "Flight Number")]
    public string FlightNumber { get; set; } = null!;

    [Display(Name = "Route")]
    public string Route => $"{DepartureAirport} ({DepartureIata}) → {ArrivalAirport} ({ArrivalIata})";

    [Display(Name = "Departure Airport")]
    public string DepartureAirport { get; set; } = null!;

    [Display(Name = "Arrival Airport")]
    public string ArrivalAirport { get; set; } = null!;

    [Display(Name = "Departure IATA")]
    public string DepartureIata { get; set; } = null!;

    [Display(Name = "Arrival IATA")]
    public string ArrivalIata { get; set; } = null!;

    [Display(Name = "Departure Time")]
    public DateTime DepartureTime { get; set; }

    [Display(Name = "Arrival Time")]
    public DateTime ArrivalTime { get; set; }

    [Display(Name = "Duration")]
    public TimeSpan Duration => ArrivalTime - DepartureTime;

    [Display(Name = "Available Seats")]
    public int AvailableSeats { get; set; }

    // Payment Info
    [Display(Name = "Payment Status")]
    public string? PaymentStatus { get; set; }

    [Display(Name = "Payment Method")]
    public string? PaymentMethod { get; set; }

    [Display(Name = "Payment Date")]
    public DateTime? PaymentDate { get; set; }

    // Passenger List
    public List<PassengerInfoVM> Passengers { get; set; } = new();
}

public class PassengerInfoVM
{
    [Display(Name = "Passenger")]
    public string FullName { get; set; } = null!;

    [Display(Name = "Passport")]
    public string PassportNumber { get; set; } = null!;

    [Display(Name = "Age")]
    public int Age { get; set; }

    [Display(Name = "Seat Class")]
    public string SeatClass { get; set; } = null!;

    [Display(Name = "Seat Number")]
    public string? SeatNumber { get; set; }

    [Display(Name = "Ticket Number")]
    public string? TicketNumber { get; set; }
}