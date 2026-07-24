using System.ComponentModel.DataAnnotations;

namespace AeroFly.Web.Models;

public class RewardAccount
{
    [Key]
    public int AccountId { get; set; }

    [Required]
    public int PointsBalance { get; set; } = 0;

    [Required]
    public int UserId { get; set; }

    // Navigation
    public virtual User User { get; set; } = null!;
    public virtual ICollection<PointsTransaction> Transactions { get; set; } = new List<PointsTransaction>();
}