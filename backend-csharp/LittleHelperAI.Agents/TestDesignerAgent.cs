// Test Designer Agent - Creates comprehensive test cases
using System.Text.RegularExpressions;

namespace LittleHelperAI.Agents;

public class TestDesignerAgent : BaseAgent
{
    public override string AgentId => "test_designer";
    public override string AgentName => "Test Designer";
    public override string AgentColor => "#F59E0B";
    public override string AgentIcon => "TestTube";
    public override string AgentDescription => "Creates comprehensive test cases and test files";

    public TestDesignerAgent(IAIService aiService) : base(aiService) { }

    protected override string BuildSystemPrompt(ProjectContext? context)
    {
        var language = context?.Language ?? "Python";
        var frameworks = new Dictionary<string, string>
        {
            ["Python"] = "pytest",
            ["JavaScript"] = "Jest",
            ["TypeScript"] = "Jest",
            ["Java"] = "JUnit 5",
            ["C#"] = "xUnit",
            ["Go"] = "testing",
            ["Rust"] = "cargo test"
        };
        var framework = frameworks.GetValueOrDefault(language, "appropriate testing framework");

        return $@"You are an expert test engineer specializing in {language}. Your role is to:

1. CREATE comprehensive test cases using {framework}
2. COVER happy paths, edge cases, and error conditions
3. WRITE clear, maintainable test code
4. INCLUDE setup and teardown when needed
5. TEST all public interfaces

For each test file, use this format:

### test_filename.ext
```language
test code here
```

TEST CATEGORIES:
- Unit tests for individual functions/methods
- Integration tests for component interactions
- Edge case tests
- Error handling tests";
    }

    public override async Task<AgentResult> ExecuteAsync(string task, ProjectContext? context = null, ExecutionContext? execContext = null)
    {
        var prompt = BuildPrompt(task, context, execContext);

        if (execContext?.ExistingFiles.Any() == true)
        {
            prompt += "\n\n## Files to Test\n";
            foreach (var f in execContext.ExistingFiles)
            {
                var content = f.Content.Length > 1000 ? f.Content[..1000] : f.Content;
                prompt += $"\n### {f.Path}\n```\n{content}\n```\n";
            }
        }

        prompt += "\n\nCreate comprehensive tests for the above code.";

        try
        {
            var response = await _aiService.GenerateAsync(prompt, BuildSystemPrompt(context));
            var files = ExtractCodeBlocks(response.Content);

            return new AgentResult
            {
                Success = true,
                Content = response.Content,
                TokensUsed = response.Tokens,
                FilesCreated = files,
                Metadata = new Dictionary<string, object>
                {
                    ["test_files_count"] = files.Count,
                    ["test_file_names"] = files.Select(f => f.Path).ToList()
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
