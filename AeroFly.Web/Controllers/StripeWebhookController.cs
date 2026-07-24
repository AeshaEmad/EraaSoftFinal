using AeroFly.Web.Data;
using AeroFly.Web.Models;
using AeroFly.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace AeroFly.Web.Controllers;

[ApiController]
[Route("api/stripe/webhook")]
[AllowAnonymous]
public class StripeWebhookController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IStripeService _stripe;
    private readonly IBookingWorkflowService _bookingWorkflow;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(
        ApplicationDbContext db,
        IStripeService stripe,
        IBookingWorkflowService bookingWorkflow,
        ILogger<StripeWebhookController> logger)
    {
        _db = db;
        _stripe = stripe;
        _bookingWorkflow = bookingWorkflow;
        _logger = logger;
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        var json = await new StreamReader(Request.Body).ReadToEndAsync(cancellationToken);
        Event stripeEvent;
        try
        {
            stripeEvent = _stripe.ConstructWebhookEvent(
                json,
                Request.Headers["Stripe-Signature"].ToString());
        }
        catch (Exception ex) when (ex is StripeException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Rejected Stripe webhook with an invalid signature.");
            return BadRequest();
        }

        if (await _db.StripeWebhookEvents.AnyAsync(
                e => e.EventId == stripeEvent.Id,
                cancellationToken))
        {
            return Ok();
        }

        try
        {
            switch (stripeEvent.Type)
            {
                case "payment_intent.succeeded":
                {
                    var intent = (PaymentIntent)stripeEvent.Data.Object;
                    if (!intent.Metadata.TryGetValue("booking_id", out var bookingValue) ||
                        !int.TryParse(bookingValue, out var bookingId))
                    {
                        return BadRequest("Missing booking metadata.");
                    }

                    var result = await _bookingWorkflow.ConfirmPaidBookingAsync(
                        bookingId,
                        intent.Id,
                        intent.Amount / 100m,
                        cancellationToken);
                    if (!result.Success)
                    {
                        _logger.LogError(
                            "Paid booking {BookingId} could not be confirmed: {Message}",
                            bookingId,
                            result.Message);
                    }
                    break;
                }
                case "payment_intent.payment_failed":
                {
                    var intent = (PaymentIntent)stripeEvent.Data.Object;
                    var payment = await _db.Payments.FirstOrDefaultAsync(
                        p => p.TransactionRef == intent.Id && p.PayStatus == "Pending",
                        cancellationToken);
                    if (payment != null)
                    {
                        payment.PayStatus = "Failed";
                    }
                    break;
                }
                case "refund.updated":
                {
                    var refund = (Refund)stripeEvent.Data.Object;
                    if (refund.Status == "succeeded" && !string.IsNullOrWhiteSpace(refund.PaymentIntentId))
                    {
                        await _bookingWorkflow.CompleteRefundAsync(
                            refund.Id,
                            refund.PaymentIntentId,
                            cancellationToken);
                    }
                    else if (refund.Status is "failed" or "canceled")
                    {
                        var payment = await _db.Payments.FirstOrDefaultAsync(
                            p => p.StripeRefundId == refund.Id,
                            cancellationToken);
                        if (payment != null)
                        {
                            payment.RefundStatus = "Failed";
                            payment.RefundFailureReason = refund.FailureReason;
                        }
                    }
                    break;
                }
            }

            _db.StripeWebhookEvents.Add(new StripeWebhookEvent
            {
                EventId = stripeEvent.Id,
                EventType = stripeEvent.Type
            });
            await _db.SaveChangesAsync(cancellationToken);
            return Ok();
        }
        catch (DbUpdateException ex)
        {
            _db.ChangeTracker.Clear();
            if (await _db.StripeWebhookEvents.AnyAsync(
                    e => e.EventId == stripeEvent.Id,
                    cancellationToken))
            {
                _logger.LogInformation(ex, "Stripe event {EventId} was processed concurrently.", stripeEvent.Id);
                return Ok();
            }

            _logger.LogError(ex, "Stripe event {EventId} failed and will be retried.", stripeEvent.Id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stripe event {EventId} failed and will be retried.", stripeEvent.Id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
