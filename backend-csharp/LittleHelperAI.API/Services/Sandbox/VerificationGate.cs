// Verification Gate Service
// Deterministic pass/fail validation before delivery
using Microsoft.Extensions.Logging;

namespace LittleHelperAI.API.Services.Sandbox;

public interface IVerificationGate
{
    Task<VerificationResult> ValidateAsync(VerificationRequest request);
    Task<bool> QuickValidateAsync(string projectId, List<ProjectFile> files);
}

public class VerificationGate : IVerificationGate
{
    private readonly ILogger<VerificationGate> _logger;
    private readonly VerificationConfig _config;

    public VerificationGate(ILogger<VerificationGate> logger, VerificationConfig? config = null)
    {
        _logger = logger;
        _config = config ?? new VerificationConfig();
    }

    public async Task<VerificationResult> ValidateAsync(VerificationRequest request)
    {
        _logger.LogInformation("Running verification gate for project {ProjectId}", request.ProjectId);

        var result = new VerificationResult
        {
            ProjectId = request.ProjectId,
            ValidatedAt = DateTime.UtcNow
        };

        // 1. Code Quality Gate
        var qualityResult = await ValidateCodeQualityAsync(request);
        result.Checks.Add(qualityResult);

        // 2. Test Coverage Gate
        var testResult = await ValidateTestsAsync(request);
        result.Checks.Add(testResult);

        // 3. Security Gate
        var securityResult = await ValidateSecurityAsync(request);
        result.Checks.Add(securityResult);

        // 4. Build Artifacts Gate
        var buildResult = await ValidateBuildAsync(request);
        result.Checks.Add(buildResult);

        // 5. Runtime Behavior Gate
        var runtimeResult = await ValidateRuntimeAsync(request);
        result.Checks.Add(runtimeResult);

        // Collect all issues
        foreach (var check in result.Checks)
        {
            result.Issues.AddRange(check.Issues);
        }

        // Determine overall pass/fail
        result.Passed = DetermineOverallResult(result);
        result.Score = CalculateScore(result);

        _logger.LogInformation("Verification gate completed. Passed: {Passed}, Score: {Score}, Issues: {Issues}",
            result.Passed, result.Score, result.Issues.Count);

        return result;
    }

    public async Task<bool> QuickValidateAsync(string projectId, List<ProjectFile> files)
    {
        // Quick validation for syntax and basic checks
        var hasCode = files.Any(f => !string.IsNullOrWhiteSpace(f.Content));
        var hasEntry = files.Any(f => 
            f.Path.EndsWith("main.py") || 
            f.Path.EndsWith("index.js") || 
            f.Path.EndsWith("Program.cs") ||
            f.Path.EndsWith("main.go") ||
            f.Path.EndsWith("Main.java"));

        return await Task.FromResult(hasCode && hasEntry);
    }

    private Task<VerificationCheck> ValidateCodeQualityAsync(VerificationRequest request)
    {
        var check = new VerificationCheck
        {
            Name = "Code Quality",
            Category = VerificationCategory.Quality
        };

        if (request.StaticAnalysis == null)
        {
            check.Passed = false;
            check.Issues.Add(new VerificationIssue
            {
                Severity = IssueSeverity.Error,
                Category = VerificationCategory.Quality,
                Message = "Static analysis results not available"
            });
            return Task.FromResult(check);
        }

        // Check syntax validity
        if (!request.StaticAnalysis.SyntaxValid)
        {
            check.Passed = false;
            check.Issues.Add(new VerificationIssue
            {
                Severity = IssueSeverity.Error,
                Category = VerificationCategory.Quality,
                Message = "Code has syntax errors"
            });
            
            foreach (var error in request.StaticAnalysis.SyntaxErrors.Take(5))
            {
                check.Issues.Add(new VerificationIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = VerificationCategory.Quality,
                    Message = error.Message,
                    File = error.File,
                    Line = error.Line
                });
            }
            return Task.FromResult(check);
        }

