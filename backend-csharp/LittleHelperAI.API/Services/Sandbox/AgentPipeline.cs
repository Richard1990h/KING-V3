// Closed-Loop Agent Pipeline
// Orchestrates code generation, validation, and self-correction
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace LittleHelperAI.API.Services.Sandbox;

public interface IAgentPipeline
{
    Task<PipelineResult> ExecutePipelineAsync(PipelineRequest request, CancellationToken ct = default);
    Task<PipelineResult> RunSinglePhaseAsync(string projectId, PipelinePhase phase, CancellationToken ct = default);
}

public class AgentPipeline : IAgentPipeline
{
    private readonly ISandboxExecutor _sandbox;
    private readonly IStaticAnalyzer _staticAnalyzer;
    private readonly ITestGenerator _testGenerator;
    private readonly IVerificationGate _verificationGate;
    private readonly IRateLimiter _rateLimiter;
    private readonly ICodeGeneratorService _codeGenerator;
    private readonly ILogger<AgentPipeline> _logger;

    private const int MAX_SELF_CORRECTION_ATTEMPTS = 5;
    private const int MAX_TOTAL_ITERATIONS = 10;

    public AgentPipeline(
        ISandboxExecutor sandbox,
        IStaticAnalyzer staticAnalyzer,
        ITestGenerator testGenerator,
        IVerificationGate verificationGate,
        IRateLimiter rateLimiter,
        ICodeGeneratorService codeGenerator,
        ILogger<AgentPipeline> logger)
    {
        _sandbox = sandbox;
        _staticAnalyzer = staticAnalyzer;
        _testGenerator = testGenerator;
        _verificationGate = verificationGate;
        _rateLimiter = rateLimiter;
        _codeGenerator = codeGenerator;
        _logger = logger;
    }

