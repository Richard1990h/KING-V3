// Plans Controller - Subscription Plan Management
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LittleHelperAI.API.Services;
using LittleHelperAI.Data.Models;

namespace LittleHelperAI.API.Controllers;

[ApiController]
[Route("api")]
public class PlansController : ControllerBase
{
    private readonly ICreditService _creditService;
    private readonly IAuthService _authService;
    private readonly ILogger<PlansController> _logger;

    public PlansController(
        ICreditService creditService,
        IAuthService authService,
        ILogger<PlansController> logger)
    {
        _creditService = creditService;
        _authService = authService;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirst("user_id")?.Value ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Get all active subscription plans (public)
    /// </summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<ActionResult> GetPlans()
    {
        var plans = await _creditService.GetSubscriptionPlansAsync(activeOnly: true);
        return Ok(plans);
    }

    /// <summary>
    /// Get all plans including inactive (admin only)
    /// </summary>
    [HttpGet("plans/all")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> GetAllPlans()
    {
        var plans = await _creditService.GetSubscriptionPlansAsync(activeOnly: false);
        return Ok(plans);
    }

    /// <summary>
    /// Create a new subscription plan (admin only)
    /// </summary>
    [HttpPost("admin/plans")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> CreatePlan([FromBody] CreatePlanRequest request)
    {
        try
        {
            var plan = await _creditService.CreateSubscriptionPlanAsync(request);
            return Ok(plan);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing subscription plan (admin only)
    /// </summary>
    [HttpPut("admin/plans/{planId}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> UpdatePlan(string planId, [FromBody] UpdatePlanRequest request)
    {
        try
        {
            var plan = await _creditService.UpdateSubscriptionPlanAsync(planId, request);
            if (plan == null)
                return NotFound(new { detail = "Plan not found" });
            return Ok(plan);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Deactivate a subscription plan (admin only)
    /// </summary>
    [HttpDelete("admin/plans/{planId}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> DeactivatePlan(string planId)
    {
        // Prevent deletion of default plans
        var defaultPlans = new[] { "free", "starter", "pro", "enterprise" };
        if (defaultPlans.Contains(planId.ToLower()))
        {
            return BadRequest(new { detail = "Cannot delete default plans" });
        }

        var success = await _creditService.DeactivateSubscriptionPlanAsync(planId);
        if (!success)
            return NotFound(new { detail = "Plan not found" });
        
        return Ok(new { message = "Plan deactivated" });
    }

    /// <summary>
    /// Distribute daily credits to eligible users (admin only)
    /// </summary>
    [HttpPost("admin/distribute-daily-credits")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> DistributeDailyCredits()
    {
        var count = await _creditService.DistributeDailyCreditsAsync();
        return Ok(new { message = $"Distributed credits to {count} users" });
    }

    /// <summary>
    /// Get user's current subscription details
    /// </summary>
    [HttpGet("user/subscription")]
    [Authorize]
    public async Task<ActionResult> GetUserSubscription()
    {
        var userId = GetUserId();
        var subscription = await _creditService.GetUserSubscriptionAsync(userId);
        return Ok(subscription);
    }

    /// <summary>
    /// Check if user can start a new workspace
    /// </summary>
    [HttpGet("user/workspace-limit")]
    [Authorize]
    public async Task<ActionResult> CheckWorkspaceLimit()
    {
        var userId = GetUserId();
        var result = await _creditService.CheckWorkspaceLimitAsync(userId);
        return Ok(result);
    }

    /// <summary>
    /// Subscribe to a plan
    /// </summary>
    [HttpPost("user/subscribe")]
    [Authorize]
    public async Task<ActionResult> Subscribe([FromBody] SubscribeRequest request)
    {
        var userId = GetUserId();
        
        try
        {
            var result = await _creditService.SubscribeUserAsync(userId, request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Get available credit add-on packages
    /// </summary>
    [HttpGet("credits/addon-packages")]
    [Authorize]
    public async Task<ActionResult> GetAddonPackages()
    {
        var packages = await _creditService.GetCreditPackagesAsync();
        return Ok(packages);
    }

    /// <summary>
    /// Purchase credit add-on
    /// </summary>
    [HttpPost("credits/purchase-addon")]
    [Authorize]
    public async Task<ActionResult> PurchaseAddon([FromBody] PurchaseAddonRequest request)
    {
        var userId = GetUserId();
        
        try
        {
            var result = await _creditService.PurchaseCreditAddonAsync(userId, request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }
}

// Request/Response Models
public record CreatePlanRequest(
    string PlanId,
    string Name,
    string? Description,
    decimal PriceMonthly,
    decimal? PriceYearly,
    int DailyCredits,
    int MaxConcurrentWorkspaces,
    bool AllowsOwnApiKeys,
    List<string>? Features,
    int SortOrder = 0
);

public record UpdatePlanRequest(
    string? Name,
    string? Description,
    decimal? PriceMonthly,
    decimal? PriceYearly,
    int? DailyCredits,
    int? MaxConcurrentWorkspaces,
    bool? AllowsOwnApiKeys,
    List<string>? Features,
    bool? IsActive,
    int? SortOrder
);

public record SubscribeRequest(
    string PlanId,
    string? OriginUrl
);

public record PurchaseAddonRequest(
    string PackageId,
    string OriginUrl
);

public record WorkspaceLimitResponse(
    bool CanStartWorkspace,
    int ActiveWorkspaces,
    int MaxConcurrentWorkspaces,
    string? Message
);

public record UserSubscriptionResponse(
    string? PlanId,
    string? PlanName,
    int DailyCredits,
    int MaxConcurrentWorkspaces,
    bool AllowsOwnApiKeys,
    string Status,
    DateTime? StartDate,
    DateTime? NextBillingDate,
    int ActiveWorkspaces
);