        // Check lint score
        if (request.StaticAnalysis.OverallScore < _config.MinQualityScore)
        {
            check.Passed = false;
            check.Issues.Add(new VerificationIssue
            {
                Severity = IssueSeverity.Warning,
                Category = VerificationCategory.Quality,
                Message = $"Code quality score ({request.StaticAnalysis.OverallScore:F1}) below threshold ({_config.MinQualityScore})"
            });
        }
        else
        {
            check.Passed = true;
        }

        // Add lint errors as issues
        foreach (var error in request.StaticAnalysis.LintErrors.Where(e => e.Type.Contains("error", StringComparison.OrdinalIgnoreCase)))
        {
            check.Issues.Add(new VerificationIssue
            {
                Severity = IssueSeverity.Error,
                Category = VerificationCategory.Quality,
                Message = error.Message,
                File = error.File,
                Line = error.Line,
                Code = error.Code
            });
        }

        check.Score = request.StaticAnalysis.OverallScore;
        return Task.FromResult(check);
    }

    private Task<VerificationCheck> ValidateTestsAsync(VerificationRequest request)
    {
        var check = new VerificationCheck
        {
            Name = "Test Coverage",
            Category = VerificationCategory.Tests
        };

        if (request.TestResults?.TestResults == null)
        {
            if (_config.RequireTests)
            {
                check.Passed = false;
                check.Issues.Add(new VerificationIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = VerificationCategory.Tests,
                    Message = "No test results available"
                });
            }
            else
            {
                check.Passed = true;
                check.Issues.Add(new VerificationIssue
                {
                    Severity = IssueSeverity.Info,
                    Category = VerificationCategory.Tests,
                    Message = "No tests defined (tests not required)"
                });
            }
            return Task.FromResult(check);
        }

        var testResults = request.TestResults.TestResults;

        // Check if any tests failed
        if (testResults.Failed > 0)
        {
            check.Passed = false;
            check.Issues.Add(new VerificationIssue
            {
                Severity = IssueSeverity.Error,
                Category = VerificationCategory.Tests,
                Message = $"{testResults.Failed} test(s) failed out of {testResults.Total}"
            });
        }
        else if (testResults.Total == 0)
        {
            if (_config.RequireTests)
            {
                check.Passed = false;
                check.Issues.Add(new VerificationIssue
                {
                    Severity = IssueSeverity.Warning,
                    Category = VerificationCategory.Tests,
                    Message = "No tests were executed"
                });
            }
            else
            {
                check.Passed = true;
            }
        }
        else
        {
            check.Passed = true;
        }

        // Calculate pass rate
        if (testResults.Total > 0)
        {
            check.Score = (testResults.Passed * 100.0) / testResults.Total;
            
            if (check.Score < _config.MinTestPassRate)
            {
                check.Passed = false;
                check.Issues.Add(new VerificationIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = VerificationCategory.Tests,
                    Message = $"Test pass rate ({check.Score:F1}%) below threshold ({_config.MinTestPassRate}%)"
                });
            }
        }

        return Task.FromResult(check);
    }

    private Task<VerificationCheck> ValidateSecurityAsync(VerificationRequest request)
    {
        var check = new VerificationCheck
        {
            Name = "Security",
            Category = VerificationCategory.Security,
            Passed = true
        };

        if (request.Files == null || !request.Files.Any())
        {
            return Task.FromResult(check);
        }

        foreach (var file in request.Files)
        {
            var issues = ScanForSecurityIssues(file);
            check.Issues.AddRange(issues);

            if (issues.Any(i => i.Severity == IssueSeverity.Critical))
            {
                check.Passed = false;
            }
        }

        check.Score = check.Passed ? 100 : 0;
        return Task.FromResult(check);
    }

    private List<VerificationIssue> ScanForSecurityIssues(ProjectFile file)
    {
        var issues = new List<VerificationIssue>();
        var content = file.Content;
        var lines = content.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNum = i + 1;

            // Check for hardcoded secrets
            if (ContainsHardcodedSecret(line))
            {
                issues.Add(new VerificationIssue
                {
                    Severity = IssueSeverity.Critical,
                    Category = VerificationCategory.Security,
                    Message = "Potential hardcoded secret detected",
                    File = file.Path,
                    Line = lineNum
                });
            }

            // Check for SQL injection patterns
            if (ContainsSqlInjectionRisk(line))
            {
                issues.Add(new VerificationIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = VerificationCategory.Security,
                    Message = "Potential SQL injection vulnerability",
                    File = file.Path,
                    Line = lineNum
                });
            }

            // Check for dangerous functions
            if (ContainsDangerousFunction(line))
            {
                issues.Add(new VerificationIssue
                {
                    Severity = IssueSeverity.Warning,
                    Category = VerificationCategory.Security,
                    Message = "Use of potentially dangerous function",
                    File = file.Path,
                    Line = lineNum
                });
            }
        }

        return issues;
    }

    private bool ContainsHardcodedSecret(string line)
    {
        var patterns = new[]
        {
            @"['\""](sk-|pk_|api_key|apikey|secret|password|token)[^'""]*['\"]",
            @"(password|secret|api_key|apikey)\s*[=:]\s*['""][^'""]+['""]",
            @"Bearer\s+[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+",
            @"['\""](ghp_|github_pat_|AKIA|ASIA)[^'""]+['\"]"
        };

        return patterns.Any(p => System.Text.RegularExpressions.Regex.IsMatch(line, p, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    private bool ContainsSqlInjectionRisk(string line)
    {
        var patterns = new[]
        {
            @"(SELECT|INSERT|UPDATE|DELETE|DROP).*\+.*\$",
            @"(SELECT|INSERT|UPDATE|DELETE|DROP).*\{[^}]*\}",
            @"f['\""](SELECT|INSERT|UPDATE|DELETE|DROP)",
            @"\.format\s*\(.*\).*WHERE",
            @"string\.Format.*SELECT"
        };

        return patterns.Any(p => System.Text.RegularExpressions.Regex.IsMatch(line, p, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    private bool ContainsDangerousFunction(string line)
    {
        var dangerousFunctions = new[]
        {
            "eval(", "exec(", "compile(", // Python
            "eval(", "Function(", "setTimeout(eval", // JavaScript
            "Runtime.exec(", "ProcessBuilder", // Java
            "Process.Start(", "cmd /c", // C#
            "os.system(", "subprocess.call(", "shell=True"
        };

        return dangerousFunctions.Any(f => line.Contains(f, StringComparison.OrdinalIgnoreCase));
    }

    private Task<VerificationCheck> ValidateBuildAsync(VerificationRequest request)
    {
        var check = new VerificationCheck
        {
            Name = "Build",
            Category = VerificationCategory.Build,
            Passed = true
        };

        if (string.IsNullOrEmpty(request.BuildOutput))
        {
            check.Issues.Add(new VerificationIssue
            {
                Severity = IssueSeverity.Info,
                Category = VerificationCategory.Build,
                Message = "No build output available"
            });
            return Task.FromResult(check);
        }

        // Check for build errors
        var errorPatterns = new[]
        {
            @"error\s*[A-Z]*\d+:",
            @"ERROR:",
            @"Build FAILED",
            @"FAILURE:",
            @"fatal error",
            @"npm ERR!"
        };

        foreach (var pattern in errorPatterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(request.BuildOutput, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                check.Passed = false;
                check.Issues.Add(new VerificationIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = VerificationCategory.Build,
                    Message = "Build contains errors"
                });
                break;
            }
        }

        // Check for warnings
        var warningCount = System.Text.RegularExpressions.Regex.Matches(request.BuildOutput, @"warning\s*[A-Z]*\d*:", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
        if (warningCount > _config.MaxBuildWarnings)
        {
            check.Issues.Add(new VerificationIssue
            {
                Severity = IssueSeverity.Warning,
                Category = VerificationCategory.Build,
                Message = $"Build has {warningCount} warnings (threshold: {_config.MaxBuildWarnings})"
            });
        }

        check.Score = check.Passed ? 100 : 0;
        return Task.FromResult(check);
    }

    private Task<VerificationCheck> ValidateRuntimeAsync(VerificationRequest request)
    {
        var check = new VerificationCheck
        {
            Name = "Runtime",
            Category = VerificationCategory.Runtime,
            Passed = true
        };

        // Check test execution results for runtime errors
        if (request.TestResults != null && !request.TestResults.Success)
        {
            foreach (var error in request.TestResults.Errors)
            {
                if (error.Type.Contains("Runtime") || error.Type.Contains("Exception"))
                {
                    check.Passed = false;
                    check.Issues.Add(new VerificationIssue
                    {
                        Severity = IssueSeverity.Error,
                        Category = VerificationCategory.Runtime,
                        Message = error.Message,
                        File = error.File,
                        Line = error.Line
                    });
                }
            }
        }

        check.Score = check.Passed ? 100 : 0;
        return Task.FromResult(check);
    }

    private bool DetermineOverallResult(VerificationResult result)
    {
        // Must pass all critical checks
        var criticalChecks = new[] { "Code Quality", "Build" };
        foreach (var checkName in criticalChecks)
        {
            var check = result.Checks.FirstOrDefault(c => c.Name == checkName);
            if (check != null && !check.Passed)
                return false;
        }

        // No critical security issues
        if (result.Issues.Any(i => i.Severity == IssueSeverity.Critical))
            return false;

        // Test pass rate must meet threshold
        var testCheck = result.Checks.FirstOrDefault(c => c.Category == VerificationCategory.Tests);
        if (testCheck != null && _config.RequireTests && !testCheck.Passed)
            return false;

        return true;
    }

    private double CalculateScore(VerificationResult result)
    {
        if (!result.Checks.Any())
            return 0;

        var weights = new Dictionary<VerificationCategory, double>
        {
            { VerificationCategory.Quality, 0.3 },
            { VerificationCategory.Tests, 0.3 },
            { VerificationCategory.Security, 0.2 },
            { VerificationCategory.Build, 0.15 },
            { VerificationCategory.Runtime, 0.05 }
        };

        double totalScore = 0;
        double totalWeight = 0;

        foreach (var check in result.Checks)
        {
            if (weights.TryGetValue(check.Category, out var weight))
            {
                totalScore += check.Score * weight;
                totalWeight += weight;
            }
        }

        return totalWeight > 0 ? totalScore / totalWeight : 0;
    }
}

// Models
public class VerificationRequest
{
    public string ProjectId { get; set; } = "";
    public List<ProjectFile>? Files { get; set; }
    public PhaseResult? TestResults { get; set; }
    public StaticAnalysisResult? StaticAnalysis { get; set; }
    public string? BuildOutput { get; set; }
}

public class VerificationResult
{
    public string ProjectId { get; set; } = "";
    public DateTime ValidatedAt { get; set; }
    public bool Passed { get; set; }
    public double Score { get; set; }
    public List<VerificationCheck> Checks { get; set; } = new();
    public List<VerificationIssue> Issues { get; set; } = new();
}

public class VerificationCheck
{
    public string Name { get; set; } = "";
    public VerificationCategory Category { get; set; }
    public bool Passed { get; set; }
    public double Score { get; set; }
    public List<VerificationIssue> Issues { get; set; } = new();
}

public class VerificationIssue
{
    public IssueSeverity Severity { get; set; }
    public VerificationCategory Category { get; set; }
    public string Message { get; set; } = "";
    public string? File { get; set; }
    public int? Line { get; set; }
    public string? Code { get; set; }
}

public enum VerificationCategory
{
    Quality,
    Tests,
    Security,
    Build,
    Runtime
}

public enum IssueSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public class VerificationConfig
{
    public double MinQualityScore { get; set; } = 70;
    public double MinTestPassRate { get; set; } = 80;
    public bool RequireTests { get; set; } = true;
    public int MaxBuildWarnings { get; set; } = 10;
}
