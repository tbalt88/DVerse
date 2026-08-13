using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace DVerse.Harness.Gates;

/// <summary>
/// G7. Runs Microsoft's Power Apps Checker (Solution Checker) against the
/// packed solution and refuses on any Critical or High severity finding.
/// <para>
/// Composition over reinvention: DVerse does not re-implement static analysis
/// of plugins, web resources, canvas app formulas, and so on. Microsoft
/// already ships that analysis as a service. This gate's entire job is to
/// invoke it correctly (via <c>pac</c>, the same tool an author would run by
/// hand) and translate its verdict into the ledger's shape.
/// </para>
/// <para>
/// <c>pac solution check</c> does not accept an unpacked YAML folder; it
/// needs a solution zip. So this gate first shells out to
/// <c>pac solution pack</c> to produce one in a scratch directory, then
/// shells out to <c>pac solution check</c> against that zip. Both steps are
/// process execution, not analysis, and are kept out of the pure parsing path
/// below (<see cref="ParseSarif"/>) on purpose: a unit test must be able to
/// prove the severity mapping without ever starting a process or touching the
/// network. Only the live verification run exercises <see cref="Evaluate"/>
/// end to end.
/// </para>
/// <para>
/// HONEST TENSION with the frozen <see cref="IGate"/> contract: "Implementations
/// must be pure with respect to the filesystem... write nothing." Composing
/// <c>pac</c> makes that impossible to honor literally, because
/// <c>solution pack</c> and <c>solution check --outputDirectory</c> are
/// disk-writing operations by design; there is no in-process way to invoke
/// either without them. The writes are confined to a scratch directory,
/// never to the repository or solution root, and the gate still returns only
/// a verdict; nothing it writes is treated as gate output. Flagged here
/// rather than silently working around the contract's wording.
/// </para>
/// </summary>
public sealed class PowerAppsCheckerGate : IGate
{
    private const string RuleSetName = "Solution Checker";
    private const string Geo = "UnitedStates";

    private static readonly TimeSpan PackTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromMinutes(20);

    public string Id => "G7";
    public string Name => "power-apps-checker";
    public bool RequiresTenant => true;

    public IEnumerable<GateVerdict> Evaluate(GateContext context)
    {
        // Fixed, non-random scratch directory name rather than a per-run GUID.
        // The ONLINE workflow (gates-online.yml) uploads this exact directory
        // as a workflow artifact after the CLI process has already exited, so
        // its path must be predictable from outside this gate rather than
        // discovered from stdout. $RUNNER_TEMP is GitHub Actions' own scratch
        // area for a job; falling back to the OS temp path keeps local runs
        // (the mission's live verification step) working without it.
        var workDir = Path.Combine(TempRoot(), "dverse-g7-checker");

        // Wipe rather than reuse: a stale result zip or sarif from a previous
        // local run must never be mistaken for this run's evidence.
        if (Directory.Exists(workDir))
            Directory.Delete(workDir, recursive: true);
        Directory.CreateDirectory(workDir);

        var zipPath = Path.Combine(workDir, "solution.zip");
        var outputDir = Path.Combine(workDir, "checker-output");
        Directory.CreateDirectory(outputDir);

        Pack(context.SolutionRoot, zipPath);
        var sarifJson = Check(zipPath, outputDir);
        var findings = ParseSarif(sarifJson);

        return BuildVerdicts(context, findings);
    }

    /// <summary>
    /// Maps parsed findings to verdicts. Pure: no filesystem, no process, no
    /// network. Kept separate from <see cref="Evaluate"/> and public (like
    /// <see cref="ParseSarif"/>) so the severity mapping rule (Critical/High
    /// refuses, Medium and below are counted) is directly unit-testable
    /// against synthetic findings, without starting pac or a process.
    /// </summary>
    public static IReadOnlyList<GateVerdict> BuildVerdicts(
        GateContext context, IReadOnlyList<CheckerFinding> findings)
    {
        var artifact = RelativeArtifact(context, context.SolutionRoot);
        var refusals = new List<GateVerdict>();

        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Critical"] = 0,
            ["High"] = 0,
            ["Medium"] = 0,
            ["Low"] = 0,
            ["Informational"] = 0
        };

