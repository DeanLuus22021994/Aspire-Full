using Aspire_Full.Shared;
using Aspire_Full.Shared.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aspire_Full.Agents.Core.Maintenance;

/// <summary>
/// Service for orchestrating self-enhancement automation, including:
/// - Registry redundancy analysis
/// - Code quality profiling
/// - Automated cleanup recommendations
/// </summary>
public sealed class SelfEnhancementService : ISelfEnhancementService
{
    private readonly ILogger<SelfEnhancementService>? _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IMaintenanceAgent _maintenanceAgent;

    public SelfEnhancementService(
        IMaintenanceAgent maintenanceAgent,
        ILogger<SelfEnhancementService>? logger = null,
        TimeProvider? timeProvider = null)
    {
        _maintenanceAgent = maintenanceAgent ?? throw new ArgumentNullException(nameof(maintenanceAgent));
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Runs the registry analyzer Python script and parses results.
    /// </summary>
    public async Task<Result<AnalysisReport>> AnalyzeRegistryAsync(
        string workspaceRoot,
        AnalysisOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new AnalysisOptions();
        var startTime = _timeProvider.GetTimestamp();

        _logger?.LogInformation("Starting registry analysis in {WorkspaceRoot}", workspaceRoot);

        try
        {
            // Run the Python registry analyzer script
            var scriptPath = Path.Combine(
                workspaceRoot,
                "Infra",
                "Aspire-Full.DevContainer",
                "Scripts",
                "registry_analyzer.py");

            if (!File.Exists(scriptPath))
            {
                return Result<AnalysisReport>.Failure($"Registry analyzer script not found: {scriptPath}");
            }

            var outputDir = Path.Combine(workspaceRoot, "Infra", ".config");
            var args = new List<string>
            {
                scriptPath,
                "--output-dir", outputDir
            };

            if (options.DryRun)
            {
                args.Add("--dry-run");
            }

            var result = await RunPythonScriptAsync(args, workspaceRoot, ct);
            if (!result.IsSuccess)
            {
                return Result<AnalysisReport>.Failure($"Analysis failed: {result.Error}");
            }

            // Parse the JSON report
            var reportPath = Path.Combine(outputDir, "registry-analysis.json");
            if (!options.DryRun && File.Exists(reportPath))
            {
                var report = await ParseReportAsync(reportPath, ct);
                if (report is not null)
                {
                    var elapsed = _timeProvider.GetElapsedTime(startTime);
                    _logger?.LogInformation(
                        "Analysis complete. Findings: {Count}, Duration: {Duration}",
                        report.TotalFindings,
                        elapsed);
                    return Result<AnalysisReport>.Success(report);
                }
            }

            return Result<AnalysisReport>.Success(new AnalysisReport
            {
                Timestamp = _timeProvider.GetUtcNow(),
                DryRun = options.DryRun
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Registry analysis failed");
            return Result<AnalysisReport>.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Applies auto-fixable recommendations from the analysis report.
    /// </summary>
    public async Task<Result<EnhancementResult>> ApplyEnhancementsAsync(
        AnalysisReport report,
        string workspaceRoot,
        CancellationToken ct = default)
    {
        var appliedFixes = new List<string>();
        var failedFixes = new List<string>();

        _logger?.LogInformation("Applying {Count} auto-fixable enhancements", report.AutoFixableCount);

        foreach (var finding in report.Findings.Where(f => f.AutoFixable))
        {
            try
            {
                var applied = await ApplySingleFixAsync(finding, workspaceRoot, ct);
                if (applied)
                {
                    appliedFixes.Add($"{finding.Category}: {finding.Message}");
                }
                else
                {
                    failedFixes.Add($"{finding.Category}: {finding.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to apply fix for {Category}", finding.Category);
                failedFixes.Add($"{finding.Category}: {ex.Message}");
            }
        }

        return Result<EnhancementResult>.Success(new EnhancementResult
        {
            AppliedFixes = appliedFixes,
            FailedFixes = failedFixes,
            Timestamp = _timeProvider.GetUtcNow()
        });
    }

    /// <summary>
    /// Runs a full self-enhancement cycle: analyze → fix → verify.
    /// </summary>
    public async Task<Result<SelfEnhancementCycle>> RunFullCycleAsync(
        string workspaceRoot,
        CancellationToken ct = default)
    {
        var startTime = _timeProvider.GetTimestamp();
        _logger?.LogInformation("Starting full self-enhancement cycle");

        // Phase 1: Analyze
        var analysisResult = await AnalyzeRegistryAsync(workspaceRoot, new AnalysisOptions(), ct);
        if (!analysisResult.IsSuccess)
        {
            return Result<SelfEnhancementCycle>.Failure($"Analysis phase failed: {analysisResult.Error}");
        }

        // Phase 2: Apply fixes
        var enhancementResult = await ApplyEnhancementsAsync(analysisResult.Value!, workspaceRoot, ct);
        if (!enhancementResult.IsSuccess)
        {
            return Result<SelfEnhancementCycle>.Failure($"Enhancement phase failed: {enhancementResult.Error}");
        }

        // Phase 3: Run maintenance
        var maintenanceResult = await _maintenanceAgent.RunAsync(workspaceRoot, ct);
        if (!maintenanceResult.IsSuccess)
        {
            _logger?.LogWarning("Maintenance phase encountered issues: {Error}", maintenanceResult.Error);
        }

        var elapsed = _timeProvider.GetElapsedTime(startTime);

        return Result<SelfEnhancementCycle>.Success(new SelfEnhancementCycle
        {
            AnalysisReport = analysisResult.Value!,
            EnhancementResult = enhancementResult.Value!,
            MaintenanceSucceeded = maintenanceResult.IsSuccess,
            TotalDuration = elapsed,
            Timestamp = _timeProvider.GetUtcNow()
        });
    }

    private async Task<Result> RunPythonScriptAsync(
        IEnumerable<string> args,
        string workingDirectory,
        CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "python",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = new System.Diagnostics.Process { StartInfo = psi };
        var errorOutput = new System.Text.StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                _logger?.LogDebug("[python] {Output}", e.Data);
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                errorOutput.AppendLine(e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                return Result.Failure($"Exit code {process.ExitCode}: {errorOutput}");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Process failed: {ex.Message}");
        }
    }

    private static async Task<AnalysisReport?> ParseReportAsync(string path, CancellationToken ct)
    {
        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            return System.Text.Json.JsonSerializer.Deserialize<AnalysisReport>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    private Task<bool> ApplySingleFixAsync(AnalysisFinding finding, string workspaceRoot, CancellationToken ct)
    {
        // Placeholder for actual fix implementations
        // Each finding category would have its own fix logic:
        // - "overhead" + dangling images → docker image prune
        // - "unused" imports → ruff fix or manual removal
        // - "redundancy" → consolidation logic
        _logger?.LogDebug("Would apply fix: {Category} - {Message}", finding.Category, finding.Message);
        return Task.FromResult(false);
    }
}

// Supporting types

public interface ISelfEnhancementService
{
    Task<Result<AnalysisReport>> AnalyzeRegistryAsync(
        string workspaceRoot,
        AnalysisOptions? options = null,
        CancellationToken ct = default);

    Task<Result<EnhancementResult>> ApplyEnhancementsAsync(
        AnalysisReport report,
        string workspaceRoot,
        CancellationToken ct = default);

    Task<Result<SelfEnhancementCycle>> RunFullCycleAsync(
        string workspaceRoot,
        CancellationToken ct = default);
}

public sealed class AnalysisOptions
{
    public bool DryRun { get; init; }
    public bool IncludeDocker { get; init; } = true;
    public bool IncludePython { get; init; } = true;
    public bool IncludeInfra { get; init; } = true;
}

public sealed class AnalysisReport
{
    public DateTimeOffset Timestamp { get; init; }
    public bool DryRun { get; init; }
    public int TotalFindings { get; init; }
    public int AutoFixableCount { get; init; }
    public List<AnalysisFinding> Findings { get; init; } = [];
    public AnalysisSummary? Summary { get; init; }
}

public sealed class AnalysisFinding
{
    public required string Category { get; init; }
    public required string Severity { get; init; }
    public required string Location { get; init; }
    public required string Message { get; init; }
    public string? SuggestedAction { get; init; }
    public bool AutoFixable { get; init; }
}

public sealed class AnalysisSummary
{
    public int TotalModules { get; init; }
    public int TotalLinesOfCode { get; init; }
    public int TotalFunctions { get; init; }
    public double AverageComplexity { get; init; }
    public int DockerImagesCount { get; init; }
}

public sealed class EnhancementResult
{
    public List<string> AppliedFixes { get; init; } = [];
    public List<string> FailedFixes { get; init; } = [];
    public DateTimeOffset Timestamp { get; init; }
}

public sealed class SelfEnhancementCycle
{
    public required AnalysisReport AnalysisReport { get; init; }
    public required EnhancementResult EnhancementResult { get; init; }
    public bool MaintenanceSucceeded { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}
