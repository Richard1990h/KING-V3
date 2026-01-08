// Data Models for LittleHelper AI - Updated with all features
namespace LittleHelperAI.Data.Models;

public class User
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string? DisplayName { get; set; }
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "user";
    public decimal Credits { get; set; } = 100m;
    public bool CreditsEnabled { get; set; } = true;
    public string Plan { get; set; } = "free";
    public string Language { get; set; } = "en";
    public string? AvatarUrl { get; set; }
    public UserTheme? Theme { get; set; }
    public string? RegistrationIp { get; set; }
    public string? LastLoginIp { get; set; }
    public bool TosAccepted { get; set; } = false;
    public DateTime? TosAcceptedAt { get; set; }
    public string? TosVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class UserTheme
{
    public string PrimaryColor { get; set; } = "#d946ef";
    public string SecondaryColor { get; set; } = "#06b6d4";
    public string BackgroundColor { get; set; } = "#030712";
    public string CardColor { get; set; } = "#0B0F19";
    public string TextColor { get; set; } = "#ffffff";
    public string HoverColor { get; set; } = "#a855f7";
    public string CreditsColor { get; set; } = "#d946ef";
    public string? BackgroundImage { get; set; }
}

public class Project
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Language { get; set; } = "Python";
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ProjectFile
{
    public string Id { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string Path { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Todo
{
    public string Id { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string Text { get; set; } = "";
    public bool Completed { get; set; }
    public string Priority { get; set; } = "medium";
    public string? Agent { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Job
{
    public string Id { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string Status { get; set; } = "pending";
    public bool MultiAgentMode { get; set; } = true;
    public string? Tasks { get; set; } // JSON
    public decimal TotalEstimatedCredits { get; set; }
    public decimal CreditsUsed { get; set; }
    public decimal CreditsApproved { get; set; }
    public int CurrentTaskIndex { get; set; } = -1;
    public int ErrorCount { get; set; }
    public int MaxErrors { get; set; } = 5;
    public string? PlannerOutput { get; set; }
    public string? PlannerMetadata { get; set; } // JSON
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class ChatMessage
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string? ProjectId { get; set; }
    public string? ConversationId { get; set; }
    public string? ConversationTitle { get; set; }
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
    public string? AgentId { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public int TokensUsed { get; set; }
    public decimal CreditsDeducted { get; set; }
    public bool MultiAgentMode { get; set; }
    public bool DeletedByUser { get; set; }
    public bool? IsValid { get; set; } = true;  // For knowledge base training
    public DateTime? InvalidatedAt { get; set; }
    public DateTime Timestamp { get; set; }
}

public class UserAIProvider
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Provider { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string? ModelPreference { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreditHistoryEntry
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public decimal Delta { get; set; }
    public string Reason { get; set; } = "";
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public decimal BalanceAfter { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PaymentTransaction
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string PackageId { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "usd";
    public int Credits { get; set; }
    public string Status { get; set; } = "pending";
    public string PaymentStatus { get; set; } = "initiated";
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class SystemSetting
{
    public string SettingKey { get; set; } = "";
    public string SettingValue { get; set; } = "";
    public string SettingType { get; set; } = "string";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ProjectRun
{
    public string Id { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string RunType { get; set; } = "run";
    public string Status { get; set; } = "pending";
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? Output { get; set; }
    public string? Logs { get; set; } // JSON
    public string? Errors { get; set; } // JSON
}

// NEW: Subscription Plans
public class SubscriptionPlan
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal PriceMonthly { get; set; }
    public decimal PriceYearly { get; set; }
    public int DailyCredits { get; set; }
    public int MaxConcurrentWorkspaces { get; set; } = 1;
    public bool AllowsOwnApiKeys { get; set; } = false;
    public List<string> Features { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// NEW: User Subscription
public class UserSubscription
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string PlanId { get; set; } = "";
    public string Status { get; set; } = "active";
    public string? StripeSubscriptionId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? NextBillingDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

// NEW: IP Records for security tracking
public class IpRecord
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public string Action { get; set; } = ""; // "login", "register", "api_call"
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; }
}

// NEW: Free AI Provider Settings (admin-configured)
public class FreeAIProvider
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Provider { get; set; } = ""; // "groq", "together", "huggingface"
    public string ApiKey { get; set; } = "";
    public string? Model { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; } = 0;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// NEW: Credit Package (for add-on purchases)
public class CreditPackage
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Credits { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

// NEW: Default Settings (for new users)
public class DefaultSettings
{
    public string Key { get; set; } = "new_user_defaults";
    public int FreeCredits { get; set; } = 100;
    public string Language { get; set; } = "en";
    public UserTheme Theme { get; set; } = new();
}

// NEW: Agent Activity Log
public class AgentActivity
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string? JobId { get; set; }
    public string AgentId { get; set; } = "";
    public string Action { get; set; } = "";
    public int TokensUsed { get; set; }
    public decimal CreditsUsed { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime Timestamp { get; set; }
}

// Knowledge Base Entry
public class KnowledgeBaseEntry
{
    public string Id { get; set; } = "";
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    public string? Provider { get; set; }
    public int HitCount { get; set; } = 0;
    public bool IsValid { get; set; } = true;
    public DateTime? InvalidatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Terms of Service version tracking
public class TosVersion
{
    public string Id { get; set; } = "";
    public string Version { get; set; } = "1.0";
    public string Content { get; set; } = "";
    public DateTime EffectiveDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
