// Sandboxed Code Execution Service
// Executes code in Docker containers with resource limits
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace LittleHelperAI.API.Services.Sandbox;

public interface ISandboxExecutor
{
    Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken ct = default);
    Task<ExecutionResult> ExecuteWithRetryAsync(ExecutionRequest request, int maxRetries = 3, CancellationToken ct = default);
    Task CleanupContainerAsync(string containerId);
}

public class SandboxExecutor : ISandboxExecutor
{
    private readonly ILogger<SandboxExecutor> _logger;
    private readonly SandboxConfig _config;
    private readonly SemaphoreSlim _executionSemaphore;

    public SandboxExecutor(ILogger<SandboxExecutor> logger, SandboxConfig? config = null)
    {
        _logger = logger;
        _config = config ?? new SandboxConfig();
        _executionSemaphore = new SemaphoreSlim(_config.MaxConcurrentExecutions);
    }

    public async Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken ct = default)
    {
        await _executionSemaphore.WaitAsync(ct);
        var startTime = DateTime.UtcNow;
        var containerId = $"sandbox-{Guid.NewGuid():N}";

        try
        {
            _logger.LogInformation("Starting sandboxed execution for project {ProjectId}, language: {Language}", 
                request.ProjectId, request.Language);

            // 1. Prepare workspace
            var workDir = await PrepareWorkspaceAsync(request, containerId);

            // 2. Select Docker image based on language
            var dockerImage = GetDockerImage(request.Language);

            // 3. Build Docker run command with resource limits
            var dockerCmd = BuildDockerCommand(containerId, dockerImage, workDir, request);

            // 4. Execute in sandbox
            var (exitCode, stdout, stderr) = await RunDockerContainerAsync(dockerCmd, request.TimeoutSeconds, ct);

            // 5. Parse execution output
            var result = new ExecutionResult
            {
                Success = exitCode == 0,
                ExitCode = exitCode,
                Stdout = stdout,
                Stderr = stderr,
                ContainerId = containerId,
                ExecutionTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                Language = request.Language,
                Phase = request.Phase
            };

            // 6. Parse structured errors if failed
            if (!result.Success)
            {
                result.Errors = ParseErrors(stderr, stdout, request.Language);
                result.StackTrace = ExtractStackTrace(stderr);
            }

            _logger.LogInformation("Sandbox execution completed. Success: {Success}, ExitCode: {ExitCode}, Time: {Time}ms",
                result.Success, exitCode, result.ExecutionTimeMs);

            return result;
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult
            {
                Success = false,
                ExitCode = -1,
                Stderr = "Execution cancelled",
                ContainerId = containerId,
                ExecutionTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                Errors = new List<ExecutionError> { new() { Type = "Timeout", Message = "Execution was cancelled or timed out" } }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sandbox execution failed for project {ProjectId}", request.ProjectId);
            return new ExecutionResult
            {
                Success = false,
                ExitCode = -1,
                Stderr = ex.Message,
                ContainerId = containerId,
                ExecutionTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                Errors = new List<ExecutionError> { new() { Type = "Internal", Message = ex.Message, StackTrace = ex.StackTrace } }
            };
        }
        finally
        {
            _executionSemaphore.Release();
            // Cleanup container asynchronously
            _ = CleanupContainerAsync(containerId);
        }
    }

    public async Task<ExecutionResult> ExecuteWithRetryAsync(ExecutionRequest request, int maxRetries = 3, CancellationToken ct = default)
    {
        ExecutionResult? lastResult = null;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            _logger.LogInformation("Execution attempt {Attempt}/{MaxRetries} for project {ProjectId}", 
                attempt, maxRetries, request.ProjectId);

            lastResult = await ExecuteAsync(request, ct);

            if (lastResult.Success)
            {
                lastResult.RetryCount = attempt - 1;
                return lastResult;
            }

            // Check if error is retryable
            if (!IsRetryableError(lastResult))
            {
                _logger.LogWarning("Non-retryable error encountered. Stopping retries.");
                break;
            }

            if (attempt < maxRetries)
            {
                // Exponential backoff
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                _logger.LogInformation("Waiting {Delay}s before retry", delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
        }

        lastResult!.RetryCount = maxRetries;
        return lastResult;
    }

    private async Task<string> PrepareWorkspaceAsync(ExecutionRequest request, string containerId)
    {
        var workDir = Path.Combine(_config.WorkspacePath, containerId);
        Directory.CreateDirectory(workDir);

        // Write all project files
        foreach (var file in request.Files)
        {
            var filePath = Path.Combine(workDir, file.Path);
            var fileDir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(fileDir))
                Directory.CreateDirectory(fileDir);
            
            await File.WriteAllTextAsync(filePath, file.Content);
        }

        // Write entry point script based on language
        var entryScript = GenerateEntryScript(request);
        await File.WriteAllTextAsync(Path.Combine(workDir, "entrypoint.sh"), entryScript);

        return workDir;
    }

    private string GetDockerImage(string language) => language.ToLower() switch
    {
        "python" => _config.PythonImage,
        "javascript" or "typescript" or "node" => _config.NodeImage,
        "csharp" or "dotnet" => _config.DotNetImage,
        "java" => _config.JavaImage,
        "go" or "golang" => _config.GoImage,
        "rust" => _config.RustImage,
        "ruby" => _config.RubyImage,
        "php" => _config.PhpImage,
        _ => _config.DefaultImage
    };

    private string BuildDockerCommand(string containerId, string image, string workDir, ExecutionRequest request)
    {
        var sb = new StringBuilder();
        sb.Append("docker run --rm ");
        sb.Append($"--name {containerId} ");
        
        // Resource limits
        sb.Append($"--memory={_config.MemoryLimitMb}m ");
        sb.Append($"--cpus={_config.CpuLimit} ");
        sb.Append($"--pids-limit={_config.PidsLimit} ");
        
        // Network isolation (disable by default for security)
        if (!request.AllowNetwork)
            sb.Append("--network=none ");
        
        // Security options
        sb.Append("--security-opt=no-new-privileges ");
        sb.Append("--cap-drop=ALL ");
        
        // Read-only root filesystem with temp writable
        sb.Append("--read-only ");
        sb.Append("--tmpfs /tmp:rw,noexec,nosuid,size=100m ");
        
        // Mount workspace
        sb.Append($"-v {workDir}:/workspace:rw ");
        sb.Append("-w /workspace ");
        
        // Environment variables
        sb.Append("-e SANDBOX=true ");
        sb.Append($"-e LANGUAGE={request.Language} ");
        
        sb.Append($"{image} ");
        sb.Append("/bin/sh /workspace/entrypoint.sh");

        return sb.ToString();
    }

    private async Task<(int exitCode, string stdout, string stderr)> RunDockerContainerAsync(
        string command, int timeoutSeconds, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdoutBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderrBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
            return (process.ExitCode, stdoutBuilder.ToString(), stderrBuilder.ToString());
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }
    }

    private string GenerateEntryScript(ExecutionRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#!/bin/sh");
        sb.AppendLine("set -e");
        sb.AppendLine();

        switch (request.Language.ToLower())
        {
            case "python":
                sb.AppendLine("# Install dependencies");
                sb.AppendLine("if [ -f requirements.txt ]; then pip install -r requirements.txt --quiet; fi");
                sb.AppendLine();
                if (request.Phase == ExecutionPhase.StaticAnalysis)
                {
                    sb.AppendLine("# Static analysis");
                    sb.AppendLine("python -m py_compile *.py 2>&1 || true");
                    sb.AppendLine("pylint --output-format=json *.py 2>&1 || true");
                }
                else if (request.Phase == ExecutionPhase.Test)
                {
                    sb.AppendLine("# Run tests");
                    sb.AppendLine("pytest --tb=short -v 2>&1");
                }
                else
                {
                    sb.AppendLine("# Run main");
                    sb.AppendLine($"python {request.EntryPoint ?? "main.py"}");
                }
                break;

            case "javascript":
            case "typescript":
            case "node":
                sb.AppendLine("# Install dependencies");
                sb.AppendLine("if [ -f package.json ]; then npm install --silent; fi");
                sb.AppendLine();
                if (request.Phase == ExecutionPhase.StaticAnalysis)
                {
                    sb.AppendLine("# Static analysis");
                    sb.AppendLine("npx eslint . --format=json 2>&1 || true");
                }
                else if (request.Phase == ExecutionPhase.Build)
                {
                    sb.AppendLine("# Build");
                    sb.AppendLine("npm run build 2>&1");
                }
                else if (request.Phase == ExecutionPhase.Test)
                {
                    sb.AppendLine("# Run tests");
                    sb.AppendLine("npm test 2>&1");
                }
                else
                {
                    sb.AppendLine("# Run");
                    sb.AppendLine($"node {request.EntryPoint ?? "index.js"}");
                }
                break;

            case "csharp":
            case "dotnet":
                if (request.Phase == ExecutionPhase.StaticAnalysis)
                {
                    sb.AppendLine("# Static analysis with Roslyn");
                    sb.AppendLine("dotnet build --no-restore -warnaserror 2>&1");
                }
                else if (request.Phase == ExecutionPhase.Build)
                {
                    sb.AppendLine("# Restore and build");
                    sb.AppendLine("dotnet restore");
                    sb.AppendLine("dotnet build -c Release 2>&1");
                }
                else if (request.Phase == ExecutionPhase.Test)
                {
                    sb.AppendLine("# Run tests");
                    sb.AppendLine("dotnet test --no-build -v normal 2>&1");
                }
                else
                {
                    sb.AppendLine("# Run");
                    sb.AppendLine("dotnet run 2>&1");
                }
                break;

            case "go":
            case "golang":
                if (request.Phase == ExecutionPhase.StaticAnalysis)
                {
                    sb.AppendLine("# Static analysis");
                    sb.AppendLine("go vet ./... 2>&1");
                    sb.AppendLine("golint ./... 2>&1 || true");
                }
                else if (request.Phase == ExecutionPhase.Build)
                {
                    sb.AppendLine("# Build");
                    sb.AppendLine("go build -o app ./... 2>&1");
                }
                else if (request.Phase == ExecutionPhase.Test)
                {
                    sb.AppendLine("# Run tests");
                    sb.AppendLine("go test -v ./... 2>&1");
                }
                else
                {
                    sb.AppendLine("# Run");
                    sb.AppendLine("go run . 2>&1");
                }
                break;

            case "java":
                if (request.Phase == ExecutionPhase.Build)
                {
                    sb.AppendLine("# Build");
                    sb.AppendLine("javac -d out *.java 2>&1");
                }
                else if (request.Phase == ExecutionPhase.Test)
                {
                    sb.AppendLine("# Run tests");
                    sb.AppendLine("java -cp out:junit.jar org.junit.runner.JUnitCore TestSuite 2>&1");
                }
                else
                {
                    sb.AppendLine("# Run");
                    sb.AppendLine($"java -cp out {request.EntryPoint ?? "Main"} 2>&1");
                }
                break;

            default:
                sb.AppendLine($"# Unsupported language: {request.Language}");
                sb.AppendLine("exit 1");
                break;
        }

        return sb.ToString();
    }

    private List<ExecutionError> ParseErrors(string stderr, string stdout, string language)
    {
        var errors = new List<ExecutionError>();
        var combined = $"{stderr}\n{stdout}";

        // Try to parse JSON error output (ESLint, pylint)
        try
        {
            if (combined.TrimStart().StartsWith("["))
            {
                var jsonErrors = JsonSerializer.Deserialize<List<JsonElement>>(combined);
                if (jsonErrors != null)
                {
                    foreach (var err in jsonErrors)
                    {
                        errors.Add(new ExecutionError
                        {
                            Type = "Lint",
                            Message = err.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown",
                            File = err.TryGetProperty("path", out var path) ? path.GetString() : null,
                            Line = err.TryGetProperty("line", out var line) ? line.GetInt32() : null,
                            Column = err.TryGetProperty("column", out var col) ? col.GetInt32() : null
                        });
                    }
                    return errors;
                }
            }
        }
        catch { }

        // Parse line-by-line for common error patterns
        var lines = combined.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var error = ParseErrorLine(line, language);
            if (error != null)
                errors.Add(error);
        }

        if (errors.Count == 0 && !string.IsNullOrWhiteSpace(stderr))
        {
            errors.Add(new ExecutionError { Type = "Runtime", Message = stderr.Trim() });
        }

        return errors;
    }

    private ExecutionError? ParseErrorLine(string line, string language)
    {
        // Python: File "main.py", line 10, in <module>
        if (line.Contains("File \"") && line.Contains("\", line"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(line, @"File ""([^""]+)"", line (\d+)");
            if (match.Success)
            {
                return new ExecutionError
                {
                    Type = "Python",
                    File = match.Groups[1].Value,
                    Line = int.Parse(match.Groups[2].Value),
                    Message = line
                };
            }
        }

        // JavaScript/Node: filename.js:10:5
        var jsMatch = System.Text.RegularExpressions.Regex.Match(line, @"([^:\s]+\.(js|ts)):(\d+):(\d+)");
        if (jsMatch.Success)
        {
            return new ExecutionError
            {
                Type = "JavaScript",
                File = jsMatch.Groups[1].Value,
                Line = int.Parse(jsMatch.Groups[3].Value),
                Column = int.Parse(jsMatch.Groups[4].Value),
                Message = line
            };
        }

        // C#: filename.cs(10,5): error CS0001: Message
        var csMatch = System.Text.RegularExpressions.Regex.Match(line, @"([^(]+\.cs)\((\d+),(\d+)\):\s*(error|warning)\s+(\w+):\s*(.+)");
        if (csMatch.Success)
        {
            return new ExecutionError
            {
                Type = csMatch.Groups[4].Value == "error" ? "CompileError" : "Warning",
                File = csMatch.Groups[1].Value,
                Line = int.Parse(csMatch.Groups[2].Value),
                Column = int.Parse(csMatch.Groups[3].Value),
                Code = csMatch.Groups[5].Value,
                Message = csMatch.Groups[6].Value
            };
        }

        // Go: filename.go:10:5: message
        var goMatch = System.Text.RegularExpressions.Regex.Match(line, @"([^:\s]+\.go):(\d+):(\d+):\s*(.+)");
        if (goMatch.Success)
        {
            return new ExecutionError
            {
                Type = "Go",
                File = goMatch.Groups[1].Value,
                Line = int.Parse(goMatch.Groups[2].Value),
                Column = int.Parse(goMatch.Groups[3].Value),
                Message = goMatch.Groups[4].Value
            };
        }

        return null;
    }

    private string? ExtractStackTrace(string stderr)
    {
        var lines = stderr.Split('\n');
        var stackLines = new List<string>();
        bool inStack = false;

        foreach (var line in lines)
        {
            if (line.Contains("Traceback") || line.Contains("at ") || line.Contains("   at "))
            {
                inStack = true;
            }
            if (inStack)
            {
                stackLines.Add(line);
            }
        }

        return stackLines.Count > 0 ? string.Join("\n", stackLines) : null;
    }

    private bool IsRetryableError(ExecutionResult result)
    {
        // Don't retry syntax errors or import errors
        var nonRetryable = new[] { "SyntaxError", "ImportError", "ModuleNotFoundError", "CompileError" };
        return !result.Errors.Any(e => nonRetryable.Any(nr => e.Type.Contains(nr) || e.Message.Contains(nr)));
    }

    public async Task CleanupContainerAsync(string containerId)
    {
        try
        {
            // Force remove container if still running
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"rm -f {containerId}",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process != null)
                await process.WaitForExitAsync();

            // Clean up workspace
            var workDir = Path.Combine(_config.WorkspacePath, containerId);
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup container {ContainerId}", containerId);
        }
    }
}

