// Static Analysis Service
// Language-specific static analysis using AST parsing and linters
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace LittleHelperAI.API.Services.Sandbox;

public interface IStaticAnalyzer
{
    Task<StaticAnalysisResult> AnalyzeAsync(string projectId, string language, List<ProjectFile> files);
    Task<StaticAnalysisResult> AnalyzeFileAsync(string language, string filename, string content);
}

public class StaticAnalyzer : IStaticAnalyzer
{
    private readonly ISandboxExecutor _sandbox;
    private readonly ILogger<StaticAnalyzer> _logger;

    public StaticAnalyzer(ISandboxExecutor sandbox, ILogger<StaticAnalyzer> logger)
    {
        _sandbox = sandbox;
        _logger = logger;
    }

    public async Task<StaticAnalysisResult> AnalyzeAsync(string projectId, string language, List<ProjectFile> files)
    {
        _logger.LogInformation("Running static analysis for project {ProjectId}, language: {Language}", projectId, language);

        var result = new StaticAnalysisResult
        {
            ProjectId = projectId,
            Language = language,
            AnalyzedAt = DateTime.UtcNow
        };

        // 1. Run syntax check (AST parse)
        var syntaxResult = await RunSyntaxCheckAsync(language, files);
        result.SyntaxErrors = syntaxResult.Errors;
        result.SyntaxValid = syntaxResult.Success;

        if (!result.SyntaxValid)
        {
            result.OverallScore = 0;
            result.PassesGate = false;
            return result;
        }

        // 2. Run linter in sandbox
        var lintResult = await _sandbox.ExecuteAsync(new ExecutionRequest
        {
            ProjectId = projectId,
            Language = language,
            Files = files,
            Phase = ExecutionPhase.StaticAnalysis,
            TimeoutSeconds = 120
        });

        result.LintErrors = lintResult.Errors;
        result.LintOutput = lintResult.Stdout + lintResult.Stderr;

        // 3. Calculate score and determine pass/fail
        result.OverallScore = CalculateScore(result);
        result.PassesGate = result.SyntaxValid && 
                           !result.LintErrors.Any(e => e.Type == "error" || e.Type == "CompileError");

        _logger.LogInformation("Static analysis complete. Score: {Score}, Passes: {Passes}", 
            result.OverallScore, result.PassesGate);

        return result;
    }

    public async Task<StaticAnalysisResult> AnalyzeFileAsync(string language, string filename, string content)
    {
        var files = new List<ProjectFile> { new() { Path = filename, Content = content } };
        return await AnalyzeAsync($"single-file-{Guid.NewGuid():N}", language, files);
    }

    private async Task<SyntaxCheckResult> RunSyntaxCheckAsync(string language, List<ProjectFile> files)
    {
        var errors = new List<ExecutionError>();

        foreach (var file in files)
        {
            var fileErrors = language.ToLower() switch
            {
                "python" => await CheckPythonSyntaxAsync(file),
                "javascript" or "typescript" => await CheckJavaScriptSyntaxAsync(file),
                "csharp" => await CheckCSharpSyntaxAsync(file),
                "go" => await CheckGoSyntaxAsync(file),
                "java" => await CheckJavaSyntaxAsync(file),
                _ => new List<ExecutionError>()
            };
            errors.AddRange(fileErrors);
        }

        return new SyntaxCheckResult
        {
            Success = !errors.Any(),
            Errors = errors
        };
    }

    private Task<List<ExecutionError>> CheckPythonSyntaxAsync(ProjectFile file)
    {
        var errors = new List<ExecutionError>();
        
        // Basic Python syntax patterns
        var lines = file.Content.Split('\n');
        int indentLevel = 0;
        bool inMultilineString = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                continue;

            // Check for multiline strings
            if (trimmed.Contains("\"\"\"") || trimmed.Contains("'''"))
            {
                var count = System.Text.RegularExpressions.Regex.Matches(line, @"\"\"\"|\'''").Count;
                if (count % 2 == 1)
                    inMultilineString = !inMultilineString;
            }

            if (inMultilineString) continue;

            // Check for common syntax errors
            if (trimmed.EndsWith(":") && !trimmed.StartsWith("if ") && !trimmed.StartsWith("elif ") &&
                !trimmed.StartsWith("else") && !trimmed.StartsWith("for ") && !trimmed.StartsWith("while ") &&
                !trimmed.StartsWith("def ") && !trimmed.StartsWith("class ") && !trimmed.StartsWith("try") &&
                !trimmed.StartsWith("except") && !trimmed.StartsWith("finally") && !trimmed.StartsWith("with ") &&
                !trimmed.StartsWith("async ") && !trimmed.StartsWith("match ") && !trimmed.StartsWith("case "))
            {
                // Might be a dict or slice, not an error
            }

            // Check for unmatched brackets
            var openParens = line.Count(c => c == '(');
            var closeParens = line.Count(c => c == ')');
            var openBrackets = line.Count(c => c == '[');
            var closeBrackets = line.Count(c => c == ']');
            var openBraces = line.Count(c => c == '{');
            var closeBraces = line.Count(c => c == '}');

            // Track across multiple lines - simplified for single line check
            // Real implementation would use AST parser
        }

