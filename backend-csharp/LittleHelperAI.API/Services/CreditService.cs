// Credit Service Implementation
using LittleHelperAI.API.Controllers;
using LittleHelperAI.Data;
using LittleHelperAI.Data.Models;
using System.Text.Json;

namespace LittleHelperAI.API.Services;

public class CreditService : ICreditService
{
    private readonly IDbContext _db;
    private readonly ILogger<CreditService> _logger;

    private static readonly Dictionary<string, CreditPackageInfo> _defaultPackages = new()
    {
        ["pack-100"] = new("100 Credits", 100, 4.99m),
        ["pack-500"] = new("500 Credits", 500, 19.99m),
        ["pack-1000"] = new("1000 Credits", 1000, 34.99m),
        ["pack-5000"] = new("5000 Credits", 5000, 149.99m)
    };

    public CreditService(IDbContext db, ILogger<CreditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Dictionary<string, CreditPackageInfo> GetPackages() => _defaultPackages;

    public async Task<List<CreditHistoryEntry>> GetHistoryAsync(string userId, int limit)
    {
        var history = await _db.QueryAsync<CreditHistoryEntry>(
            "SELECT * FROM credit_history WHERE user_id = @UserId ORDER BY created_at DESC LIMIT @Limit",
            new { UserId = userId, Limit = limit });
        return history.ToList();
    }

    public async Task<int> BulkAddCreditsAsync(double amount, List<string>? userIds)
    {
        if (userIds == null || userIds.Count == 0)
        {
            // Add to all users
            return await _db.ExecuteAsync(
                "UPDATE users SET credits = credits + @Amount",
                new { Amount = amount });
        }

        var count = 0;
        foreach (var userId in userIds)
        {
            var result = await _db.ExecuteAsync(
                "UPDATE users SET credits = credits + @Amount WHERE id = @UserId",
                new { Amount = amount, UserId = userId });
            count += result;
        }
        return count;
    }

    public async Task<bool> UserUsesOwnKeyAsync(string userId)
    {
        var count = await _db.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM user_ai_providers WHERE user_id = @UserId AND is_active = TRUE",
            new { UserId = userId });
        return count > 0;
    }

    public async Task<decimal> DeductCreditsAsync(string userId, decimal amount, string reason)
    {
        var user = await _db.QueryFirstOrDefaultAsync<User>(
            "SELECT credits FROM users WHERE id = @UserId",
            new { UserId = userId });

        if (user == null) throw new InvalidOperationException("User not found");

        var newBalance = user.Credits - amount;
        if (newBalance < 0) throw new InvalidOperationException("Insufficient credits");

        await _db.ExecuteAsync(
            "UPDATE users SET credits = @NewBalance WHERE id = @UserId",
            new { NewBalance = newBalance, UserId = userId });

        await _db.ExecuteAsync(@"
            INSERT INTO credit_history (id, user_id, delta, reason, balance_after, created_at)
            VALUES (@Id, @UserId, @Delta, @Reason, @BalanceAfter, @CreatedAt)",
            new {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                Delta = -amount,
                Reason = reason,
                BalanceAfter = newBalance,
                CreatedAt = DateTime.UtcNow
            });

        return newBalance;
    }

    public async Task<object> GetSettingsAsync()
    {
        var settings = await _db.QueryAsync<SystemSetting>("SELECT * FROM system_settings");
        return settings.ToDictionary(s => s.SettingKey, s => s.SettingValue);
    }

    public async Task<bool> UpdateSettingAsync(string key, string value)
    {
        var result = await _db.ExecuteAsync(@"
            INSERT INTO system_settings (setting_key, setting_value, updated_at)
            VALUES (@Key, @Value, @Now)
            ON DUPLICATE KEY UPDATE setting_value = @Value, updated_at = @Now",
            new { Key = key, Value = value, Now = DateTime.UtcNow });
        return result > 0;
    }

    public async Task<string?> GetSettingValueAsync(string key)
    {
        var setting = await _db.QueryFirstOrDefaultAsync<SystemSetting>(
            "SELECT * FROM system_settings WHERE setting_key = @Key",
            new { Key = key });
        return setting?.SettingValue;
    }

    public async Task<List<CreditPackage>> GetCreditPackagesAsync()
    {
        var packages = await _db.QueryAsync<CreditPackage>(
            "SELECT * FROM credit_packages WHERE is_active = TRUE ORDER BY sort_order");
        return packages.ToList();
    }