    public async Task<PipelineResult> ExecutePipelineAsync(PipelineRequest request, CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new PipelineResult
        {
            ProjectId = request.ProjectId,
            RequestId = Guid.NewGuid().ToString(),
            StartedAt = startTime
        };

        _logger.LogInformation("Starting pipeline for project {ProjectId}, prompt: {Prompt}", 
            request.ProjectId, request.Prompt?.Substring(0, Math.Min(100, request.Prompt?.Length ?? 0)));

        try
        {
            // 1. Check rate limits
            var rateLimitCheck = await _rateLimiter.CheckLimitAsync(request.ProjectId, request.UserId);
            if (!rateLimitCheck.Allowed)
            {
                result.Status = PipelineStatus.RateLimited;
                result.ErrorMessage = rateLimitCheck.Message;
                result.CompletedAt = DateTime.UtcNow;
                return result;
            }

            // 2. Main pipeline loop with self-correction
            int iteration = 0;
            var currentFiles = request.Files.ToList();
            var errorHistory = new List<PipelineError>();

            while (iteration < MAX_TOTAL_ITERATIONS)
            {
                iteration++;
                result.Iterations = iteration;
                _logger.LogInformation("Pipeline iteration {Iteration}/{Max}", iteration, MAX_TOTAL_ITERATIONS);

                // Phase 1: Code Generation (if first iteration or needs correction)
                if (iteration == 1 || errorHistory.Any())
                {
                    var genResult = await GenerateCodeAsync(request, currentFiles, errorHistory, ct);
                    result.Phases.Add(genResult);
                    
                    if (!genResult.Success)
                    {
                        result.Status = PipelineStatus.GenerationFailed;
                        break;
                    }
                    
                    currentFiles = genResult.OutputFiles ?? currentFiles;
                }

                // Phase 2: Static Analysis
                var analysisResult = await RunStaticAnalysisAsync(request.ProjectId, request.Language, currentFiles, ct);
                result.Phases.Add(analysisResult);

                if (!analysisResult.Success)
                {
                    errorHistory.AddRange(analysisResult.Errors);
                    if (errorHistory.Count >= MAX_SELF_CORRECTION_ATTEMPTS)
                    {
                        result.Status = PipelineStatus.StaticAnalysisFailed;
                        result.ErrorMessage = "Max self-correction attempts reached during static analysis";
                        break;
                    }
                    continue; // Loop back for self-correction
                }

                // Phase 3: Dependency Resolution & Build
                var buildResult = await RunBuildAsync(request.ProjectId, request.Language, currentFiles, ct);
                result.Phases.Add(buildResult);

                if (!buildResult.Success)
                {
                    errorHistory.AddRange(buildResult.Errors);
                    if (errorHistory.Count >= MAX_SELF_CORRECTION_ATTEMPTS)
                    {
                        result.Status = PipelineStatus.BuildFailed;
                        result.ErrorMessage = "Max self-correction attempts reached during build";
                        break;
                    }
                    continue;
                }

                // Phase 4: Test Generation
                var testGenResult = await GenerateTestsAsync(request.ProjectId, request.Language, currentFiles, ct);
                result.Phases.Add(testGenResult);

                if (testGenResult.Success && testGenResult.OutputFiles != null)
                {
                    currentFiles.AddRange(testGenResult.OutputFiles);
                }

                // Phase 5: Test Execution
                var testRunResult = await RunTestsAsync(request.ProjectId, request.Language, currentFiles, ct);
                result.Phases.Add(testRunResult);

                if (!testRunResult.Success)
                {
                    errorHistory.AddRange(testRunResult.Errors);
                    if (errorHistory.Count >= MAX_SELF_CORRECTION_ATTEMPTS)
                    {
                        result.Status = PipelineStatus.TestsFailed;
                        result.ErrorMessage = "Max self-correction attempts reached during tests";
                        break;
                    }
                    continue;
                }

                // Phase 6: Runtime Execution (optional)
                if (request.RunAfterBuild)
                {
                    var runResult = await RunExecutionAsync(request.ProjectId, request.Language, currentFiles, request.EntryPoint, ct);
                    result.Phases.Add(runResult);

                    if (!runResult.Success)
                    {
                        errorHistory.AddRange(runResult.Errors);
                        if (errorHistory.Count >= MAX_SELF_CORRECTION_ATTEMPTS)
                        {
                            result.Status = PipelineStatus.RuntimeFailed;
                            result.ErrorMessage = "Max self-correction attempts reached during runtime";
                            break;
                        }
                        continue;
                    }
                }

                // Phase 7: Verification Gate
                var verificationResult = await _verificationGate.ValidateAsync(new VerificationRequest
                {
                    ProjectId = request.ProjectId,
                    Files = currentFiles,
                    TestResults = testRunResult,
                    StaticAnalysis = analysisResult.AnalysisResult,
                    BuildOutput = buildResult.Output
                });

                result.Phases.Add(new PhaseResult
                {
                    Phase = PipelinePhase.Verification,
                    Success = verificationResult.Passed,
                    Duration = TimeSpan.Zero,
                    Output = JsonSerializer.Serialize(verificationResult)
                });

                if (verificationResult.Passed)
                {
                    result.Status = PipelineStatus.Success;
                    result.OutputFiles = currentFiles;
                    result.VerificationResult = verificationResult;
                    break;
                }
                else
                {
                    errorHistory.AddRange(verificationResult.Issues.Select(i => new PipelineError
                    {
                        Phase = PipelinePhase.Verification,
                        Type = i.Severity.ToString(),
                        Message = i.Message,
                        File = i.File,
                        Line = i.Line
                    }));

                    if (iteration >= MAX_TOTAL_ITERATIONS)
                    {
                        result.Status = PipelineStatus.VerificationFailed;
                        result.ErrorMessage = "Verification gate failed after max iterations";
                    }
                }
            }

            // Record cost
            result.TotalCost = await _rateLimiter.RecordUsageAsync(request.ProjectId, request.UserId, result);

        }
        catch (OperationCanceledException)
        {
            result.Status = PipelineStatus.Cancelled;
            result.ErrorMessage = "Pipeline was cancelled";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline failed for project {ProjectId}", request.ProjectId);
            result.Status = PipelineStatus.InternalError;
            result.ErrorMessage = ex.Message;
        }

        result.CompletedAt = DateTime.UtcNow;
        result.TotalDuration = result.CompletedAt.Value - result.StartedAt;

        _logger.LogInformation("Pipeline completed for project {ProjectId}. Status: {Status}, Iterations: {Iterations}, Duration: {Duration}ms",
            request.ProjectId, result.Status, result.Iterations, result.TotalDuration?.TotalMilliseconds);

        return result;
    }

    public async Task<PipelineResult> RunSinglePhaseAsync(string projectId, PipelinePhase phase, CancellationToken ct = default)
    {
        var result = new PipelineResult
        {
            ProjectId = projectId,
            RequestId = Guid.NewGuid().ToString(),
            StartedAt = DateTime.UtcNow
        };

        // This would load files from the project and run a single phase
        // Simplified for now
        result.Status = PipelineStatus.Success;
        result.CompletedAt = DateTime.UtcNow;

        return await Task.FromResult(result);
    }

