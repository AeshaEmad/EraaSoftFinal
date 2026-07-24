namespace AeroFly.Web.ViewModels;

public class TicketVerificationVM
{
    public bool IsValid { get; set; }
    public bool WasAlreadyUsed { get; set; }
    public string Message { get; set; } = "";
    public string? PassengerName { get; set; }
    public string? PassportNumber { get; set; }
    public string? FlightNumber { get; set; }
    public string? SeatNumber { get; set; }
    public string? BookingStatus { get; set; }
    public DateTime? UsedAt { get; set; }
}
