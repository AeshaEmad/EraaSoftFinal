using System.ComponentModel.DataAnnotations;

namespace AeroFly.Web.ViewModels;

public class PaymentListVM
{
    public int PayId { get; set; }

    [Display(Name = "Transaction Ref")]
    public string TransactionRef { get; set; } = null!;

    [Display(Name = "PNR")]
    public string PNR => $"AF{BookingId:D6}";

    public int BookingId { get; set; }

    [Display(Name = "Passenger")]
    public string PassengerName { get; set; } = null!;

    [Display(Name = "Email")]
    public string Email { get; set; } = null!;

    [Display(Name = "Flight")]
    public string FlightNumber { get; set; } = null!;

    [Display(Name = "Amount")]
    [DataType(DataType.Currency)]
    public decimal Amount { get; set; }

    [Display(Name = "Payment Method")]
    public string PayMethod { get; set; } = null!;

    [Display(Name = "Status")]
    public string PayStatus { get; set; } = null!;

    [Display(Name = "Payment Date")]
    public DateTime PayDate { get; set; }

    public string StatusColor => PayStatus switch
    {
        "Completed" => "success",
        "Pending" => "warning",
        "Failed" => "danger",
        "Refunded" => "info",
        _ => "secondary"
    };
}

public class PaymentDetailsVM
{
    public int PayId { get; set; }

    [Display(Name = "Transaction Ref")]
    public string TransactionRef { get; set; } = null!;

    [Display(Name = "PNR")]
    public string PNR => $"AF{BookingId:D6}";

    public int BookingId { get; set; }

    [Display(Name = "Passenger")]
    public string PassengerName { get; set; } = null!;

    [Display(Name = "Email")]
    public string Email { get; set; } = null!;

    [Display(Name = "Flight")]
    public string FlightNumber { get; set; } = null!;

    [Display(Name = "Route")]
    public string Route => $"{DepartureIata} → {ArrivalIata}";

    [Display(Name = "Departure IATA")]
    public string DepartureIata { get; set; } = null!;

    [Display(Name = "Arrival IATA")]
    public string ArrivalIata { get; set; } = null!;

    [Display(Name = "Departure Time")]
    public DateTime DepartureTime { get; set; }

    [Display(Name = "Amount")]
    [DataType(DataType.Currency)]
    public decimal Amount { get; set; }

    [Display(Name = "Payment Method")]
    public string PayMethod { get; set; } = null!;

    [Display(Name = "Status")]
    public string PayStatus { get; set; } = null!;

    [Display(Name = "Payment Date")]
    public DateTime PayDate { get; set; }

    [Display(Name = "Booking Status")]
    public string BookingStatus { get; set; } = null!;

    public string StatusColor => PayStatus switch
    {
        "Completed" => "success",
        "Pending" => "warning",
        "Failed" => "danger",
        "Refunded" => "info",
        _ => "secondary"
    };

    // Passenger Info
    public int PassengerCount { get; set; }
    public List<string> PassengerNames { get; set; } = new();
}