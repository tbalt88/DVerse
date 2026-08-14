using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace DVerse.Harness.Gates;

/// <summary>
/// G6. Discovers every <c>*.csproj</c> under the solution root and shells
/// <c>dotnet test</c> against each one (which builds first, since <c>dotnet
/// test</c> always builds before running). A build failure or any failed
/// test is a Refuse for that project. A project whose build succeeds and
/// whose tests all pass, with zero skipped, is a Pass.
/// <para>
/// Zero discovered projects is a genuine Pass, not a Skip: the demo solution
/// is declarative-only until wave 4.4 introduces plugin projects, so "no
/// code to build or test" is currently true and the gate says so honestly
/// rather than reporting nothing.
/// </para>
/// <para>
/// Per house law, a test that skips is not a test that passes: a project
/// whose build and tests otherwise succeed but which reports one or more
/// skipped tests is a Refuse, and the skipped tests are named.
/// </para>
/// <para>
/// Process execution and result parsing are kept apart on purpose, the same
/// separation <see cref="PowerAppsCheckerGate"/> uses for <c>pac</c>:
/// <see cref="ParseDotnetTestOutput"/> and <see cref="BuildVerdict"/> are
/// pure functions over captured text, unit-testable with synthetic
/// <c>dotnet test</c> output and never starting a process. Only
/// <see cref="Evaluate"/> and <see cref="RunDotnetTest"/> touch the process
/// and the filesystem, and only those paths are exercised by the
/// fixture-driven integration tests that run the real <c>dotnet</c> against
/// the tiny fixtures under <c>harness/fixtures/g6/</c>.
/// </para>
/// <para>
/// HONEST TENSION with the frozen <see cref="IGate"/> contract, the same one
/// flagged on <see cref="PowerAppsCheckerGate"/>: "Implementations must be
/// pure with respect to the filesystem... write nothing." <c>dotnet test</c>
/// necessarily writes build output (<c>bin/</c>, <c>obj/</c>) next to each
/// project it builds, which for a real plugin project would be inside the
/// solution root, i.e. inside the repository. There is no way to invoke
/// <c>dotnet test</c> without this. Those directories are gitignored, never
/// committed, and this gate still returns only a verdict; nothing it writes
/// is treated as gate output. Flagged here rather than silently worked
/// around, as the contract's own docstring now permits for exactly this
/// class of case.
/// </para>
/// </summary>
public sealed class BuildAndTestsGate : IGate
{
    /// <summary>
    /// Generous on purpose per the frozen ruling: a hung <c>dotnet test</c>
    /// must be a Refuse via the fail-closed timeout below, not a false pass
    /// from giving up too early, and real projects (unlike the tiny
    /// fixtures) may need a first-time restore.
    /// </summary>
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(10);

    public string Id => "G6";
    public string Name => "build-and-tests";
    public bool RequiresTenant => false;

