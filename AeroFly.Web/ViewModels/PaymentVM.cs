using System.ComponentModel.DataAnnotations;

namespace AeroFly.Web.ViewModels;

public class PaymentVM
{
    public int BookingId { get; set; }

    [Display(Name = "Amount")]
    [DataType(DataType.Currency)]
    public decimal Amount { get; set; }

    public int AvailablePoints { get; set; }

    // Payment Method (for display)
    [Display(Name = "Payment Method")]
    public string PaymentMethod { get; set; } = "CreditCard";

    // Stripe specific
    public string? StripeClientSecret { get; set; }
    public string? StripePublishableKey { get; set; }

    // ===== OLD FIELDS (Keep for reference, but not used with Stripe) =====
    // These are kept for compatibility with existing code

    [Display(Name = "Card Number")]
    [CreditCard(ErrorMessage = "Invalid card number")]
    public string? CardNumber { get; set; }

    [Display(Name = "Card Holder Name")]
    public string? CardHolderName { get; set; }

    [Display(Name = "Expiry Date")]
    [RegularExpression(@"^(0[1-9]|1[0-2])\/([0-9]{2})$", ErrorMessage = "Invalid expiry date format (MM/YY)")]
    public string? ExpiryDate { get; set; }

    [Display(Name = "CVV")]
    [StringLength(4, MinimumLength = 3, ErrorMessage = "CVV must be 3 or 4 digits")]
    [RegularExpression(@"^[0-9]{3,4}$", ErrorMessage = "CVV must contain only numbers")]
    public string? Cvv { get; set; }

    [Display(Name = "Save Card")]
    public bool SaveCard { get; set; } = false;
}