    private async Task<PhaseResult> GenerateCodeAsync(
        PipelineRequest request, 
        List<ProjectFile> currentFiles, 
        List<PipelineError> errorHistory, 
        CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        var result = new PhaseResult { Phase = PipelinePhase.CodeGeneration };

        try
        {
            // Build prompt with error context for self-correction
            var prompt = request.Prompt ?? "";
            
            if (errorHistory.Any())
            {
                var errorContext = new System.Text.StringBuilder();
                errorContext.AppendLine("\n\n--- ERRORS TO FIX ---");
                errorContext.AppendLine("The previous code had the following errors that need to be fixed:");
                
                foreach (var error in errorHistory.TakeLast(10))
                {
                    errorContext.AppendLine($"- [{error.Type}] {error.File ?? "unknown"}:{error.Line?.ToString() ?? "?"}: {error.Message}");
                    if (!string.IsNullOrEmpty(error.StackTrace))
                    {
                        errorContext.AppendLine($"  Stack: {error.StackTrace.Substring(0, Math.Min(200, error.StackTrace.Length))}...");
                    }
                }
                
                errorContext.AppendLine("\nPlease fix these errors in the generated code.");
                prompt += errorContext.ToString();
            }

            // Call AI code generator
            var generatedCode = await _codeGenerator.GenerateCodeAsync(new CodeGenerationRequest
            {
                ProjectId = request.ProjectId,
                Language = request.Language,
                Prompt = prompt,
                ExistingFiles = currentFiles,
                Context = request.Context
            }, ct);

            result.Success = generatedCode.Success;
            result.OutputFiles = generatedCode.Files;
            result.Output = generatedCode.Explanation;
            result.TokensUsed = generatedCode.TokensUsed;

            if (!generatedCode.Success)
            {
                result.Errors.Add(new PipelineError
                {
                    Phase = PipelinePhase.CodeGeneration,
                    Type = "GenerationError",
                    Message = generatedCode.ErrorMessage ?? "Code generation failed"
                });
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(new PipelineError
            {
                Phase = PipelinePhase.CodeGeneration,
                Type = "Exception",
                Message = ex.Message,
                StackTrace = ex.StackTrace
            });
        }

        result.Duration = DateTime.UtcNow - startTime;
        return result;
    }

    private async Task<PhaseResult> RunStaticAnalysisAsync(
        string projectId, 
        string language, 
        List<ProjectFile> files, 
        CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        var result = new PhaseResult { Phase = PipelinePhase.StaticAnalysis };

        try
        {
            var analysis = await _staticAnalyzer.AnalyzeAsync(projectId, language, files);
            
            result.Success = analysis.PassesGate;
            result.Output = analysis.LintOutput;
            result.AnalysisResult = analysis;

            foreach (var error in analysis.SyntaxErrors.Concat(analysis.LintErrors))
            {
                result.Errors.Add(new PipelineError
                {
                    Phase = PipelinePhase.StaticAnalysis,
                    Type = error.Type,
                    Message = error.Message,
                    File = error.File,
                    Line = error.Line,
                    Column = error.Column,
                    Code = error.Code
                });
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(new PipelineError
            {
                Phase = PipelinePhase.StaticAnalysis,
                Type = "Exception",
                Message = ex.Message
            });
        }

        result.Duration = DateTime.UtcNow - startTime;
        return result;
    }

    private async Task<PhaseResult> RunBuildAsync(
        string projectId, 
        string language, 
        List<ProjectFile> files, 
        CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        var result = new PhaseResult { Phase = PipelinePhase.Build };

        try
        {
            var execResult = await _sandbox.ExecuteAsync(new ExecutionRequest
            {
                ProjectId = projectId,
                Language = language,
                Files = files,
                Phase = ExecutionPhase.Build,
                TimeoutSeconds = 300 // Build can take longer
            }, ct);

            result.Success = execResult.Success;
            result.Output = execResult.Stdout + "\n" + execResult.Stderr;
            result.ExitCode = execResult.ExitCode;

            foreach (var error in execResult.Errors)
            {
                result.Errors.Add(new PipelineError
                {
                    Phase = PipelinePhase.Build,
                    Type = error.Type,
                    Message = error.Message,
                    File = error.File,
                    Line = error.Line,
                    Column = error.Column,
                    StackTrace = error.StackTrace
                });
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(new PipelineError
            {
                Phase = PipelinePhase.Build,
                Type = "Exception",
                Message = ex.Message
            });
        }

        result.Duration = DateTime.UtcNow - startTime;
        return result;
    }

    private async Task<PhaseResult> GenerateTestsAsync(
        string projectId, 
        string language, 
        List<ProjectFile> files, 
        CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        var result = new PhaseResult { Phase = PipelinePhase.TestGeneration };

        try
        {
            var tests = await _testGenerator.GenerateTestsAsync(projectId, language, files);
            
            result.Success = true;
            result.Output = $"Generated {tests.TestCases.Count} test cases";
            result.OutputFiles = new List<ProjectFile>
            {
                new() { Path = tests.TestFileName, Content = tests.TestFileContent }
            };
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(new PipelineError
            {
                Phase = PipelinePhase.TestGeneration,
                Type = "Exception",
                Message = ex.Message
            });
        }

        result.Duration = DateTime.UtcNow - startTime;
        return result;
    }

    private async Task<PhaseResult> RunTestsAsync(
        string projectId, 
        string language, 
        List<ProjectFile> files, 
        CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        var result = new PhaseResult { Phase = PipelinePhase.TestExecution };

        try
        {
            var execResult = await _sandbox.ExecuteAsync(new ExecutionRequest
            {
                ProjectId = projectId,
                Language = language,
                Files = files,
                Phase = ExecutionPhase.Test,
                TimeoutSeconds = 180
            }, ct);

            result.Success = execResult.Success;
            result.Output = execResult.Stdout + "\n" + execResult.Stderr;
            result.ExitCode = execResult.ExitCode;

            // Parse test results
            result.TestResults = ParseTestResults(execResult.Stdout, execResult.Stderr, language);

            foreach (var error in execResult.Errors)
            {
                result.Errors.Add(new PipelineError
                {
                    Phase = PipelinePhase.TestExecution,
                    Type = error.Type,
                    Message = error.Message,
                    File = error.File,
                    Line = error.Line,
                    StackTrace = error.StackTrace
                });
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(new PipelineError
            {
                Phase = PipelinePhase.TestExecution,
                Type = "Exception",
                Message = ex.Message
            });
        }

        result.Duration = DateTime.UtcNow - startTime;
        return result;
    }

    private async Task<PhaseResult> RunExecutionAsync(
        string projectId, 
        string language, 
        List<ProjectFile> files, 
        string? entryPoint,
        CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        var result = new PhaseResult { Phase = PipelinePhase.Execution };

        try
        {
            var execResult = await _sandbox.ExecuteAsync(new ExecutionRequest
            {
                ProjectId = projectId,
                Language = language,
                Files = files,
                Phase = ExecutionPhase.Run,
                EntryPoint = entryPoint,
                TimeoutSeconds = 60
            }, ct);

            result.Success = execResult.Success;
            result.Output = execResult.Stdout;
            result.ExitCode = execResult.ExitCode;

            if (!string.IsNullOrEmpty(execResult.Stderr))
            {
                result.Output += "\n--- STDERR ---\n" + execResult.Stderr;
            }

            foreach (var error in execResult.Errors)
            {
                result.Errors.Add(new PipelineError
                {
                    Phase = PipelinePhase.Execution,
                    Type = error.Type,
                    Message = error.Message,
                    File = error.File,
                    Line = error.Line,
                    StackTrace = error.StackTrace
                });
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(new PipelineError
            {
                Phase = PipelinePhase.Execution,
                Type = "Exception",
                Message = ex.Message
            });
        }

        result.Duration = DateTime.UtcNow - startTime;
        return result;
    }

    private TestResults ParseTestResults(string stdout, string stderr, string language)
    {
        var results = new TestResults();
        var combined = stdout + "\n" + stderr;

        // Parse based on language/framework
        switch (language.ToLower())
        {
            case "python":
                // pytest output: "5 passed, 2 failed"
                var pytestMatch = System.Text.RegularExpressions.Regex.Match(combined, @"(\d+) passed");
                if (pytestMatch.Success)
                    results.Passed = int.Parse(pytestMatch.Groups[1].Value);
                
                var pytestFailMatch = System.Text.RegularExpressions.Regex.Match(combined, @"(\d+) failed");
                if (pytestFailMatch.Success)
                    results.Failed = int.Parse(pytestFailMatch.Groups[1].Value);
                break;

            case "javascript":
            case "typescript":
                // Jest output: "Tests: 5 passed, 2 failed"
                var jestMatch = System.Text.RegularExpressions.Regex.Match(combined, @"Tests:\s*(\d+) passed");
                if (jestMatch.Success)
                    results.Passed = int.Parse(jestMatch.Groups[1].Value);
                
                var jestFailMatch = System.Text.RegularExpressions.Regex.Match(combined, @"(\d+) failed");
                if (jestFailMatch.Success)
                    results.Failed = int.Parse(jestFailMatch.Groups[1].Value);
                break;

            case "csharp":
                // dotnet test: "Passed: 5, Failed: 2"
                var dotnetPassMatch = System.Text.RegularExpressions.Regex.Match(combined, @"Passed:\s*(\d+)");
                if (dotnetPassMatch.Success)
                    results.Passed = int.Parse(dotnetPassMatch.Groups[1].Value);
                
                var dotnetFailMatch = System.Text.RegularExpressions.Regex.Match(combined, @"Failed:\s*(\d+)");
                if (dotnetFailMatch.Success)
                    results.Failed = int.Parse(dotnetFailMatch.Groups[1].Value);
                break;

            case "go":
                // go test: "ok" or "FAIL"
                results.Passed = System.Text.RegularExpressions.Regex.Matches(combined, @"--- PASS").Count;
                results.Failed = System.Text.RegularExpressions.Regex.Matches(combined, @"--- FAIL").Count;
                break;
        }

        results.Total = results.Passed + results.Failed + results.Skipped;
        return results;
    }
}

// Models
public class PipelineRequest
{
    public string ProjectId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Language { get; set; } = "python";
    public string? Prompt { get; set; }
    public List<ProjectFile> Files { get; set; } = new();
    public string? EntryPoint { get; set; }
    public bool RunAfterBuild { get; set; } = false;
    public Dictionary<string, string> Context { get; set; } = new();
    public int MaxIterations { get; set; } = 10;
}

public class PipelineResult
{
    public string ProjectId { get; set; } = "";
    public string RequestId { get; set; } = "";
    public PipelineStatus Status { get; set; } = PipelineStatus.Pending;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? TotalDuration { get; set; }
    public int Iterations { get; set; }
    public List<PhaseResult> Phases { get; set; } = new();
    public List<ProjectFile>? OutputFiles { get; set; }
    public VerificationResult? VerificationResult { get; set; }
    public string? ErrorMessage { get; set; }
    public decimal TotalCost { get; set; }
}

public class PhaseResult
{
    public PipelinePhase Phase { get; set; }
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
    public string Output { get; set; } = "";
    public int? ExitCode { get; set; }
    public List<PipelineError> Errors { get; set; } = new();
    public List<ProjectFile>? OutputFiles { get; set; }
    public int TokensUsed { get; set; }
    public StaticAnalysisResult? AnalysisResult { get; set; }
    public TestResults? TestResults { get; set; }
}

public class PipelineError
{
    public PipelinePhase Phase { get; set; }
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public string? File { get; set; }
    public int? Line { get; set; }
    public int? Column { get; set; }
    public string? Code { get; set; }
    public string? StackTrace { get; set; }
}

public class TestResults
{
    public int Total { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public List<TestCaseResult> Details { get; set; } = new();
}

public class TestCaseResult
{
    public string Name { get; set; } = "";
    public bool Passed { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
}

public enum PipelineStatus
{
    Pending,
    Running,
    Success,
    GenerationFailed,
    StaticAnalysisFailed,
    BuildFailed,
    TestsFailed,
    RuntimeFailed,
    VerificationFailed,
    RateLimited,
    Cancelled,
    InternalError
}

public enum PipelinePhase
{
    CodeGeneration,
    StaticAnalysis,
    DependencyResolution,
    Build,
    TestGeneration,
    TestExecution,
    Execution,
    Verification
}

// Code Generator Interface (to be implemented with AI service)
public interface ICodeGeneratorService
{
    Task<CodeGenerationResult> GenerateCodeAsync(CodeGenerationRequest request, CancellationToken ct = default);
}

public class CodeGenerationRequest
{
    public string ProjectId { get; set; } = "";
    public string Language { get; set; } = "";
    public string Prompt { get; set; } = "";
    public List<ProjectFile> ExistingFiles { get; set; } = new();
    public Dictionary<string, string> Context { get; set; } = new();
}

public class CodeGenerationResult
{
    public bool Success { get; set; }
    public List<ProjectFile> Files { get; set; } = new();
    public string? Explanation { get; set; }
    public string? ErrorMessage { get; set; }
    public int TokensUsed { get; set; }
}
