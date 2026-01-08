// Researcher Agent - Gathers knowledge and best practices
namespace LittleHelperAI.Agents;

public class ResearcherAgent : BaseAgent
{
    public override string AgentId => "researcher";
    public override string AgentName => "Researcher";
    public override string AgentColor => "#06B6D4";
    public override string AgentIcon => "Search";
    public override string AgentDescription => "Gathers relevant knowledge, documentation, and best practices";

    public ResearcherAgent(IAIService aiService) : base(aiService) { }

    protected override string BuildSystemPrompt(ProjectContext? context)
    {
        return @"You are an expert technical researcher. Your role is to:

1. GATHER relevant information for the task
2. RESEARCH best practices and patterns
3. IDENTIFY appropriate libraries and frameworks
4. DOCUMENT key considerations and trade-offs
5. PROVIDE actionable recommendations

Format your response as:
## Overview
[Brief overview]

## Recommended Approach
[Detailed approach]

## Libraries & Dependencies
[List with versions]

## Best Practices
[Key practices]

## File Structure
[Recommended structure]

## Code Patterns
[Examples]";
    }

    public override async Task<AgentResult> ExecuteAsync(string task, ProjectContext? context = null, ExecutionContext? execContext = null)
    {
        var prompt = BuildPrompt(task, context, execContext);

        try
        {
            var response = await _aiService.GenerateAsync(prompt, BuildSystemPrompt(context));
            var sectionCount = response.Content.Split("## ").Length - 1;

            return new AgentResult
            {
                Success = true,
                Content = response.Content,
                TokensUsed = response.Tokens,
                Metadata = new Dictionary<string, object>
                {
                    ["research_complete"] = true,
                    ["sections_found"] = sectionCount
                }
            };
        }
        catch (Exception ex)
        {
            return new AgentResult
            {
                Success = false,
                Content = ex.Message,
                Errors = new List<string> { ex.Message }
            };
        }
    }
}
