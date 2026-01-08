// Data Models for LittleHelper AI - Updated with Dapper column mappings
using System.ComponentModel.DataAnnotations.Schema;

namespace LittleHelperAI.Data.Models;

public class User
{
    [Column("id")]
    public string Id { get; set; } = "";
    
    [Column("email")]
    public string Email { get; set; } = "";
    
    [Column("name")]
    public string Name { get; set; } = "";
    
    [Column("display_name")]
    public string? DisplayName { get; set; }
    
    [Column("password_hash")]
    public string PasswordHash { get; set; } = "";
    
    [Column("role")]
    public string Role { get; set; } = "user";
    
    [Column("credits")]
    public decimal Credits { get; set; } = 100m;
    
    [Column("credits_enabled")]
    public bool CreditsEnabled { get; set; } = true;
    
    [Column("plan")]
    public string Plan { get; set; } = "free";
    
    [Column("language")]
    public string Language { get; set; } = "en";
    
    [Column("avatar_url")]
    public string? AvatarUrl { get; set; }
    
    public UserTheme? Theme { get; set; }
    
    [Column("registration_ip")]
    public string? RegistrationIp { get; set; }
    
    [Column("last_login_ip")]
    public string? LastLoginIp { get; set; }
    
    [Column("tos_accepted")]
    public bool TosAccepted { get; set; } = false;
    
    [Column("tos_accepted_at")]
    public DateTime? TosAcceptedAt { get; set; }
    
    [Column("tos_version")]
    public string? TosVersion { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }
}

public class UserTheme
{
    [Column("user_id")]
    public string? UserId { get; set; }
    
    [Column("primary_color")]
    public string PrimaryColor { get; set; } = "#d946ef";
    
    [Column("secondary_color")]
    public string SecondaryColor { get; set; } = "#06b6d4";
    
    [Column("background_color")]
    public string BackgroundColor { get; set; } = "#030712";
    
    [Column("card_color")]
    public string CardColor { get; set; } = "#0B0F19";
    
    [Column("text_color")]
    public string TextColor { get; set; } = "#ffffff";
    
    [Column("hover_color")]
    public string HoverColor { get; set; } = "#a855f7";
    
    [Column("credits_color")]
    public string CreditsColor { get; set; } = "#d946ef";
    
    [Column("background_image")]
    public string? BackgroundImage { get; set; }
}

public class Project
{
    [Column("id")]
    public string Id { get; set; } = "";
    
    [Column("user_id")]
    public string UserId { get; set; } = "";
    
    [Column("name")]
    public string Name { get; set; } = "";
    
    [Column("description")]
    public string Description { get; set; } = "";
    
    [Column("language")]
    public string Language { get; set; } = "Python";
    
    [Column("status")]
    public string Status { get; set; } = "active";
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

public class ProjectFile
{
    [Column("id")]
    public string Id { get; set; } = "";
    
    [Column("project_id")]
    public string ProjectId { get; set; } = "";
    
    [Column("path")]
    public string Path { get; set; } = "";
    
    [Column("content")]
    public string Content { get; set; } = "";
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

public class Todo
{
    [Column("id")]
    public string Id { get; set; } = "";
    
    [Column("project_id")]
    public string ProjectId { get; set; } = "";
    
    [Column("text")]
    public string Text { get; set; } = "";
    
    [Column("completed")]
    public bool Completed { get; set; }
    
    [Column("priority")]
    public string Priority { get; set; } = "medium";
    