        return Task.FromResult(errors);
    }

    private Task<List<ExecutionError>> CheckJavaScriptSyntaxAsync(ProjectFile file)
    {
        var errors = new List<ExecutionError>();
        var content = file.Content;

        // Basic bracket matching
        var stack = new Stack<(char bracket, int line, int col)>();
        var lines = content.Split('\n');
        bool inString = false;
        char stringChar = '\0';
        bool inComment = false;
        bool inBlockComment = false;

        for (int lineNum = 0; lineNum < lines.Length; lineNum++)
        {
            var line = lines[lineNum];
            for (int col = 0; col < line.Length; col++)
            {
                var c = line[col];
                var prev = col > 0 ? line[col - 1] : '\0';

                // Handle comments
                if (!inString && !inBlockComment && c == '/' && col + 1 < line.Length && line[col + 1] == '/')
                {
                    break; // Rest of line is comment
                }
                if (!inString && c == '/' && col + 1 < line.Length && line[col + 1] == '*')
                {
                    inBlockComment = true;
                    continue;
                }
                if (inBlockComment && c == '*' && col + 1 < line.Length && line[col + 1] == '/')
                {
                    inBlockComment = false;
                    col++; // Skip the /
                    continue;
                }
                if (inBlockComment) continue;

                // Handle strings
                if (!inString && (c == '"' || c == '\'' || c == '`') && prev != '\\')
                {
                    inString = true;
                    stringChar = c;
                    continue;
                }
                if (inString && c == stringChar && prev != '\\')
                {
                    inString = false;
                    continue;
                }
                if (inString) continue;

                // Track brackets
                if (c == '(' || c == '[' || c == '{')
                {
                    stack.Push((c, lineNum + 1, col + 1));
                }
                else if (c == ')' || c == ']' || c == '}')
                {
                    var expected = c switch { ')' => '(', ']' => '[', '}' => '{', _ => '\0' };
                    if (stack.Count == 0)
                    {
                        errors.Add(new ExecutionError
                        {
                            Type = "SyntaxError",
                            File = file.Path,
                            Line = lineNum + 1,
                            Column = col + 1,
                            Message = $"Unexpected '{c}'"
                        });
                    }
                    else if (stack.Peek().bracket != expected)
                    {
                        var (bracket, l, co) = stack.Pop();
                        errors.Add(new ExecutionError
                        {
                            Type = "SyntaxError",
                            File = file.Path,
                            Line = lineNum + 1,
                            Column = col + 1,
                            Message = $"Mismatched brackets: expected '{(expected == '(' ? ')' : expected == '[' ? ']' : '}')}' to match '{bracket}' at line {l}, column {co}"
                        });
                    }
                    else
                    {
                        stack.Pop();
                    }
                }
            }
        }

        // Report unclosed brackets
        while (stack.Count > 0)
        {
            var (bracket, line, col) = stack.Pop();
            errors.Add(new ExecutionError
            {
                Type = "SyntaxError",
                File = file.Path,
                Line = line,
                Column = col,
                Message = $"Unclosed '{bracket}'"
            });
        }

        return Task.FromResult(errors);
    }

    private Task<List<ExecutionError>> CheckCSharpSyntaxAsync(ProjectFile file)
    {
        // C# syntax checking would ideally use Roslyn
        // For now, do basic bracket matching
        return CheckJavaScriptSyntaxAsync(file); // Similar syntax for brackets
    }

    private Task<List<ExecutionError>> CheckGoSyntaxAsync(ProjectFile file)
    {
        var errors = new List<ExecutionError>();
        var lines = file.Content.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            
            // Check for common Go mistakes
            if (line.StartsWith("func ") && !line.Contains("{") && !line.EndsWith("{"))
            {
                // Function declaration should have brace on same line in Go
                // This is just a warning though
            }
        }

        return Task.FromResult(errors);
    }

    private Task<List<ExecutionError>> CheckJavaSyntaxAsync(ProjectFile file)
    {
        return CheckCSharpSyntaxAsync(file); // Similar syntax
    }

    private double CalculateScore(StaticAnalysisResult result)
    {
        if (!result.SyntaxValid) return 0;

        double score = 100.0;

        // Deduct for errors
        foreach (var error in result.LintErrors)
        {
            if (error.Type.Contains("error", StringComparison.OrdinalIgnoreCase) || 
                error.Type.Contains("CompileError"))
            {
                score -= 10;
            }
            else if (error.Type.Contains("warning", StringComparison.OrdinalIgnoreCase))
            {
                score -= 2;
            }
            else
            {
                score -= 1;
            }
        }

        return Math.Max(0, Math.Min(100, score));
    }
}

public class StaticAnalysisResult
{
    public string ProjectId { get; set; } = "";
    public string Language { get; set; } = "";
    public DateTime AnalyzedAt { get; set; }
    public bool SyntaxValid { get; set; }
    public List<ExecutionError> SyntaxErrors { get; set; } = new();
    public List<ExecutionError> LintErrors { get; set; } = new();
    public string LintOutput { get; set; } = "";
    public double OverallScore { get; set; }
    public bool PassesGate { get; set; }
}

public class SyntaxCheckResult
{
    public bool Success { get; set; }
    public List<ExecutionError> Errors { get; set; } = new();
}
