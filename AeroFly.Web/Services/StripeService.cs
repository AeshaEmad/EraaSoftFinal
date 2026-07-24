using Stripe;

namespace AeroFly.Web.Services;

public interface IStripeService
{
    Task<PaymentIntent> CreatePaymentIntentAsync(decimal amount, int bookingId, int userId, string currency = "usd");
    Task<PaymentIntent> GetPaymentIntentAsync(string paymentIntentId);
    Task<PaymentIntent> ConfirmPaymentIntentAsync(string paymentIntentId);
    Task CancelPaymentIntentAsync(string paymentIntentId);
    Task<Refund> CreateRefundAsync(string paymentIntentId, int bookingId);
    Event ConstructWebhookEvent(string json, string signature);
}

public class StripeService : IStripeService
{
    private readonly string _webhookSecret;

    public StripeService(IConfiguration config)
    {
        var secretKey = config["Stripe:SecretKey"];
        _webhookSecret = config["Stripe:WebhookSecret"] ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(secretKey))
        {
            StripeConfiguration.ApiKey = secretKey;
        }
    }

    public async Task<PaymentIntent> CreatePaymentIntentAsync(
        decimal amount,
        int bookingId,
        int userId,
        string currency = "usd")
    {
        var options = new PaymentIntentCreateOptions
        {
            Amount = checked((long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero)),
            Currency = currency,
            PaymentMethodTypes = new List<string> { "card" },
            Metadata = new Dictionary<string, string>
            {
                ["booking_id"] = bookingId.ToString(),
                ["user_id"] = userId.ToString()
            }
        };

        return await new PaymentIntentService().CreateAsync(
            options,
            new RequestOptions { IdempotencyKey = $"booking-{bookingId}-amount-{options.Amount}" });
    }

    public Task<PaymentIntent> GetPaymentIntentAsync(string paymentIntentId) =>
        new PaymentIntentService().GetAsync(paymentIntentId);

    public Task<PaymentIntent> ConfirmPaymentIntentAsync(string paymentIntentId) =>
        new PaymentIntentService().ConfirmAsync(paymentIntentId);

    public async Task CancelPaymentIntentAsync(string paymentIntentId)
    {
        if (!paymentIntentId.StartsWith("pi_", StringComparison.Ordinal))
        {
            return;
        }

        var intent = await GetPaymentIntentAsync(paymentIntentId);
        if (intent.Status is "succeeded" or "canceled")
        {
            return;
        }

        await new PaymentIntentService().CancelAsync(
            paymentIntentId,
            new PaymentIntentCancelOptions(),
            new RequestOptions { IdempotencyKey = $"cancel-{paymentIntentId}" });
    }

    public Task<Refund> CreateRefundAsync(string paymentIntentId, int bookingId) =>
        new RefundService().CreateAsync(
            new RefundCreateOptions
            {
                PaymentIntent = paymentIntentId,
                Reason = "requested_by_customer",
                Metadata = new Dictionary<string, string>
                {
                    ["booking_id"] = bookingId.ToString()
                }
            },
            new RequestOptions { IdempotencyKey = $"booking-refund-{bookingId}" });

    public Event ConstructWebhookEvent(string json, string signature)
    {
        if (string.IsNullOrWhiteSpace(_webhookSecret))
        {
            throw new InvalidOperationException("Stripe:WebhookSecret is not configured.");
        }

        return EventUtility.ConstructEvent(json, signature, _webhookSecret);
    }
}
