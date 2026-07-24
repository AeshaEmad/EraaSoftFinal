namespace AeroFly.Web.ViewModels;

public class RewardViewModel
{
    public int PointsBalance { get; set; }
    public List<TransactionVM> Transactions { get; set; } = new();
}

public class TransactionVM
{
    public int Points { get; set; }
    public string Type { get; set; } = null!;
    public DateTime Date { get; set; }
    public string Description { get; set; } = null!;

    public string TypeColor => Type switch
    {
        "Earned" => "success",
        "Redeemed" => "warning",
        "Refunded" => "info",
        "Expired" => "danger",
        _ => "secondary"
    };

    public string TypeIcon => Type switch
    {
        "Earned" => "fa-plus-circle",
        "Redeemed" => "fa-minus-circle",
        "Refunded" => "fa-undo",
        "Expired" => "fa-times-circle",
        _ => "fa-circle"
    };
}