    [Column("agent")]
    public string? Agent { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

public class Job
{
    [Column("id")]
    public string Id { get; set; } = "";
    
    [Column("project_id")]
    public string ProjectId { get; set; } = "";
    
    [Column("user_id")]
    public string UserId { get; set; } = "";
    
    [Column("prompt")]
    public string Prompt { get; set; } = "";
    
    [Column("status")]
    public string Status { get; set; } = "pending";
    
    [Column("multi_agent_mode")]
    public bool MultiAgentMode { get; set; } = true;
    
    [Column("tasks")]
    public string? Tasks { get; set; } // JSON
    
    [Column("total_estimated_credits")]
    public decimal TotalEstimatedCredits { get; set; }
    
    [Column("credits_used")]
    public decimal CreditsUsed { get; set; }
    
    [Column("credits_approved")]
    public decimal CreditsApproved { get; set; }
    
    [Column("current_task_index")]
    public int CurrentTaskIndex { get; set; } = -1;
    
    [Column("error_count")]
    public int ErrorCount { get; set; }
    
    [Column("max_errors")]
    public int MaxErrors { get; set; } = 5;
    
    [Column("planner_output")]
    public string? PlannerOutput { get; set; }
    
    [Column("planner_metadata")]
    public string? PlannerMetadata { get; set; } // JSON
    
    [Column("error")]
    public string? Error { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
    
    [Column("started_at")]
    public DateTime? StartedAt { get; set; }
    
    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }
}

public class ChatMessage
{
    [Column("id")]
    public string Id { get; set; } = "";
    
    [Column("user_id")]
    public string UserId { get; set; } = "";
    
    [Column("project_id")]
    public string? ProjectId { get; set; }
    
    [Column("conversation_id")]
    public string? ConversationId { get; set; }
    
    [Column("conversation_title")]
    public string? ConversationTitle { get; set; }
    
    [Column("role")]
    public string Role { get; set; } = "user";
    
    [Column("content")]
    public string Content { get; set; } = "";
    
    [Column("agent_id")]
    public string? AgentId { get; set; }
    
    [Column("provider")]
    public string? Provider { get; set; }
    
    [Column("model")]
    public string? Model { get; set; }
    
    [Column("tokens_used")]
    public int TokensUsed { get; set; }
    
    [Column("credits_deducted")]
    public decimal CreditsDeducted { get; set; }
    
    [Column("multi_agent_mode")]
    public bool MultiAgentMode { get; set; }
    
    [Column("deleted_by_user")]
    public bool DeletedByUser { get; set; }
    
    [Column("is_valid")]
    public bool? IsValid { get; set; } = true;  // For knowledge base training
    
    [Column("invalidated_at")]
    public DateTime? InvalidatedAt { get; set; }
    
    [Column("timestamp")]
    public DateTime Timestamp { get; set; }
}

public class UserAIProvider
{
    [Column("id")]
    public string Id { get; set; } = "";
    
    [Column("user_id")]
    public string UserId { get; set; } = "";
    
    [Column("provider")]
    public string Provider { get; set; } = "";
    
    [Column("api_key")]
    public string ApiKey { get; set; } = "";
    
    [Column("model_preference")]
    public string? ModelPreference { get; set; }
    
    [Column("is_active")]
    public bool IsActive { get; set; } = true;
    
    [Column("is_default")]
    public bool IsDefault { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

public class CreditHistoryEntry
{
    [Column("id")]
    public string Id { get; set; } = "";
    
    [Column("user_id")]
    public string UserId { get; set; } = "";
    
    [Column("delta")]
    public decimal Delta { get; set; }
    
    [Column("reason")]
    public string Reason { get; set; } = "";
    
    [Column("reference_type")]
    public string? ReferenceType { get; set; }
    
    [Column("reference_id")]
    public string? ReferenceId { get; set; }
    
    [Column("balance_after")]
    public decimal BalanceAfter { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

public class PaymentTransaction
{
    [Column("id")]
    public string Id { get; set; } = "";
    
    [Column("user_id")]
    public string UserId { get; set; } = "";
    
    [Column("session_id")]
    public string SessionId { get; set; } = "";
    
    [Column("package_id")]
    public string PackageId { get; set; } = "";
    
    [Column("amount")]
    public decimal Amount { get; set; }
    
    [Column("currency")]
    public string Currency { get; set; } = "usd";
    
    [Column("credits")]
    public int Credits { get; set; }
    
    [Column("status")]
    public string Status { get; set; } = "pending";
    
    [Column("payment_status")]
    public string PaymentStatus { get; set; } = "initiated";
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }
}

public class SystemSetting
{
    [Column("setting_key")]
    public string SettingKey { get; set; } = "";
    
    [Column("setting_value")]
    public string SettingValue { get; set; } = "";
    
    [Column("setting_type")]
    public string SettingType { get; set; } = "string";
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

public class ProjectRun
{
    [Column("id")]
    public string Id { get; set; } = "";
    
    [Column("project_id")]
    public string ProjectId { get; set; } = "";
    
    [Column("run_type")]
    public string RunType { get; set; } = "run";
    
    [Column("status")]
    public string Status { get; set; } = "pending";
    
    [Column("started_at")]
    public DateTime StartedAt { get; set; }
    
    [Column("ended_at")]
    public DateTime? EndedAt { get; set; }
    
    [Column("output")]
    public string? Output { get; set; }
    
    [Column("logs")]
    public string? Logs { get; set; } // JSON
    
    [Column("errors")]
    public string? Errors { get; set; } // JSON
}

// Subscription Plans
public class SubscriptionPlan
{
    [Column("id")]
    public string Id { get; set; } = "";
    
    [Column("name")]
    public string Name { get; set; } = "";
    
    [Column("description")]
    public string Description { get; set; } = "";
    
    [Column("price_monthly")]
    public decimal PriceMonthly { get; set; }
    
    [Column("price_yearly")]
    public decimal PriceYearly { get; set; }
    
    [Column("daily_credits")]
    public int DailyCredits { get; set; }
    
    [Column("max_concurrent_workspaces")]
    public int MaxConcurrentWorkspaces { get; set; } = 1;
    
    [Column("allows_own_api_keys")]
    public bool AllowsOwnApiKeys { get; set; } = false;
    
    [Column("features")]
    public List<string> Features { get; set; } = new();
    
    [Column("is_active")]
    public bool IsActive { get; set; } = true;
    
    [Column("sort_order")]
    public int SortOrder { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

// User Subscription
public class UserSubscription
{
    [Column("id")]
    public string Id { get; set; } = "";
    
    [Column("user_id")]
    public string UserId { get; set; } = "";
    
    [Column("plan_id")]
    public string PlanId { get; set; } = "";
    
    [Column("status")]
    public string Status { get; set; } = "active";
    
    [Column("stripe_subscription_id")]
    public string? StripeSubscriptionId { get; set; }
    
    [Column("start_date")]
    public DateTime StartDate { get; set; }
    
    [Column("end_date")]
    public DateTime? EndDate { get; set; }
    
    [Column("next_billing_date")]
    public DateTime? NextBillingDate { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

// IP Records for security tracking
public class IpRecord
{
    [Column("id")]
    public string Id { get; set; } = "";
    
    [Column("user_id")]
    public string UserId { get; set; } = "";
    
    [Column("ip_address")]
    public string IpAddress { get; set; } = "";
    
    [Column("action")]
    public string Action { get; set; } = ""; // "login", "register", "api_call"
    
    [Column("user_agent")]
    public string? UserAgent { get; set; }
    
    [Column("timestamp")]
    public DateTime Timestamp { get; set; }
}

// Free AI Provider Settings (admin-configured)
public class FreeAIProvider
{
    [Column("id")]
    public string Id { get; set; } = "";
    
    [Column("name")]
    public string Name { get; set; } = "";
    
    [Column("provider")]
    public string Provider { get; set; } = ""; // "groq", "together", "huggingface"
    
    [Column("api_key")]
    public string ApiKey { get; set; } = "";
    
    [Column("model")]
    public string? Model { get; set; }
    
    [Column("is_enabled")]
    public bool IsEnabled { get; set; } = true;
    
    [Column("priority")]
    public int Priority { get; set; } = 0;
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

// Credit Package (for add-on purchases)
public class CreditPackage
{
    [Column("id")]
    public string Id { get; set; } = "";
    
    [Column("name")]
    public string Name { get; set; } = "";
    
    [Column("credits")]
    public int Credits { get; set; }
    
    [Column("price")]
    public decimal Price { get; set; }
    
    [Column("is_active")]
    public bool IsActive { get; set; } = true;
    
    [Column("sort_order")]
    public int SortOrder { get; set; }
}

// Default Settings (for new users)
public class DefaultSettings
{
    [Column("setting_key")]
    public string Key { get; set; } = "new_user_defaults";
    
    [Column("free_credits")]
    public int FreeCredits { get; set; } = 100;
    
    [Column("language")]
    public string Language { get; set; } = "en";
    
    [Column("theme_json")]
    public UserTheme Theme { get; set; } = new();
}

// Agent Activity Log
public class AgentActivity
{
    [Column("id")]
    public string Id { get; set; } = "";
    
    [Column("user_id")]
    public string UserId { get; set; } = "";
    
    [Column("project_id")]
    public string ProjectId { get; set; } = "";
    
    [Column("job_id")]
    public string? JobId { get; set; }
    
    [Column("agent_id")]
    public string AgentId { get; set; } = "";
    
    [Column("action")]
    public string Action { get; set; } = "";
    
    [Column("tokens_used")]
    public int TokensUsed { get; set; }
    
    [Column("credits_used")]
    public decimal CreditsUsed { get; set; }
    
    [Column("success")]
    public bool Success { get; set; }
    
    [Column("error")]
    public string? Error { get; set; }
    
    [Column("timestamp")]
    public DateTime Timestamp { get; set; }
}

// Knowledge Base Entry
public class KnowledgeBaseEntry
{
    [Column("id")]
    public string Id { get; set; } = "";
    
    [Column("question")]
    public string Question { get; set; } = "";
    
    [Column("answer")]
    public string Answer { get; set; } = "";
    
    [Column("provider")]
    public string? Provider { get; set; }
    
    [Column("usage_count")]
    public int HitCount { get; set; } = 0;
    
    [Column("is_valid")]
    public bool IsValid { get; set; } = true;
    
    [Column("invalidated_at")]
    public DateTime? InvalidatedAt { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

// Terms of Service version tracking
public class TosVersion
{
    [Column("id")]
    public string Id { get; set; } = "";
    
    [Column("version")]
    public string Version { get; set; } = "1.0";
    
    [Column("content")]
    public string Content { get; set; } = "";
    
    [Column("effective_date")]
    public DateTime EffectiveDate { get; set; }
    
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}


// Site Settings - Admin-configurable site-wide settings
public class SiteSettings
{
    [Column("id")]
    public string Id { get; set; } = "default";
    
    [Column("announcement_enabled")]
    public bool AnnouncementEnabled { get; set; } = false;
    
    [Column("announcement_message")]
    public string? AnnouncementMessage { get; set; }
    
    [Column("announcement_type")]
    public string AnnouncementType { get; set; } = "info"; // info, warning, success, error
    
    [Column("maintenance_mode")]
    public bool MaintenanceMode { get; set; } = false;
    
    [Column("admins_auto_friend")]
    public bool AdminsAutoFriend { get; set; } = true;
    
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
    
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }
}

// Site Settings Request/Response DTOs
public class SiteSettingsRequest
{
    public bool? AnnouncementEnabled { get; set; }
    public string? AnnouncementMessage { get; set; }
    public string? AnnouncementType { get; set; }
    public bool? MaintenanceMode { get; set; }
    public bool? AdminsAutoFriend { get; set; }
}

public class PublicSiteSettings
{
    public bool AnnouncementEnabled { get; set; }
    public string? AnnouncementMessage { get; set; }
    public string AnnouncementType { get; set; } = "info";
    public bool MaintenanceMode { get; set; }
}