    public async Task<CreditPackage> CreateCreditPackageAsync(CreateCreditPackageRequest request)
    {
        var package = new CreditPackage
        {
            Id = request.PackageId,
            Name = request.Name,
            Credits = request.Credits,
            Price = request.Price,
            SortOrder = request.SortOrder,
            IsActive = true
        };

        await _db.ExecuteAsync(@"
            INSERT INTO credit_packages (id, name, credits, price, sort_order)
            VALUES (@Id, @Name, @Credits, @Price, @SortOrder)",
            package);

        return package;
    }

    public async Task<bool> UpdateCreditPackageAsync(string packageId, UpdateCreditPackageRequest request)
    {
        var updates = new List<string>();
        var parameters = new Dictionary<string, object> { ["Id"] = packageId };

        if (request.Name != null) { updates.Add("name = @Name"); parameters["Name"] = request.Name; }
        if (request.Credits.HasValue) { updates.Add("credits = @Credits"); parameters["Credits"] = request.Credits.Value; }
        if (request.Price.HasValue) { updates.Add("price = @Price"); parameters["Price"] = request.Price.Value; }
        if (request.IsActive.HasValue) { updates.Add("is_active = @IsActive"); parameters["IsActive"] = request.IsActive.Value; }
        if (request.SortOrder.HasValue) { updates.Add("sort_order = @SortOrder"); parameters["SortOrder"] = request.SortOrder.Value; }

        if (updates.Count == 0) return true;

        var result = await _db.ExecuteAsync(
            $"UPDATE credit_packages SET {string.Join(", ", updates)} WHERE id = @Id",
            parameters);
        return result > 0;
    }

    public async Task<bool> DeleteCreditPackageAsync(string packageId)
    {
        var result = await _db.ExecuteAsync(
            "UPDATE credit_packages SET is_active = FALSE WHERE id = @Id",
            new { Id = packageId });
        return result > 0;
    }

    public async Task<List<SubscriptionPlan>> GetSubscriptionPlansAsync(bool activeOnly = true)
    {
        var sql = activeOnly 
            ? "SELECT * FROM subscription_plans WHERE is_active = TRUE ORDER BY sort_order"
            : "SELECT * FROM subscription_plans ORDER BY sort_order";
        var plans = await _db.QueryAsync<SubscriptionPlan>(sql);
        return plans.ToList();
    }

