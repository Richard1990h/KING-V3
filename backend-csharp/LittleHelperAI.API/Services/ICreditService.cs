// Credit Service Interface
using LittleHelperAI.API.Controllers;
using LittleHelperAI.Data.Models;

namespace LittleHelperAI.API.Services;

public interface ICreditService
{
    // Credit Balance & History
    Task<List<CreditHistoryEntry>> GetHistoryAsync(string userId, int limit);
    Task<int> BulkAddCreditsAsync(double amount, List<string>? userIds);
    Task<bool> UserUsesOwnKeyAsync(string userId);
    Task<decimal> DeductCreditsAsync(string userId, decimal amount, string reason);
    
    // Settings
    Task<object> GetSettingsAsync();
    Task<bool> UpdateSettingAsync(string key, string value);
    Task<string?> GetSettingValueAsync(string key);
    
    // Credit Packages
    Dictionary<string, CreditPackageInfo> GetPackages();
    Task<List<CreditPackage>> GetCreditPackagesAsync();
    Task<CreditPackage> CreateCreditPackageAsync(CreateCreditPackageRequest request);
    Task<bool> UpdateCreditPackageAsync(string packageId, UpdateCreditPackageRequest request);
    Task<bool> DeleteCreditPackageAsync(string packageId);
    
    // Subscription Plans
    Task<List<SubscriptionPlan>> GetSubscriptionPlansAsync(bool activeOnly = true);
    Task<SubscriptionPlan> CreateSubscriptionPlanAsync(CreateSubscriptionPlanRequest request);
    Task<bool> UpdateSubscriptionPlanAsync(string planId, UpdateSubscriptionPlanRequest request);
    Task<bool> DeleteSubscriptionPlanAsync(string planId);
    
    // Legacy subscription methods
    Task<int> DistributeDailyCreditsAsync();
    Task<UserSubscriptionResponse> GetUserSubscriptionAsync(string userId);
    Task<WorkspaceLimitResponse> CheckWorkspaceLimitAsync(string userId);
    Task<object> SubscribeUserAsync(string userId, SubscribeRequest request);
    Task<object> PurchaseCreditAddonAsync(string userId, PurchaseAddonRequest request);
    
    // Payments
    Task CreateTransactionAsync(string userId, string sessionId, string packageId, CreditPackageInfo package);
    Task<object> CheckPaymentStatusAsync(string sessionId, string userId);
}

public record CreditPackageInfo(string Name, int Credits, decimal Price);
