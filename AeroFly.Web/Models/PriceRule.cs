// PriceRule.cs
using System.ComponentModel.DataAnnotations;

namespace AeroFly.Web.Models;

public class PriceRule
{
    [Key]
    public int RuleId { get; set; }

    [Required]
    [StringLength(50)]
    public string ConditionType { get; set; } = null!; // AdvanceDays, Season, DayOfWeek

    [Required]
    [Range(0.5, 3)]
    public decimal Multiplier { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    public string? ConditionValue { get; set; }

    // Navigation
    public virtual ICollection<FlightRule> FlightRules { get; set; } = new List<FlightRule>();
}