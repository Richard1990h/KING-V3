// Credits Controller
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LittleHelperAI.API.Services;
using Stripe.Checkout;

namespace LittleHelperAI.API.Controllers;

[ApiController]
[Route("api/credits")]
[Authorize]
public class CreditsController : ControllerBase
{
    private readonly ICreditService _creditService;
    private readonly IAuthService _authService;
    private readonly IConfiguration _config;
    private readonly ILogger<CreditsController> _logger;

    public CreditsController(
        ICreditService creditService,
        IAuthService authService,
        IConfiguration config,
        ILogger<CreditsController> logger)
    {
        _creditService = creditService;
        _authService = authService;
        _config = config;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirst("user_id")?.Value ?? throw new UnauthorizedAccessException();

    [HttpGet("packages")]
    [AllowAnonymous]
    public ActionResult GetPackages()
    {
        var packages = _creditService.GetPackages();
        return Ok(packages);
    }

    [HttpGet("balance")]
    public async Task<ActionResult> GetBalance()
    {
        var user = await _authService.GetUserByIdAsync(GetUserId());
        if (user == null)
            return Unauthorized();

        var usesOwnKey = await _creditService.UserUsesOwnKeyAsync(user.Id);

        return Ok(new
        {
            credits = user.Credits,
            credits_enabled = user.CreditsEnabled,
            uses_own_key = usesOwnKey,
            free_usage = !user.CreditsEnabled || usesOwnKey
        });
    }

    [HttpGet("history")]
    public async Task<ActionResult> GetHistory([FromQuery] int limit = 50)
    {
        var history = await _creditService.GetHistoryAsync(GetUserId(), limit);
        return Ok(history);
    }

    [HttpPost("purchase")]
    public async Task<ActionResult> Purchase([FromBody] PurchaseRequest request)
    {
        var user = await _authService.GetUserByIdAsync(GetUserId());
        if (user == null)
            return Unauthorized();

        var package = _creditService.GetPackages().FirstOrDefault(p => p.Key == request.PackageId);
        if (package.Value == null)
            return BadRequest(new { detail = "Invalid package" });

        try
        {
            var stripeKey = _config["Stripe:SecretKey"];
            Stripe.StripeConfiguration.ApiKey = stripeKey;

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "usd",
                            UnitAmount = (long)(package.Value.Price * 100),
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = package.Value.Name,
                                Description = $"{package.Value.Credits} AI credits"
                            }
                        },
                        Quantity = 1
                    }
                },
                Mode = "payment",
                SuccessUrl = $"{request.OriginUrl}/credits/success?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{request.OriginUrl}/credits",
                Metadata = new Dictionary<string, string>
                {
                    { "user_id", user.Id },
                    { "package_id", request.PackageId },
                    { "credits", package.Value.Credits.ToString() }
                }
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            await _creditService.CreateTransactionAsync(user.Id, session.Id, request.PackageId, package.Value);

            return Ok(new { url = session.Url, session_id = session.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Stripe session");
            return BadRequest(new { detail = "Payment initialization failed" });
        }
    }

    [HttpGet("status/{sessionId}")]
    public async Task<ActionResult> CheckStatus(string sessionId)
    {
        var user = await _authService.GetUserByIdAsync(GetUserId());
        if (user == null)
            return Unauthorized();

        var result = await _creditService.CheckPaymentStatusAsync(sessionId, user.Id);
        return Ok(result);
    }
}

public record PurchaseRequest(string PackageId, string OriginUrl);
