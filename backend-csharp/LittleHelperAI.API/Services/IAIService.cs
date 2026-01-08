// AI Service Interface
using LittleHelperAI.Data.Models;
using LittleHelperAI.Agents;

namespace LittleHelperAI.API.Services;

public interface IAIService
{
    // Health Check
    Task<object> CheckHealthAsync();
    
    // Free AI Providers
    Task<List<FreeAIProvider>> GetFreeAIProvidersAsync();
    Task UpdateFreeAIProviderAsync(string providerId, bool enabled, string? apiKey = null);
    
    // Knowledge Base
    Task<List<KnowledgeBaseEntry>> GetKnowledgeBaseEntriesAsync(int limit);
    Task<KnowledgeBaseEntry> AddKnowledgeEntryAsync(string question, string answer, string? provider);
    Task<bool> DeleteKnowledgeEntryAsync(string entryId);
    Task ValidateKnowledgeEntryAsync(string entryId, bool isValid);
    
    // Agent Activity
    Task<List<AgentActivity>> GetAgentActivityAsync(int limit);
    
    // AI Execution (implements Agents.IAIService methods)
    Task<AIResponse> GenerateAsync(string prompt, string? systemPrompt = null, int maxTokens = 4000);
    IAsyncEnumerable<string> GenerateStreamingAsync(string prompt, string? systemPrompt = null);
}

public record AIHealthStatus(string Database, string LocalLlm, string Stripe, List<string>? AvailableModels);
