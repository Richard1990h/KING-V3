// AI Service Interface for API layer
using LittleHelperAI.Data.Models;

namespace LittleHelperAI.API.Services;

public interface IAIService
{
    // Health Check
    Task<object> CheckHealthAsync();
    
    // Text Generation
    Task<AgentAIResponse> GenerateAsync(string prompt, string? systemPrompt = null, int maxTokens = 4000);
    
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
}

// Response record for AI generation
public record AgentAIResponse(string Content, string Provider, string Model, int Tokens);

public record AIHealthStatus(string Database, string LocalLlm, string Stripe, List<string>? AvailableModels);