        foreach (var finding in findings)
        {
            counts[finding.Severity]++;

            if (finding.Severity is not ("Critical" or "High"))
                continue;

            refusals.Add(Verdict(
                context,
                GateOutcome.Refuse,
                artifact,
                $"pac solution check (ruleset '{RuleSetName}', geo {Geo}) flagged rule "
                + $"'{finding.RuleId}' at severity {finding.Severity}.",
                $"{finding.Message} (file: {finding.FileReference ?? "not reported by the checker"})"));
        }

        if (refusals.Count > 0)
            return refusals;

        // Whole-solution Pass, including the zero-findings case: the ruleset
        // and the exact per-severity counts are the evidence, so "nothing was
        // found" and "nothing was checked" can never look the same in the
        // ledger.
        return
        [
            Verdict(
                context,
                GateOutcome.Pass,
                artifact,
                $"pac solution check (ruleset '{RuleSetName}', geo {Geo}) inspected {artifact}: "
                + $"{counts["Critical"]} Critical, {counts["High"]} High, {counts["Medium"]} Medium, "
                + $"{counts["Low"]} Low, {counts["Informational"]} Informational finding(s) out of "
                + $"{findings.Count} total.")
        ];
    }

    /// <summary>
    /// Parses the checker's SARIF result file into findings with a resolved
    /// severity. Pure function: takes the SARIF text, returns data, touches
    /// nothing else. This is the seam the unit tests exercise directly with
    /// synthetic SARIF, so parsing and severity mapping are provably correct
    /// without ever invoking pac or the network.
    /// <para>
    /// WHY severity is looked up per rule rather than read off the result:
    /// SARIF's own <c>level</c> field is a generic note/warning/error/none
    /// classification. The Power Apps Checker's five-level severity
    /// (Critical/High/Medium/Low/Informational, the same scale
    /// <c>pac solution check</c> prints in its own summary table) is carried
    /// as a custom property on each rule definition in
    /// <c>runs[0].tool.driver.rules[].properties.severity</c>, confirmed by
    /// running the real checker against demo-solution during this slice.
    /// Every result only carries a <c>ruleId</c>, so severity is resolved by
    /// joining against that rule table.
    /// </para>
    /// </summary>
    public static IReadOnlyList<CheckerFinding> ParseSarif(string sarifJson)
    {
        using var doc = Parse(sarifJson);

        if (!doc.RootElement.TryGetProperty("runs", out var runsEl)
            || runsEl.ValueKind != JsonValueKind.Array
            || runsEl.GetArrayLength() == 0)
        {
            throw new InvalidDataException(
                "Checker output has no 'runs' array; cannot determine findings.");
        }

        var run = runsEl[0];
        var severityByRule = ReadRuleSeverities(run);

        if (!run.TryGetProperty("results", out var resultsEl) || resultsEl.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Checker output has no 'results' array; cannot determine findings.");

        var findings = new List<CheckerFinding>();

        foreach (var result in resultsEl.EnumerateArray())
        {
            if (!result.TryGetProperty("ruleId", out var ruleIdEl) || ruleIdEl.GetString() is not { } ruleId)
                throw new InvalidDataException("Checker result is missing 'ruleId'.");

            if (!severityByRule.TryGetValue(ruleId, out var severity))
            {
                throw new InvalidDataException(
                    $"Checker result references rule '{ruleId}' with no matching severity in "
                    + "tool.driver.rules; cannot classify this finding.");
            }

            if (severity is not ("Critical" or "High" or "Medium" or "Low" or "Informational"))
            {
                throw new InvalidDataException(
                    $"Checker rule '{ruleId}' has unrecognised severity '{severity}'.");
            }

            var message = result.TryGetProperty("message", out var messageEl)
                && messageEl.TryGetProperty("text", out var textEl)
                    ? textEl.GetString() ?? string.Empty
                    : string.Empty;

            findings.Add(new CheckerFinding(ruleId, severity, message, ReadFileReference(result)));
        }

        return findings;
    }

    private static Dictionary<string, string> ReadRuleSeverities(JsonElement run)
    {
        var severityByRule = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!run.TryGetProperty("tool", out var toolEl)
            || !toolEl.TryGetProperty("driver", out var driverEl)
            || !driverEl.TryGetProperty("rules", out var rulesEl)
            || rulesEl.ValueKind != JsonValueKind.Array)
        {
            return severityByRule;
        }

        foreach (var rule in rulesEl.EnumerateArray())
        {
            if (!rule.TryGetProperty("id", out var idEl) || idEl.GetString() is not { } id)
                continue;

            if (rule.TryGetProperty("properties", out var propsEl)
                && propsEl.TryGetProperty("severity", out var sevEl)
                && sevEl.GetString() is { } severity)
            {
                severityByRule[id] = severity;
            }
        }

        return severityByRule;
    }

    private static string? ReadFileReference(JsonElement result)
    {
        if (!result.TryGetProperty("locations", out var locationsEl)
            || locationsEl.ValueKind != JsonValueKind.Array
            || locationsEl.GetArrayLength() == 0)
        {
            return null;
        }

        var first = locationsEl[0];

        if (first.TryGetProperty("physicalLocation", out var physEl)
            && physEl.TryGetProperty("artifactLocation", out var artEl)
            && artEl.TryGetProperty("uri", out var uriEl))
        {
            return uriEl.GetString();
        }

        return null;
    }

    private static JsonDocument Parse(string sarifJson)
    {
        try
        {
            return JsonDocument.Parse(sarifJson);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Checker output is not valid JSON: {ex.Message}", ex);
        }
    }

    // Process execution. Everything below writes to disk or starts a
    // process, which is exactly why nothing above does.

    private static void Pack(string solutionRoot, string zipPath)
    {
        var psi = NewPacStartInfo();
        psi.ArgumentList.Add("solution");
        psi.ArgumentList.Add("pack");
        psi.ArgumentList.Add("--zipfile");
        psi.ArgumentList.Add(zipPath);
        psi.ArgumentList.Add("--folder");
        psi.ArgumentList.Add(solutionRoot);
        psi.ArgumentList.Add("--packagetype");
        psi.ArgumentList.Add("Unmanaged");

        RunPac(psi, "solution pack", PackTimeout);
    }

    private static string Check(string zipPath, string outputDir)
    {
        var psi = NewPacStartInfo();
        psi.ArgumentList.Add("solution");
        psi.ArgumentList.Add("check");
        psi.ArgumentList.Add("--path");
        psi.ArgumentList.Add(zipPath);
        psi.ArgumentList.Add("--outputDirectory");
        psi.ArgumentList.Add(outputDir);
        psi.ArgumentList.Add("--geo");
        psi.ArgumentList.Add(Geo);
        psi.ArgumentList.Add("--ruleSet");
        psi.ArgumentList.Add(RuleSetName);

        RunPac(psi, "solution check", CheckTimeout);

        return ReadSarif(outputDir);
    }

    /// <summary>
    /// <c>pac solution check --outputDirectory</c> does not write a .sarif
    /// file directly; it downloads a results zip (observed running the real
    /// checker against demo-solution during this slice) containing one. Both
    /// shapes are accepted here so a future pac version that writes the sarif
    /// directly does not silently break this gate.
    /// </summary>
    private static string ReadSarif(string outputDir)
    {
        var files = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories);

        if (files.Length == 0)
        {
            throw new InvalidDataException(
                $"'pac solution check' reported success but wrote no files to {outputDir}.");
        }

        var sarifFile = files.FirstOrDefault(f => f.EndsWith(".sarif", StringComparison.OrdinalIgnoreCase));
        if (sarifFile is not null)
            return File.ReadAllText(sarifFile);

        var zipFile = files.FirstOrDefault(f => f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
        if (zipFile is null)
        {
            throw new InvalidDataException(
                "'pac solution check' produced no .sarif or .zip result file. Found: "
                + string.Join(", ", files.Select(Path.GetFileName)));
        }

        var extractDir = Path.Combine(outputDir, "extracted");
        ZipFile.ExtractToDirectory(zipFile, extractDir);

        var extracted = Directory.GetFiles(extractDir, "*.sarif", SearchOption.AllDirectories).FirstOrDefault();
        if (extracted is null)
            throw new InvalidDataException($"Checker result archive {zipFile} contained no .sarif file.");

        return File.ReadAllText(extracted);
    }

    private static ProcessStartInfo NewPacStartInfo() => new()
    {
        FileName = "pac",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    /// <summary>
    /// Runs one pac invocation to completion and fails closed on every way it
    /// can go wrong: missing from PATH, non-zero exit, or a hang past
    /// <paramref name="timeout"/>. None of these become a Skip; Skip is
    /// reserved for the credential-absent case GateRunner already handles
    /// before this gate ever runs. Everything here throws, which GateRunner
    /// converts into a Refuse, per the frozen fail-closed rule in Gate.cs.
    /// </summary>
    private static void RunPac(ProcessStartInfo psi, string description, TimeSpan timeout)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Could not start 'pac {description}'. pac must be installed and on PATH "
                + "(dotnet tool install --global Microsoft.PowerApps.CLI.Tool).", ex);
        }

        // Async readers, not process.StandardOutput.ReadToEnd(), because pac
        // writes enough to both streams that a synchronous read-then-wait can
        // deadlock once either pipe's OS buffer fills.
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var exited = process.WaitForExit((int)timeout.TotalMilliseconds);

        if (!exited)
        {
            TryKill(process);
            throw new TimeoutException(
                $"'pac {description}' did not finish within {timeout}. Treating an unknown "
                + "result as a failure rather than guessing.");
        }

        // Guarantees the async output/error handlers have flushed before the
        // captured text is used in an exception message below.
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'pac {description}' exited with code {process.ExitCode}."
                + $"{Environment.NewLine}stdout:{Environment.NewLine}{stdout}"
                + $"{Environment.NewLine}stderr:{Environment.NewLine}{stderr}");
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort. The TimeoutException is already the honest report;
            // a failed kill must not mask it with a different exception.
        }
    }

    private static string TempRoot() =>
        Environment.GetEnvironmentVariable("RUNNER_TEMP") is { Length: > 0 } runnerTemp
            ? runnerTemp
            : Path.GetTempPath();

    private static string RelativeArtifact(GateContext context, string absolutePath) =>
        Path.GetRelativePath(context.RepositoryRoot, absolutePath).Replace('\\', '/');

    private static GateVerdict Verdict(
        GateContext context,
        GateOutcome outcome,
        string artifact,
        string evidence,
        string? reason = null) => new()
        {
            GateId = "G7",
            GateName = "power-apps-checker",
            Outcome = outcome,
            Artifact = artifact,
            Evidence = evidence,
            Reason = reason,
            At = context.Now,
            Stage = context.Stage
        };
}

/// <summary>
/// One Power Apps Checker finding, severity already resolved from the SARIF
/// rule table so callers never need to see the rules array themselves.
/// </summary>
/// <param name="RuleId">The checker rule that fired, for example "web-avoid-eval".</param>
/// <param name="Severity">Critical, High, Medium, Low, or Informational.</param>
/// <param name="Message">The finding's own explanation, as reported by the checker.</param>
/// <param name="FileReference">
/// Repository-relative-to-the-zip file the finding points at, or null when the
/// checker did not attach a location.
/// </param>
public sealed record CheckerFinding(string RuleId, string Severity, string Message, string? FileReference);
