// Test Generator Service
// Automatically generates tests for code
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace LittleHelperAI.API.Services.Sandbox;

public interface ITestGenerator
{
    Task<GeneratedTests> GenerateTestsAsync(string projectId, string language, List<ProjectFile> files);
    string GenerateTestForFunction(string language, FunctionSignature function);
}

public class TestGenerator : ITestGenerator
{
    private readonly ILogger<TestGenerator> _logger;

    public TestGenerator(ILogger<TestGenerator> logger)
    {
        _logger = logger;
    }

    public async Task<GeneratedTests> GenerateTestsAsync(string projectId, string language, List<ProjectFile> files)
    {
        _logger.LogInformation("Generating tests for project {ProjectId}, language: {Language}", projectId, language);

        var result = new GeneratedTests
        {
            ProjectId = projectId,
            Language = language,
            GeneratedAt = DateTime.UtcNow
        };

        // Extract functions/methods from code
        var functions = ExtractFunctions(language, files);
        
        foreach (var func in functions)
        {
            var testCode = GenerateTestForFunction(language, func);
            result.TestCases.Add(new GeneratedTestCase
            {
                Name = $"test_{func.Name}",
                TargetFunction = func.Name,
                TargetFile = func.File,
                TestCode = testCode,
                TestType = DetermineTestType(func)
            });
        }

        // Generate test file content
        result.TestFileContent = GenerateTestFile(language, result.TestCases);
        result.TestFileName = GetTestFileName(language, "tests");

        _logger.LogInformation("Generated {Count} test cases", result.TestCases.Count);

        return await Task.FromResult(result);
    }

    public string GenerateTestForFunction(string language, FunctionSignature function)
    {
        return language.ToLower() switch
        {
            "python" => GeneratePythonTest(function),
            "javascript" or "typescript" => GenerateJavaScriptTest(function),
            "csharp" => GenerateCSharpTest(function),
            "go" => GenerateGoTest(function),
            "java" => GenerateJavaTest(function),
            _ => $"// Test generation not supported for {language}"
        };
    }

    private List<FunctionSignature> ExtractFunctions(string language, List<ProjectFile> files)
    {
        var functions = new List<FunctionSignature>();

        foreach (var file in files)
        {
            var fileFunctions = language.ToLower() switch
            {
                "python" => ExtractPythonFunctions(file),
                "javascript" or "typescript" => ExtractJavaScriptFunctions(file),
                "csharp" => ExtractCSharpMethods(file),
                "go" => ExtractGoFunctions(file),
                "java" => ExtractJavaMethods(file),
                _ => new List<FunctionSignature>()
            };
            functions.AddRange(fileFunctions);
        }

        return functions;
    }

    private List<FunctionSignature> ExtractPythonFunctions(ProjectFile file)
    {
        var functions = new List<FunctionSignature>();
        
        // Match: def function_name(params) -> return_type:
        var pattern = @"def\s+(\w+)\s*\(([^)]*)\)(?:\s*->\s*(\w+))?:";
        var matches = Regex.Matches(file.Content, pattern);

        foreach (Match match in matches)
        {
            var funcName = match.Groups[1].Value;
            
            // Skip private/dunder methods and test methods
            if (funcName.StartsWith("_") || funcName.StartsWith("test_"))
                continue;

            var paramsStr = match.Groups[2].Value;
            var returnType = match.Groups[3].Success ? match.Groups[3].Value : null;

            functions.Add(new FunctionSignature
            {
                Name = funcName,
                File = file.Path,
                Language = "python",
                Parameters = ParsePythonParams(paramsStr),
                ReturnType = returnType,
                IsAsync = file.Content.Contains($"async def {funcName}")
            });
        }

        return functions;
    }

    private List<FunctionParameter> ParsePythonParams(string paramsStr)
    {
        var parameters = new List<FunctionParameter>();
        if (string.IsNullOrWhiteSpace(paramsStr)) return parameters;

        var parts = paramsStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed == "self" || trimmed == "cls") continue;

            var colonIdx = trimmed.IndexOf(':');
            var eqIdx = trimmed.IndexOf('=');

            string name, type = "Any";
            string? defaultValue = null;

