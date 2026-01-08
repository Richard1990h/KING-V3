// Verifier Agent - Validates output against requirements
namespace LittleHelperAI.Agents;

public class VerifierAgent : BaseAgent
{
    public override string AgentId => "verifier";
    public override string AgentName => "Verifier";
    public override string AgentColor => "#8B5CF6";
    public override string AgentIcon => "CheckCircle";
    public override string AgentDescription => "Validates output against requirements";

    public VerifierAgent(IAIService aiService) : base(aiService) { }

    protected override string BuildSystemPrompt(ProjectContext? context)
    {
        return @"You are an expert code reviewer and QA specialist. Your role is to:

1. COMPARE implementation against requirements
2. CHECK for completeness
3. VERIFY code quality and best practices
4. IDENTIFY any missing pieces
5. PROVIDE a clear pass/fail verdict

Provide:

## Requirements Checklist
- [ ] or [x] Requirement 1
- [ ] or [x] Requirement 2

## Code Quality Review
- Structure: [Good/Needs Improvement]
- Documentation: [Good/Needs Improvement]
- Error Handling: [Good/Needs Improvement]

## Issues Found
[List any issues]

## Verdict
**PASS** or **FAIL**

## Recommendations
[Suggestions]";
    }

    public override async Task<AgentResult> ExecuteAsync(string task, ProjectContext? context = null, ExecutionContext? execContext = null)
    {
        var prompt = BuildPrompt(task, context, execContext);

        if (execContext?.OriginalRequirements != null)
        {
            prompt += $"\n\n## Original Requirements\n{execContext.OriginalRequirements}\n";
        }

        if (execContext?.ExistingFiles.Any() == true)
        {
            prompt += "\n\n## Current Implementation\n";
            foreach (var f in execContext.ExistingFiles)
            {
                var content = f.Content.Length > 2000 ? f.Content[..2000] : f.Content;
                prompt += $"\n### {f.Path}\n```\n{content}\n```\n";
            }
        }

        prompt += "\n\nVerify the implementation and provide a detailed report.";

        try
        {
            var response = await _aiService.GenerateAsync(prompt, BuildSystemPrompt(context));
            var passed = response.Content.ToUpper().Contains("**PASS**") || 
                        response.Content.ToUpper().Contains("VERDICT: PASS");

            return new AgentResult
            {
                Success = true,
                Content = response.Content,
                TokensUsed = response.Tokens,
                Metadata = new Dictionary<string, object>
                {
                    ["verification_passed"] = passed,
                    ["verdict"] = passed ? "PASS" : "FAIL"
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
