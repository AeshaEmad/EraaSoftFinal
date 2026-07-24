// Admin.cs
using System.ComponentModel.DataAnnotations;

namespace AeroFly.Web.Models;

public class Admin
{
    [Key]
    public int AdminId { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [RegularExpression("^(SuperAdmin|Admin|Moderator)$", ErrorMessage = "Invalid admin level")]
    [Display(Name = "Admin Level")]
    public string AdminLevel { get; set; } = "Admin";

    [Display(Name = "Permissions")]
    public string Permissions { get; set; } = "Read,Write";

    // Navigation
    public virtual User User { get; set; } = null!;
}