    public IEnumerable<GateVerdict> Evaluate(GateContext context)
    {
        var projects = Directory
            .GetFiles(context.SolutionRoot, "*.csproj", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        if (projects.Count == 0)
        {
            yield return Verdict(
                context,
                GateOutcome.Pass,
                RelativeArtifact(context, context.SolutionRoot),
                "No *.csproj files found under the solution root; zero projects to build "
                + "or test. Expected: the demo solution is declarative only (YAML, not "
                + "code) until wave 4.4 introduces plugin projects. Honest vacuity, not "
                + "a claim that anything was exercised.");
            yield break;
        }

        foreach (var project in projects)
        {
            var artifact = RelativeArtifact(context, project);
            var (stdout, stderr) = RunDotnetTest(project);
            var parsed = ParseDotnetTestOutput(stdout, stderr);
            yield return BuildVerdict(context, artifact, parsed);
        }
    }

    /// <summary>
    /// Maps one project's parsed <c>dotnet test</c> output to a verdict.
    /// Pure: no filesystem, no process. Kept separate and public, like
    /// <see cref="ParseDotnetTestOutput"/>, so the Refuse/Pass rules are
    /// directly unit-testable against synthetic <see cref="DotnetTestRun"/>
    /// values.
    /// </summary>
    public static GateVerdict BuildVerdict(GateContext context, string artifact, DotnetTestRun run)
    {
        if (!run.TestsRan)
        {
            return Verdict(
                context,
                GateOutcome.Refuse,
                artifact,
                $"'dotnet test' against {artifact} produced no test-run summary; the run did "
                + "not complete normally (build failure, or the run crashed before any test "
                + "could report).",
                $"Build or run failed for {artifact}. Output tail:{Environment.NewLine}{run.Tail}");
        }

        if (run.Failed > 0)
        {
            return Verdict(
                context,
                GateOutcome.Refuse,
                artifact,
                $"'dotnet test' against {artifact}: {run.Passed} passed, {run.Failed} failed, "
                + $"{run.Skipped} skipped, {run.Total} total.",
                $"{run.Failed} test(s) failed for {artifact}. Output tail:"
                + $"{Environment.NewLine}{run.Tail}");
        }

        if (run.Skipped > 0)
        {
            var names = string.Join(", ", run.SkippedTestNames);
            return Verdict(
                context,
                GateOutcome.Refuse,
                artifact,
                $"'dotnet test' against {artifact}: {run.Passed} passed, 0 failed, "
                + $"{run.Skipped} skipped, {run.Total} total. Skipped: {names}.",
                $"{run.Skipped} test(s) skipped for {artifact}: {names}. Per house rule, "
                + "tests that skip are not tests that pass; an otherwise green project with "
                + "a nonzero skip count is refused.");
        }

        return Verdict(
            context,
            GateOutcome.Pass,
            artifact,
            $"'dotnet test' against {artifact}: {run.Passed} passed, 0 failed, 0 skipped, "
            + $"{run.Total} total.");
    }

    // Parsing. Pure with respect to everything: given captured text, returns
    // data. This is the seam the unit tests exercise directly with synthetic
    // dotnet-test-shaped output, so the summary/skip parsing is provably
    // correct without ever starting a process.

    private const int TailLineCount = 40;

    /// <summary>
    /// Matches the VSTest console-runner summary line, for example:
    /// <c>Passed!  - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: 65 ms - Fixture.Pass.dll (net10.0)</c>
    /// Observed running the real dotnet test against the pass and
    /// refuse-failing-test fixtures during this slice. Absence of this line
    /// anywhere in the captured output is treated as "the run did not
    /// complete", which covers both a genuine build failure (no summary is
    /// ever printed) and any other way the process could fail to reach a
    /// verdict, fail-closed rather than guessed.
    /// </summary>
    private static readonly Regex SummaryLine = new(
        @"^(?:Passed|Failed|Skipped)!\s*-\s*Failed:\s*(?<failed>\d+),\s*Passed:\s*(?<passed>\d+),"
        + @"\s*Skipped:\s*(?<skipped>\d+),\s*Total:\s*(?<total>\d+)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// Matches one skipped-test result line, for example:
    /// <c>  Skipped Fixture.Pass.SomeTest [1 ms]</c>. Observed running the
    /// real checker's VSTest output; used only to name skipped tests in
    /// Evidence, never to compute the skip count (the summary line already
    /// carries that authoritatively).
    /// </summary>
    private static readonly Regex SkippedResultLine = new(
        @"^\s*Skipped\s+(?<name>\S+)\s*\[",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public static DotnetTestRun ParseDotnetTestOutput(string standardOutput, string standardError)
    {
        var combined = Combine(standardOutput, standardError);

        var summaryMatches = SummaryLine.Matches(combined);

        if (summaryMatches.Count == 0)
        {
            return new DotnetTestRun(
                TestsRan: false,
                Passed: 0,
                Failed: 0,
                Skipped: 0,
                Total: 0,
                SkippedTestNames: [],
                Tail: Tail(combined));
        }

        // Summed across matches rather than taking the first: a multi-TFM
        // project prints one summary line per target framework, and a
        // single project run must still be judged on its total across all
        // of them, not just the first one encountered.
        var passed = 0;
        var failed = 0;
        var skipped = 0;
        var total = 0;

        foreach (Match match in summaryMatches)
        {
            passed += int.Parse(match.Groups["passed"].Value);
            failed += int.Parse(match.Groups["failed"].Value);
            skipped += int.Parse(match.Groups["skipped"].Value);
            total += int.Parse(match.Groups["total"].Value);
        }

        var skippedNames = SkippedResultLine
            .Matches(combined)
            .Select(m => m.Groups["name"].Value)
            .ToList();

        return new DotnetTestRun(true, passed, failed, skipped, total, skippedNames, Tail(combined));
    }

    /// <summary>
    /// Joins stdout and stderr for scanning, without injecting a spurious
    /// blank line when one stream is empty (the common case: dotnet test
    /// writes almost everything to stdout).
    /// </summary>
    private static string Combine(string standardOutput, string standardError)
    {
        var stdout = standardOutput ?? string.Empty;
        var stderr = standardError ?? string.Empty;

        if (stderr.Length == 0)
            return stdout;

        return stdout + Environment.NewLine + stderr;
    }

    /// <summary>
    /// Last <see cref="TailLineCount"/> lines of the captured output, so a
    /// Refuse reason is concrete enough to act on without pasting the whole
    /// (potentially very long) build/test transcript into the ledger.
    /// </summary>
    private static string Tail(string combined)
    {
        var lines = combined.Replace("\r\n", "\n").Split('\n');
        var start = Math.Max(0, lines.Length - TailLineCount);
        return string.Join(Environment.NewLine, lines[start..]).Trim();
    }

    // Process execution. Everything below starts a process or touches disk,
    // which is exactly why nothing above does.

    private static (string StandardOutput, string StandardError) RunDotnetTest(string csprojPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("test");
        psi.ArgumentList.Add(csprojPath);

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
                "Could not start 'dotnet test'. The dotnet SDK must be installed and on PATH.", ex);
        }

        // Async readers, not process.StandardOutput.ReadToEnd(), because
        // dotnet test can write enough to both streams that a synchronous
        // read-then-wait can deadlock once either pipe's OS buffer fills.
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var exited = process.WaitForExit((int)TestTimeout.TotalMilliseconds);

        if (!exited)
        {
            TryKill(process);
            throw new TimeoutException(
                $"'dotnet test {csprojPath}' did not finish within {TestTimeout}. Treating an "
                + "unknown result as a failure rather than guessing.");
        }

        // Guarantees the async output/error handlers have flushed before the
        // captured text is used.
        process.WaitForExit();

        return (stdout.ToString(), stderr.ToString());
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort. The TimeoutException is already the honest
            // report; a failed kill must not mask it with a different one.
        }
    }