    public async Task<SubscriptionPlan> CreateSubscriptionPlanAsync(CreatePlanRequest request)
    {
        var plan = new SubscriptionPlan
        {
            Id = request.PlanId,
            Name = request.Name,
            Description = request.Description ?? "",
            PriceMonthly = request.PriceMonthly,
            PriceYearly = request.PriceYearly ?? request.PriceMonthly * 10,
            DailyCredits = request.DailyCredits,
            MaxConcurrentWorkspaces = request.MaxConcurrentWorkspaces,
            AllowsOwnApiKeys = request.AllowsOwnApiKeys,
            Features = request.Features ?? new List<string>(),
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _db.ExecuteAsync(@"
            INSERT INTO subscription_plans (id, name, description, price_monthly, price_yearly, daily_credits, 
                max_concurrent_workspaces, allows_own_api_keys, features, sort_order, created_at, updated_at)
            VALUES (@Id, @Name, @Description, @PriceMonthly, @PriceYearly, @DailyCredits, 
                @MaxConcurrentWorkspaces, @AllowsOwnApiKeys, @Features, @SortOrder, @CreatedAt, @UpdatedAt)",
            new {
                plan.Id, plan.Name, plan.Description, plan.PriceMonthly, plan.PriceYearly, plan.DailyCredits,
                plan.MaxConcurrentWorkspaces, plan.AllowsOwnApiKeys, 
                Features = JsonSerializer.Serialize(plan.Features),
                plan.SortOrder, plan.CreatedAt, plan.UpdatedAt
            });

        return plan;
    }

    public async Task<SubscriptionPlan?> UpdateSubscriptionPlanAsync(string planId, UpdatePlanRequest request)
    {
        var updates = new List<string>();
        var parameters = new Dictionary<string, object> { ["Id"] = planId };

        if (request.Name != null) { updates.Add("name = @Name"); parameters["Name"] = request.Name; }
        if (request.Description != null) { updates.Add("description = @Description"); parameters["Description"] = request.Description; }
        if (request.PriceMonthly.HasValue) { updates.Add("price_monthly = @PriceMonthly"); parameters["PriceMonthly"] = request.PriceMonthly.Value; }
        if (request.PriceYearly.HasValue) { updates.Add("price_yearly = @PriceYearly"); parameters["PriceYearly"] = request.PriceYearly.Value; }
        if (request.DailyCredits.HasValue) { updates.Add("daily_credits = @DailyCredits"); parameters["DailyCredits"] = request.DailyCredits.Value; }
        if (request.MaxConcurrentWorkspaces.HasValue) { updates.Add("max_concurrent_workspaces = @MaxWorkspaces"); parameters["MaxWorkspaces"] = request.MaxConcurrentWorkspaces.Value; }
        if (request.AllowsOwnApiKeys.HasValue) { updates.Add("allows_own_api_keys = @AllowsApiKeys"); parameters["AllowsApiKeys"] = request.AllowsOwnApiKeys.Value; }
        if (request.Features != null) { updates.Add("features = @Features"); parameters["Features"] = JsonSerializer.Serialize(request.Features); }
        if (request.IsActive.HasValue) { updates.Add("is_active = @IsActive"); parameters["IsActive"] = request.IsActive.Value; }
        if (request.SortOrder.HasValue) { updates.Add("sort_order = @SortOrder"); parameters["SortOrder"] = request.SortOrder.Value; }

        updates.Add("updated_at = @UpdatedAt");
        parameters["UpdatedAt"] = DateTime.UtcNow;

        await _db.ExecuteAsync(
            $"UPDATE subscription_plans SET {string.Join(", ", updates)} WHERE id = @Id",
            parameters);

        return await _db.QueryFirstOrDefaultAsync<SubscriptionPlan>(
            "SELECT * FROM subscription_plans WHERE id = @Id",
            new { Id = planId });
    }

    public async Task<bool> DeactivateSubscriptionPlanAsync(string planId)
    {
        var result = await _db.ExecuteAsync(
            "UPDATE subscription_plans SET is_active = FALSE, updated_at = @Now WHERE id = @Id",
            new { Id = planId, Now = DateTime.UtcNow });
        return result > 0;
    }

    // Wrapper methods for admin controller with new request types
    public async Task<SubscriptionPlan> CreateSubscriptionPlanAsync(CreateSubscriptionPlanRequest request)
    {
        var legacyRequest = new CreatePlanRequest(
            request.Id, request.Name, request.Description,
            request.PriceMonthly, request.PriceYearly, request.DailyCredits,
            request.MaxConcurrentWorkspaces, request.AllowsOwnApiKeys,
            request.Features, request.SortOrder
        );
        return await CreateSubscriptionPlanAsync(legacyRequest);
    }

    public async Task<bool> UpdateSubscriptionPlanAsync(string planId, UpdateSubscriptionPlanRequest request)
    {
        var legacyRequest = new UpdatePlanRequest(
            request.Name, request.Description, request.PriceMonthly, request.PriceYearly,
            request.DailyCredits, request.MaxConcurrentWorkspaces, request.AllowsOwnApiKeys,
            request.Features, request.IsActive, request.SortOrder
        );
        var result = await UpdateSubscriptionPlanAsync(planId, legacyRequest);
        return result != null;
    }

    public async Task<bool> DeleteSubscriptionPlanAsync(string planId)
    {
        var result = await _db.ExecuteAsync(
            "DELETE FROM subscription_plans WHERE id = @Id",
            new { Id = planId });
        return result > 0;
    }

    public async Task<int> DistributeDailyCreditsAsync()
    {
        // Get active subscriptions with their plan credits
        var result = await _db.ExecuteAsync(@"
            UPDATE users u
            INNER JOIN user_subscriptions us ON u.id = us.user_id AND us.status = 'active'
            INNER JOIN subscription_plans sp ON us.plan_id = sp.id
            SET u.credits = u.credits + sp.daily_credits
            WHERE sp.daily_credits > 0");
        return result;
    }

    public async Task<UserSubscriptionResponse> GetUserSubscriptionAsync(string userId)
    {
        var user = await _db.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM users WHERE id = @UserId",
            new { UserId = userId });

        var subscription = await _db.QueryFirstOrDefaultAsync<UserSubscription>(
            "SELECT * FROM user_subscriptions WHERE user_id = @UserId AND status = 'active'",
            new { UserId = userId });

        var plan = subscription != null
            ? await _db.QueryFirstOrDefaultAsync<SubscriptionPlan>(
                "SELECT * FROM subscription_plans WHERE id = @PlanId",
                new { subscription.PlanId })
            : null;

        var activeWorkspaces = await _db.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM projects WHERE user_id = @UserId AND status = 'active'",
            new { UserId = userId });

        return new UserSubscriptionResponse(
            plan?.Id ?? user?.Plan ?? "free",
            plan?.Name ?? "Free",
            plan?.DailyCredits ?? 50,
            plan?.MaxConcurrentWorkspaces ?? 1,
            plan?.AllowsOwnApiKeys ?? false,
            subscription?.Status ?? "active",
            subscription?.StartDate,
            subscription?.NextBillingDate,
            activeWorkspaces
        );
    }