            if (colonIdx > 0)
            {
                name = trimmed[..colonIdx].Trim();
                var rest = trimmed[(colonIdx + 1)..].Trim();
                if (eqIdx > colonIdx)
                {
                    type = rest[..(eqIdx - colonIdx - 1)].Trim();
                    defaultValue = rest[(eqIdx - colonIdx)..].Trim();
                }
                else
                {
                    type = rest;
                }
            }
            else if (eqIdx > 0)
            {
                name = trimmed[..eqIdx].Trim();
                defaultValue = trimmed[(eqIdx + 1)..].Trim();
            }
            else
            {
                name = trimmed;
            }

            parameters.Add(new FunctionParameter
            {
                Name = name,
                Type = type,
                DefaultValue = defaultValue,
                IsOptional = defaultValue != null
            });
        }

        return parameters;
    }

    private List<FunctionSignature> ExtractJavaScriptFunctions(ProjectFile file)
    {
        var functions = new List<FunctionSignature>();

        // Match: function name(params) or const name = (params) => or name(params) {
        var patterns = new[]
        {
            @"function\s+(\w+)\s*\(([^)]*)\)",
            @"(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s*)?\(([^)]*)\)\s*=>",
            @"(\w+)\s*\(([^)]*)\)\s*{"
        };

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(file.Content, pattern);
            foreach (Match match in matches)
            {
                var funcName = match.Groups[1].Value;
                
                // Skip common non-function matches and test functions
                if (funcName is "if" or "for" or "while" or "switch" or "catch" || 
                    funcName.StartsWith("test") || funcName.StartsWith("_"))
                    continue;

                var paramsStr = match.Groups[2].Value;

                functions.Add(new FunctionSignature
                {
                    Name = funcName,
                    File = file.Path,
                    Language = "javascript",
                    Parameters = ParseJavaScriptParams(paramsStr),
                    IsAsync = file.Content.Contains($"async function {funcName}") || 
                             file.Content.Contains($"async ({paramsStr})")
                });
            }
        }

        return functions.DistinctBy(f => f.Name).ToList();
    }

    private List<FunctionParameter> ParseJavaScriptParams(string paramsStr)
    {
        var parameters = new List<FunctionParameter>();
        if (string.IsNullOrWhiteSpace(paramsStr)) return parameters;

        var parts = paramsStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            var eqIdx = trimmed.IndexOf('=');

            string name;
            string? defaultValue = null;

            if (eqIdx > 0)
            {
                name = trimmed[..eqIdx].Trim();
                defaultValue = trimmed[(eqIdx + 1)..].Trim();
            }
            else
            {
                name = trimmed;
            }

            // Handle destructuring
            if (name.StartsWith("{") || name.StartsWith("["))
            {
                name = "destructured";
            }

            parameters.Add(new FunctionParameter
            {
                Name = name,
                Type = "any",
                DefaultValue = defaultValue,
                IsOptional = defaultValue != null
            });
        }

        return parameters;
    }

    private List<FunctionSignature> ExtractCSharpMethods(ProjectFile file)
    {
        var methods = new List<FunctionSignature>();

        // Match: public/private/protected ReturnType MethodName(params)
        var pattern = @"(?:public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?(\w+(?:<[^>]+>)?)\s+(\w+)\s*\(([^)]*)\)";
        var matches = Regex.Matches(file.Content, pattern);

        foreach (Match match in matches)
        {
            var returnType = match.Groups[1].Value;
            var methodName = match.Groups[2].Value;
            var paramsStr = match.Groups[3].Value;

            // Skip constructors and common methods
            if (methodName == returnType || methodName is "Main" or "ToString" or "GetHashCode" or "Equals")
                continue;

            methods.Add(new FunctionSignature
            {
                Name = methodName,
                File = file.Path,
                Language = "csharp",
                Parameters = ParseCSharpParams(paramsStr),
                ReturnType = returnType,
                IsAsync = returnType.StartsWith("Task") || returnType.StartsWith("ValueTask")
            });
        }

        return methods;
    }

    private List<FunctionParameter> ParseCSharpParams(string paramsStr)
    {
        var parameters = new List<FunctionParameter>();
        if (string.IsNullOrWhiteSpace(paramsStr)) return parameters;

        var parts = paramsStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            var tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length >= 2)
            {
                var type = tokens[0];
                var name = tokens[1];
                string? defaultValue = null;

                if (tokens.Length > 3 && tokens[2] == "=")
                {
                    defaultValue = tokens[3];
                }

                parameters.Add(new FunctionParameter
                {
                    Name = name,
                    Type = type,
                    DefaultValue = defaultValue,
                    IsOptional = defaultValue != null || type.EndsWith("?")
                });
            }
        }

        return parameters;
    }

    private List<FunctionSignature> ExtractGoFunctions(ProjectFile file)
    {
        var functions = new List<FunctionSignature>();

        // Match: func Name(params) returnType
        var pattern = @"func\s+(?:\([^)]+\)\s+)?(\w+)\s*\(([^)]*)\)(?:\s*\(([^)]+)\)|\s+(\w+))?";
        var matches = Regex.Matches(file.Content, pattern);

        foreach (Match match in matches)
        {
            var funcName = match.Groups[1].Value;
            var paramsStr = match.Groups[2].Value;
            var returnType = match.Groups[3].Success ? match.Groups[3].Value : 
                            match.Groups[4].Success ? match.Groups[4].Value : null;

            if (funcName.StartsWith("Test") || funcName == "main" || funcName == "init")
                continue;

            functions.Add(new FunctionSignature
            {
                Name = funcName,
                File = file.Path,
                Language = "go",
                Parameters = ParseGoParams(paramsStr),
                ReturnType = returnType
            });
        }

        return functions;
    }

    private List<FunctionParameter> ParseGoParams(string paramsStr)
    {
        var parameters = new List<FunctionParameter>();
        if (string.IsNullOrWhiteSpace(paramsStr)) return parameters;

        var parts = paramsStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            var tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length >= 2)
            {
                parameters.Add(new FunctionParameter
                {
                    Name = tokens[0],
                    Type = tokens[^1] // Last token is the type
                });
            }
            else if (tokens.Length == 1)
            {
                // Just type, name from previous param
                parameters.Add(new FunctionParameter
                {
                    Name = $"param{parameters.Count}",
                    Type = tokens[0]
                });
            }
        }

        return parameters;
    }

    private List<FunctionSignature> ExtractJavaMethods(ProjectFile file)
    {
        return ExtractCSharpMethods(file); // Similar syntax
    }

    private string GeneratePythonTest(FunctionSignature func)
    {
        var sb = new StringBuilder();
        var asyncPrefix = func.IsAsync ? "async " : "";
        var awaitPrefix = func.IsAsync ? "await " : "";

        sb.AppendLine($"{asyncPrefix}def test_{func.Name}_basic():");
        sb.AppendLine($"    \"\"\"Test {func.Name} with basic inputs\"\"\"");

        // Generate sample inputs based on parameter types
        var args = new List<string>();
        foreach (var param in func.Parameters)
        {
            var value = GetSampleValue(param.Type, "python");
            args.Add(value);
        }

        var argsStr = string.Join(", ", args);
        sb.AppendLine($"    result = {awaitPrefix}{func.Name}({argsStr})");
        sb.AppendLine($"    assert result is not None");
        sb.AppendLine();

        // Generate edge case test
        sb.AppendLine($"{asyncPrefix}def test_{func.Name}_edge_cases():");
        sb.AppendLine($"    \"\"\"Test {func.Name} with edge cases\"\"\"");
        if (func.Parameters.Any())
        {
            sb.AppendLine($"    # Test with None/empty values");
            sb.AppendLine($"    try:");
            sb.AppendLine($"        result = {awaitPrefix}{func.Name}(None)");
            sb.AppendLine($"    except (TypeError, ValueError) as e:");
            sb.AppendLine($"        pass  # Expected");
        }
        else
        {
            sb.AppendLine($"    result = {awaitPrefix}{func.Name}()");
            sb.AppendLine($"    assert result is not None");
        }

        return sb.ToString();
    }

    private string GenerateJavaScriptTest(FunctionSignature func)
    {
        var sb = new StringBuilder();
        var asyncPrefix = func.IsAsync ? "async " : "";
        var awaitPrefix = func.IsAsync ? "await " : "";

        sb.AppendLine($"describe('{func.Name}', () => {{");
        sb.AppendLine($"  it('should work with basic inputs', {asyncPrefix}() => {{");

        var args = func.Parameters.Select(p => GetSampleValue(p.Type, "javascript"));
        var argsStr = string.Join(", ", args);

        sb.AppendLine($"    const result = {awaitPrefix}{func.Name}({argsStr});");
        sb.AppendLine($"    expect(result).toBeDefined();");
        sb.AppendLine($"  }});");
        sb.AppendLine();

        sb.AppendLine($"  it('should handle edge cases', {asyncPrefix}() => {{");
        sb.AppendLine($"    expect(() => {awaitPrefix}{func.Name}(null)).toThrow();");
        sb.AppendLine($"  }});");
        sb.AppendLine($"}});");

        return sb.ToString();
    }

    private string GenerateCSharpTest(FunctionSignature func)
    {
        var sb = new StringBuilder();
        var asyncModifier = func.IsAsync ? "async Task" : "void";
        var awaitPrefix = func.IsAsync ? "await " : "";

        sb.AppendLine($"[Fact]");
        sb.AppendLine($"public {asyncModifier} {func.Name}_Should_Work_With_Basic_Inputs()");
        sb.AppendLine($"{{");

        var args = func.Parameters.Select(p => GetSampleValue(p.Type, "csharp"));
        var argsStr = string.Join(", ", args);

        sb.AppendLine($"    var result = {awaitPrefix}_sut.{func.Name}({argsStr});");
        sb.AppendLine($"    Assert.NotNull(result);");
        sb.AppendLine($"}}");
        sb.AppendLine();

        sb.AppendLine($"[Fact]");
        sb.AppendLine($"public {asyncModifier} {func.Name}_Should_Handle_Null()");
        sb.AppendLine($"{{");
        sb.AppendLine($"    Assert.Throws<ArgumentNullException>(() => {awaitPrefix}_sut.{func.Name}(null));");
        sb.AppendLine($"}}");

        return sb.ToString();
    }

    private string GenerateGoTest(FunctionSignature func)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"func Test{func.Name}Basic(t *testing.T) {{");

        var args = func.Parameters.Select(p => GetSampleValue(p.Type, "go"));
        var argsStr = string.Join(", ", args);

        sb.AppendLine($"    result := {func.Name}({argsStr})");
        sb.AppendLine($"    if result == nil {{");
        sb.AppendLine($"        t.Error(\"expected non-nil result\")");
        sb.AppendLine($"    }}");
        sb.AppendLine($"}}");

        return sb.ToString();
    }

    private string GenerateJavaTest(FunctionSignature func)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"@Test");
        sb.AppendLine($"public void test{char.ToUpper(func.Name[0])}{func.Name[1..]}_basic() {{");

        var args = func.Parameters.Select(p => GetSampleValue(p.Type, "java"));
        var argsStr = string.Join(", ", args);

        sb.AppendLine($"    var result = sut.{func.Name}({argsStr});");
        sb.AppendLine($"    assertNotNull(result);");
        sb.AppendLine($"}}");

        return sb.ToString();
    }

    private string GetSampleValue(string type, string language)
    {
        var lowerType = type.ToLower();
        
        return lowerType switch
        {
            "string" or "str" => language == "go" ? "\"test\"" : "\"test\"",
            "int" or "int32" or "int64" or "integer" or "number" => "42",
            "float" or "double" or "float64" or "float32" or "decimal" => "3.14",
            "bool" or "boolean" => language == "python" ? "True" : "true",
            "list" or "array" => language switch { "python" => "[]", "go" => "nil", "csharp" => "new List<object>()", _ => "[]" },
            "dict" or "dictionary" or "map" or "object" => language switch { "python" => "{}", "go" => "nil", "csharp" => "new Dictionary<string, object>()", "javascript" => "{}", _ => "{}" },
            _ when lowerType.Contains("list") || lowerType.Contains("[]") => language == "csharp" ? $"new {type}()" : "[]",
            _ when lowerType.Contains("dict") || lowerType.Contains("map") => language == "csharp" ? $"new {type}()" : "{}",
            _ => language switch { "python" => "None", "go" => "nil", "csharp" or "java" => "null", _ => "null" }
        };
    }

    private TestType DetermineTestType(FunctionSignature func)
    {
        var name = func.Name.ToLower();
        
        if (name.Contains("create") || name.Contains("insert") || name.Contains("save"))
            return TestType.Create;
        if (name.Contains("read") || name.Contains("get") || name.Contains("find") || name.Contains("fetch"))
            return TestType.Read;
        if (name.Contains("update") || name.Contains("modify") || name.Contains("set"))
            return TestType.Update;
        if (name.Contains("delete") || name.Contains("remove"))
            return TestType.Delete;
        if (name.Contains("validate") || name.Contains("check") || name.Contains("is"))
            return TestType.Validation;
        if (name.Contains("calculate") || name.Contains("compute") || name.Contains("process"))
            return TestType.Computation;

        return TestType.Unit;
    }

    private string GenerateTestFile(string language, List<GeneratedTestCase> testCases)
    {
        var sb = new StringBuilder();

        switch (language.ToLower())
        {
            case "python":
                sb.AppendLine("import pytest");
                sb.AppendLine("import asyncio");
                sb.AppendLine("from main import *  # Import functions to test");
                sb.AppendLine();
                foreach (var test in testCases)
                {
                    sb.AppendLine(test.TestCode);
                    sb.AppendLine();
                }
                break;

            case "javascript":
            case "typescript":
                sb.AppendLine("const { describe, it, expect } = require('@jest/globals');");
                sb.AppendLine("const * as module = require('./index');");
                sb.AppendLine();
                foreach (var test in testCases)
                {
                    sb.AppendLine(test.TestCode);
                    sb.AppendLine();
                }
                break;

            case "csharp":
                sb.AppendLine("using Xunit;");
                sb.AppendLine("using System.Threading.Tasks;");
                sb.AppendLine();
                sb.AppendLine("public class GeneratedTests");
                sb.AppendLine("{");
                sb.AppendLine("    private readonly object _sut; // System under test");
                sb.AppendLine();
                foreach (var test in testCases)
                {
                    foreach (var line in test.TestCode.Split('\n'))
                    {
                        sb.AppendLine($"    {line}");
                    }
                    sb.AppendLine();
                }
                sb.AppendLine("}");
                break;

            case "go":
                sb.AppendLine("package main");
                sb.AppendLine();
                sb.AppendLine("import \"testing\"");
                sb.AppendLine();
                foreach (var test in testCases)
                {
                    sb.AppendLine(test.TestCode);
                    sb.AppendLine();
                }
                break;

            default:
                sb.AppendLine($"// Test generation not fully supported for {language}");
                foreach (var test in testCases)
                {
                    sb.AppendLine(test.TestCode);
                    sb.AppendLine();
                }
                break;
        }

        return sb.ToString();
    }

    private string GetTestFileName(string language, string baseName)
    {
        return language.ToLower() switch
        {
            "python" => $"test_{baseName}.py",
            "javascript" => $"{baseName}.test.js",
            "typescript" => $"{baseName}.test.ts",
            "csharp" => $"{baseName}Tests.cs",
            "go" => $"{baseName}_test.go",
            "java" => $"{baseName}Test.java",
            _ => $"{baseName}_test.txt"
        };
    }
}

// Models
public class GeneratedTests
{
    public string ProjectId { get; set; } = "";
    public string Language { get; set; } = "";
    public DateTime GeneratedAt { get; set; }
    public List<GeneratedTestCase> TestCases { get; set; } = new();
    public string TestFileName { get; set; } = "";
    public string TestFileContent { get; set; } = "";
}

public class GeneratedTestCase
{
    public string Name { get; set; } = "";
    public string TargetFunction { get; set; } = "";
    public string TargetFile { get; set; } = "";
    public string TestCode { get; set; } = "";
    public TestType TestType { get; set; }
}

public class FunctionSignature
{
    public string Name { get; set; } = "";
    public string File { get; set; } = "";
    public string Language { get; set; } = "";
    public List<FunctionParameter> Parameters { get; set; } = new();
    public string? ReturnType { get; set; }
    public bool IsAsync { get; set; }
}

public class FunctionParameter
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? DefaultValue { get; set; }
    public bool IsOptional { get; set; }
}

public enum TestType
{
    Unit,
    Integration,
    Create,
    Read,
    Update,
    Delete,
    Validation,
    Computation,
    EdgeCase
}
