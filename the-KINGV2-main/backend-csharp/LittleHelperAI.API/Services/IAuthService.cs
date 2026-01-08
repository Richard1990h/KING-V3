// Authentication Service Interface
using LittleHelperAI.API.Controllers;
using LittleHelperAI.Data.Models;
using Microsoft.AspNetCore.Http;

namespace LittleHelperAI.API.Services;

public interface IAuthService
{
    Task<TokenResponse> RegisterAsync(RegisterRequest request, string clientIp);
    Task<TokenResponse> LoginAsync(LoginRequest request, string clientIp, string? userAgent = null);
    Task<UserResponse?> GetUserByIdAsync(string userId);
    Task<object?> GetUserProfileAsync(string userId);
    Task UpdateProfileAsync(string userId, UpdateProfileRequest request);
    Task<string> UploadAvatarAsync(string userId, IFormFile file);
    Task<UserTheme?> GetUserThemeAsync(string userId);
    Task UpdateUserThemeAsync(string userId, UserTheme theme);
    Task ChangePasswordAsync(string userId, string currentPassword, string newPassword);
    Task<List<UserAIProvider>> GetUserAIProvidersAsync(string userId);
    Task<bool> CanUserAddApiKeysAsync(string userId);
    Task AddUserAIProviderAsync(string userId, AddApiKeyRequest request);
    Task DeleteUserAIProviderAsync(string userId, string provider);
    Task<object> GetTosStatusAsync(string userId);
    Task AcceptTosAsync(string userId);
    Task UpdateLanguageAsync(string userId, string language);
    Task<List<object>> GetAllUsersAsync();
    Task<bool> UpdateUserAsync(string userId, AdminUpdateUserRequest request);
    Task<bool> DeleteUserAsync(string userId);
    Task<object> GetSystemStatsAsync();
    Task<DefaultSettings?> GetDefaultSettingsAsync();
    Task UpdateDefaultSettingsAsync(UpdateDefaultsRequest request);
    Task<List<IpRecord>> GetIpRecordsAsync(int limit);
}