    private static string RelativeArtifact(GateContext context, string absolutePath) =>
        Path.GetRelativePath(context.RepositoryRoot, absolutePath).Replace('\\', '/');

    private static GateVerdict Verdict(
        GateContext context,
        GateOutcome outcome,
        string artifact,
        string evidence,
        string? reason = null) => new()
        {
            GateId = "G6",
            GateName = "build-and-tests",
            Outcome = outcome,
            Artifact = artifact,
            Evidence = evidence,
            Reason = reason,
            At = context.Now,
            Stage = context.Stage
        };
}

/// <summary>
/// One project's parsed <c>dotnet test</c> outcome. <see cref="TestsRan"/>
/// false means the VSTest summary line was never found in the captured
/// output, which is treated as the run not having completed (build failure
/// or worse); in that case <see cref="Passed"/>, <see cref="Failed"/>,
/// <see cref="Skipped"/>, and <see cref="Total"/> are all zero and carry no
/// meaning beyond that.
/// </summary>
/// <param name="TestsRan">Whether a VSTest summary line was found at all.</param>
/// <param name="Passed">Passed test count, summed across all summary lines found.</param>
/// <param name="Failed">Failed test count, summed across all summary lines found.</param>
/// <param name="Skipped">Skipped test count, summed across all summary lines found.</param>
/// <param name="Total">Total test count, summed across all summary lines found.</param>
/// <param name="SkippedTestNames">Fully qualified names of every skipped test, for Evidence.</param>
/// <param name="Tail">Last lines of the captured output, for a Refuse Reason.</param>
public sealed record DotnetTestRun(
    bool TestsRan,
    int Passed,
    int Failed,
    int Skipped,
    int Total,
    IReadOnlyList<string> SkippedTestNames,
    string Tail);