    public async Task<WorkspaceLimitResponse> CheckWorkspaceLimitAsync(string userId)
    {
        var subscription = await GetUserSubscriptionAsync(userId);
        var canStart = subscription.MaxConcurrentWorkspaces < 0 || 
                       subscription.ActiveWorkspaces < subscription.MaxConcurrentWorkspaces;

        return new WorkspaceLimitResponse(
            canStart,
            subscription.ActiveWorkspaces,
            subscription.MaxConcurrentWorkspaces,
            canStart ? null : $"You have reached the maximum of {subscription.MaxConcurrentWorkspaces} workspaces for your plan."
        );
    }

    public async Task<object> SubscribeUserAsync(string userId, SubscribeRequest request)
    {
        // For a real implementation, this would integrate with Stripe
        var subscriptionId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        // Cancel any existing subscription
        await _db.ExecuteAsync(
            "UPDATE user_subscriptions SET status = 'cancelled' WHERE user_id = @UserId AND status = 'active'",
            new { UserId = userId });

        // Create new subscription
        await _db.ExecuteAsync(@"
            INSERT INTO user_subscriptions (id, user_id, plan_id, status, start_date, next_billing_date, created_at)
            VALUES (@Id, @UserId, @PlanId, 'active', @StartDate, @NextBilling, @CreatedAt)",
            new {
                Id = subscriptionId,
                UserId = userId,
                PlanId = request.PlanId,
                StartDate = now,
                NextBilling = now.AddMonths(1),
                CreatedAt = now
            });

        // Update user's plan
        await _db.ExecuteAsync(
            "UPDATE users SET plan = @PlanId WHERE id = @UserId",
            new { PlanId = request.PlanId, UserId = userId });

        return new { message = "Subscription activated", subscription_id = subscriptionId };
    }

    public async Task<object> PurchaseCreditAddonAsync(string userId, PurchaseAddonRequest request)
    {
        var package = await _db.QueryFirstOrDefaultAsync<CreditPackage>(
            "SELECT * FROM credit_packages WHERE id = @Id AND is_active = TRUE",
            new { Id = request.PackageId });

        if (package == null)
            throw new InvalidOperationException("Package not found");

        // In production, integrate with Stripe here
        // For now, just add the credits
        await _db.ExecuteAsync(
            "UPDATE users SET credits = credits + @Credits WHERE id = @UserId",
            new { Credits = package.Credits, UserId = userId });

        await _db.ExecuteAsync(@"
            INSERT INTO credit_history (id, user_id, delta, reason, balance_after, created_at)
            SELECT @Id, @UserId, @Credits, @Reason, credits, @CreatedAt FROM users WHERE id = @UserId",
            new {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                Credits = package.Credits,
                Reason = $"Purchased {package.Name}",
                CreatedAt = DateTime.UtcNow
            });

        return new { message = "Credits added", credits_added = package.Credits };
    }

    public async Task CreateTransactionAsync(string userId, string sessionId, string packageId, CreditPackageInfo package)
    {
        await _db.ExecuteAsync(@"
            INSERT INTO payment_transactions (id, user_id, session_id, package_id, amount, credits, status, created_at)
            VALUES (@Id, @UserId, @SessionId, @PackageId, @Amount, @Credits, 'pending', @CreatedAt)",
            new {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                SessionId = sessionId,
                PackageId = packageId,
                Amount = package.Price,
                Credits = package.Credits,
                CreatedAt = DateTime.UtcNow
            });
    }

    public async Task<object> CheckPaymentStatusAsync(string sessionId, string userId)
    {
        var transaction = await _db.QueryFirstOrDefaultAsync<PaymentTransaction>(
            "SELECT * FROM payment_transactions WHERE session_id = @SessionId AND user_id = @UserId",
            new { SessionId = sessionId, UserId = userId });

        if (transaction == null)
            return new { status = "not_found" };

        return new {
            status = transaction.Status,
            credits = transaction.Credits,
            amount = transaction.Amount
        };
    }
}
