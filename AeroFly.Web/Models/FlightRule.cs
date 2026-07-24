// FlightRule.cs
namespace AeroFly.Web.Models;

public class FlightRule
{
    public int FlightId { get; set; }
    public int RuleId { get; set; }

    // Navigation
    public virtual Flight Flight { get; set; } = null!;
    public virtual PriceRule PriceRule { get; set; } = null!;
}