// Configuration
public class SandboxConfig
{
    public string WorkspacePath { get; set; } = "/tmp/sandbox-workspaces";
    public int MaxConcurrentExecutions { get; set; } = 5;
    public int MemoryLimitMb { get; set; } = 512;
    public double CpuLimit { get; set; } = 1.0;
    public int PidsLimit { get; set; } = 100;
    public int DefaultTimeoutSeconds { get; set; } = 60;

    // Docker images
    public string PythonImage { get; set; } = "python:3.11-slim";
    public string NodeImage { get; set; } = "node:20-slim";
    public string DotNetImage { get; set; } = "mcr.microsoft.com/dotnet/sdk:8.0";
    public string JavaImage { get; set; } = "openjdk:17-slim";
    public string GoImage { get; set; } = "golang:1.21-alpine";
    public string RustImage { get; set; } = "rust:1.74-slim";
    public string RubyImage { get; set; } = "ruby:3.2-slim";
    public string PhpImage { get; set; } = "php:8.2-cli";
    public string DefaultImage { get; set; } = "alpine:latest";
}

// Models
public class ExecutionRequest
{
    public string ProjectId { get; set; } = "";
    public string Language { get; set; } = "python";
    public List<ProjectFile> Files { get; set; } = new();
    public string? EntryPoint { get; set; }
    public ExecutionPhase Phase { get; set; } = ExecutionPhase.Run;
    public int TimeoutSeconds { get; set; } = 60;
    public bool AllowNetwork { get; set; } = false;
    public Dictionary<string, string> Environment { get; set; } = new();
}

public class ProjectFile
{
    public string Path { get; set; } = "";
    public string Content { get; set; } = "";
}

public enum ExecutionPhase
{
    StaticAnalysis,
    DependencyResolution,
    Build,
    Run,
    Test
}

public class ExecutionResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string Stdout { get; set; } = "";
    public string Stderr { get; set; } = "";
    public string ContainerId { get; set; } = "";
    public long ExecutionTimeMs { get; set; }
    public string Language { get; set; } = "";
    public ExecutionPhase Phase { get; set; }
    public List<ExecutionError> Errors { get; set; } = new();
    public string? StackTrace { get; set; }
    public int RetryCount { get; set; }
}

public class ExecutionError
{
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public string? File { get; set; }
    public int? Line { get; set; }
    public int? Column { get; set; }
    public string? Code { get; set; }
    public string? StackTrace { get; set; }
}
