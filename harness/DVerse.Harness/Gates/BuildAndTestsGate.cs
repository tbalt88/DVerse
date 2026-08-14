using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace DVerse.Harness.Gates;

/// <summary>
/// G6. Discovers every <c>*.csproj</c> under the solution root and, for
/// each one, FIRST classifies it as a test project or not via
/// <c>dotnet msbuild -getProperty:IsTestProject</c> (the frozen ruling for
/// slice G6b), then routes it to the matching check. Test projects
/// (<c>IsTestProject</c> true) go through <c>dotnet test</c> exactly as
/// before: a build failure or any failed test is a Refuse, and a project
/// whose build succeeds and whose tests all pass, with zero skipped, is a
/// Pass. Non-test projects (a plain library, <c>IsTestProject</c> false or
/// empty) go through <c>dotnet build</c> instead: no VSTest summary is ever
/// produced or expected, so a clean build alone is a Pass, on the frozen
/// premise that the mandated layout keeps a non-test project's tests in a
/// SIBLING project, never nested inside it, where G6 will find and run them
/// separately.
/// <para>
/// Zero discovered projects is a genuine Pass, not a Skip: the demo solution
/// is declarative-only until wave 4.4 introduces plugin projects, so "no
/// code to build or test" is currently true and the gate says so honestly
/// rather than reporting nothing.
/// </para>
/// <para>
/// Per house law, a test that skips is not a test that passes: a project
/// whose build and tests otherwise succeed but which reports one or more
/// skipped tests is a Refuse, and the skipped tests are named. This rule
/// only ever applies to test-classified projects; a non-test project has no
/// tests of its own to skip by definition.
/// </para>
/// <para>
/// Classification is fail-closed by the same frozen ruling: a nonzero exit
/// from either step, or output that is neither empty, <c>false</c>, nor
/// <c>true</c>, is a Refuse rather than a guess, because routing an
/// unclassifiable project to the wrong check (silently skipping its real
/// tests via <c>dotnet build</c>, or wasting a test run on a plain library)
/// would be worse than refusing.
/// </para>
/// <para>
/// EMPIRICAL FINDING made while building this slice: querying
/// <c>-getProperty:IsTestProject</c> alone, with no prior restore, reports
/// EMPTY for every project, test or not, because the property is only set by
/// an SDK import (<c>Microsoft.NET.Test.Sdk</c>'s targets) that itself only
/// gets pulled in once the restore-generated <c>obj/*.nuget.g.props</c>
/// exist on disk. On a fresh checkout that would silently misclassify a real
/// test project as non-test, i.e. it would never run that project's tests
/// and would still report a Pass, exactly the kind of gamed result this
/// house refuses to produce. Also observed: a single <c>dotnet msbuild
/// -restore -getProperty:X</c> invocation does NOT fix this, because
/// <c>-getProperty</c> evaluates properties from the project graph built up
/// front, before <c>-restore</c>'s own target execution completes within
/// that same process. The only combination observed to work reliably is two
/// separate process invocations, restore then classify, which is what
/// <see cref="Evaluate"/> does before routing each project.
/// </para>
/// <para>
/// Process execution and result parsing are kept apart on purpose, the same
/// separation <see cref="PowerAppsCheckerGate"/> uses for <c>pac</c>:
/// <see cref="ParseDotnetTestOutput"/>, <see cref="BuildVerdict"/>,
/// <see cref="ClassifyProject"/>, and <see cref="BuildNonTestVerdict"/> are
/// pure functions over captured text and exit codes, unit-testable with
/// synthetic <c>dotnet</c> output and never starting a process. Only
/// <see cref="Evaluate"/> and <see cref="RunProcess"/> touch the process and
/// the filesystem, and only those paths are exercised by the fixture-driven
/// integration tests that run the real <c>dotnet</c> against the tiny
/// fixtures under <c>harness/fixtures/g6/</c>.
/// </para>
/// <para>
/// HONEST TENSION with the frozen <see cref="IGate"/> contract, the same one
/// flagged on <see cref="PowerAppsCheckerGate"/>: "Implementations must be
/// pure with respect to the filesystem... write nothing." <c>dotnet
/// restore</c>, <c>dotnet build</c>, and <c>dotnet test</c> necessarily
/// write build output (<c>bin/</c>, <c>obj/</c>) next to each project they
/// touch, which for a real plugin project would be inside the solution
/// root, i.e. inside the repository. There is no way to invoke any of them
/// without this. Those directories are gitignored, never committed, and
/// this gate still returns only a verdict; nothing it writes is treated as
/// gate output. Flagged here rather than silently worked around, as the
/// contract's own docstring now permits for exactly this class of case.
/// </para>
/// </summary>
public sealed class BuildAndTestsGate : IGate
{
    /// <summary>
    /// Generous on purpose per the frozen ruling: a hung <c>dotnet</c>
    /// invocation must be a Refuse via the fail-closed timeout below, not a
    /// false pass from giving up too early, and real projects (unlike the
    /// tiny fixtures) may need a first-time restore. Shared by every
    /// <c>dotnet</c> process this gate starts: restore, classify, build, and
    /// test.
    /// </summary>
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(10);

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

            // Classify FIRST, per the frozen ruling: restore, then query
            // IsTestProject. Two separate process invocations on purpose;
            // see the EMPIRICAL FINDING on the class docstring for why a
            // single combined invocation is not reliable here.
            var restoreResult = RunProcess(["restore", project], ProcessTimeout);
            var propertyResult = RunProcess(["msbuild", project, "-getProperty:IsTestProject"], ProcessTimeout);
            var classification = ClassifyProject(restoreResult, propertyResult);

            if (!classification.Succeeded)
            {
                yield return Verdict(
                    context,
                    GateOutcome.Refuse,
                    artifact,
                    $"Could not classify {artifact} via 'dotnet msbuild -getProperty:IsTestProject'; "
                    + "restore or the property query did not complete cleanly, or produced "
                    + "unparseable output.",
                    $"Classification failed for {artifact}. Fail closed: an unclassifiable "
                    + $"project cannot safely be routed to either 'dotnet test' or 'dotnet build'. "
                    + $"Output tail:{Environment.NewLine}{SanitizePaths(context, classification.Tail)}");
                continue;
            }

            if (classification.Kind == ProjectKind.Test)
            {
                var (stdout, stderr) = RunDotnetTest(project);
                var parsed = ParseDotnetTestOutput(stdout, stderr);
                yield return BuildVerdict(context, artifact, parsed);
            }
            else
            {
                var buildResult = RunProcess(["build", project], ProcessTimeout);
                yield return BuildNonTestVerdict(context, artifact, buildResult);
            }
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
                $"Build or run failed for {artifact}. Output tail:{Environment.NewLine}"
                + SanitizePaths(context, run.Tail));
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
                + $"{Environment.NewLine}{SanitizePaths(context, run.Tail)}");
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

    /// <summary>
    /// Maps one project's build result to a verdict, for the non-test
    /// branch only. Pure: no filesystem, no process. Unlike
    /// <see cref="BuildVerdict"/> there is no VSTest summary to look for and
    /// none is required; a nonzero exit is the only signal of failure, per
    /// the frozen ruling for this slice.
    /// </summary>
    public static GateVerdict BuildNonTestVerdict(GateContext context, string artifact, ProcessResult build)
    {
        if (build.ExitCode != 0)
        {
            return Verdict(
                context,
                GateOutcome.Refuse,
                artifact,
                $"'dotnet build' against {artifact} exited {build.ExitCode}.",
                $"Build failed for {artifact}. Output tail:{Environment.NewLine}"
                + SanitizePaths(context, Tail(Combine(build.StandardOutput, build.StandardError))));
        }

        return Verdict(
            context,
            GateOutcome.Pass,
            artifact,
            "built clean; not a test project (IsTestProject false); its tests live in "
            + "sibling projects.");
    }

    /// <summary>
    /// Classifies one project as a test project or not, from the captured
    /// results of the two process invocations <see cref="Evaluate"/> always
    /// runs in order: restore, then <c>-getProperty:IsTestProject</c>. Pure:
    /// no filesystem, no process, directly unit-testable against synthetic
    /// <see cref="ProcessResult"/> values.
    /// <para>
    /// Fail-closed per the frozen ruling: a nonzero exit from either step,
    /// or property output that is neither empty, <c>false</c>, nor
    /// <c>true</c> (case-insensitive, trimmed), yields
    /// <see cref="ClassificationResult.Succeeded"/> false rather than a
    /// guessed <see cref="ProjectKind"/>. Empty and <c>false</c> are both
    /// treated as non-test, matching the frozen ruling's own wording ("a
    /// plain library reports false or empty") and the empirical finding on
    /// the class docstring: a project with no Test SDK reference never gets
    /// <c>IsTestProject</c> set at all, restored or not, so empty is its
    /// normal, honest signal, not a failure.
    /// </para>
    /// </summary>
    public static ClassificationResult ClassifyProject(ProcessResult restore, ProcessResult property)
    {
        if (restore.ExitCode != 0)
        {
            return new ClassificationResult(
                Succeeded: false,
                Kind: ProjectKind.NonTest,
                Tail: Tail(Combine(restore.StandardOutput, restore.StandardError)));
        }

        if (property.ExitCode != 0)
        {
            return new ClassificationResult(
                Succeeded: false,
                Kind: ProjectKind.NonTest,
                Tail: Tail(Combine(property.StandardOutput, property.StandardError)));
        }

        var value = Combine(property.StandardOutput, property.StandardError).Trim();

        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            return new ClassificationResult(true, ProjectKind.Test, string.Empty);

        if (value.Length == 0 || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            return new ClassificationResult(true, ProjectKind.NonTest, string.Empty);

        // Neither empty, true, nor false: something unexpected came back
        // (an MSBuild warning mixed into the property value, a garbled
        // build, etc). Fail closed rather than guess which branch is safe.
        return new ClassificationResult(
            Succeeded: false,
            Kind: ProjectKind.NonTest,
            Tail: Tail(Combine(property.StandardOutput, property.StandardError)));
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
    /// Scrubs absolute filesystem paths from tool output before it enters a
    /// ledger verdict. The ledger is repo-root-relative by contract and lands
    /// in a public repo; raw dotnet output embeds the machine's directory
    /// layout (test dll paths, source paths in stack traces). Known roots are
    /// rewritten to relative form; any remaining drive-rooted directory prefix
    /// is stripped down to the file name so compiler style "file(line,col)"
    /// detail survives. Added at wave 3 integration when the leak-scan test
    /// caught G6's raw tail, the scan's first real catch since the wave 1
    /// runner defect.
    /// </summary>
    private static string SanitizePaths(GateContext context, string text)
    {
        var repo = Path.TrimEndingDirectorySeparator(Path.GetFullPath(context.RepositoryRoot));
        foreach (var root in new[] { repo, repo.Replace('\\', '/') })
        {
            text = text.Replace(root, ".", StringComparison.OrdinalIgnoreCase);
        }

        return System.Text.RegularExpressions.Regex.Replace(
            text, @"[A-Za-z]:[\\/](?:[^\s""':*?<>|\\/]+[\\/])*", string.Empty);
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

    /// <summary>
    /// Thin wrapper over <see cref="RunProcess"/> for the test path, kept so
    /// the call site in <see cref="Evaluate"/> reads the same as before this
    /// slice. Exit code is deliberately not part of the return value: unlike
    /// classification and the non-test build path, the test path never
    /// looks at <c>dotnet test</c>'s own exit code, only at whether a VSTest
    /// summary line shows up in the captured text (see
    /// <see cref="ParseDotnetTestOutput"/>), which is what it did before
    /// this slice too.
    /// </summary>
    private static (string StandardOutput, string StandardError) RunDotnetTest(string csprojPath)
    {
        var result = RunProcess(["test", csprojPath], ProcessTimeout);
        return (result.StandardOutput, result.StandardError);
    }

    /// <summary>
    /// Runs <c>dotnet</c> with the given arguments to completion and
    /// captures its exit code, stdout, and stderr. Shared by every
    /// <c>dotnet</c> invocation this gate makes: restore, classify, build,
    /// and (via <see cref="RunDotnetTest"/>) test. A hang past
    /// <paramref name="timeout"/> is killed and thrown as a
    /// <see cref="TimeoutException"/>, fail-closed rather than guessed.
    /// </summary>
    private static ProcessResult RunProcess(IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        var commandLine = "dotnet " + string.Join(' ', arguments);

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
                $"Could not start '{commandLine}'. The dotnet SDK must be installed and on PATH.", ex);
        }

        // Async readers, not process.StandardOutput.ReadToEnd(), because
        // dotnet can write enough to both streams that a synchronous
        // read-then-wait can deadlock once either pipe's OS buffer fills.
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var exited = process.WaitForExit((int)timeout.TotalMilliseconds);

        if (!exited)
        {
            TryKill(process);
            throw new TimeoutException(
                $"'{commandLine}' did not finish within {timeout}. Treating an unknown result "
                + "as a failure rather than guessing.");
        }

        // Guarantees the async output/error handlers have flushed before the
        // captured text is used.
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
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

/// <summary>
/// Captured result of one <c>dotnet</c> process invocation: its exit code
/// and the full captured stdout and stderr text. Deliberately generic (not
/// test-shaped or build-shaped) because <see cref="BuildAndTestsGate"/>
/// reuses it for restore, classification, build, and test invocations
/// alike.
/// </summary>
/// <param name="ExitCode">The process's exit code.</param>
/// <param name="StandardOutput">Captured standard output.</param>
/// <param name="StandardError">Captured standard error.</param>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Whether a project is a test project or not, as decided by
/// <see cref="BuildAndTestsGate.ClassifyProject"/>. Meaningless when the
/// classification that produced it did not succeed; see
/// <see cref="ClassificationResult.Succeeded"/>.
/// </summary>
public enum ProjectKind
{
    Test,
    NonTest
}

/// <summary>
/// Outcome of classifying one project via <c>IsTestProject</c>.
/// <see cref="Succeeded"/> false means classification itself failed
/// (nonzero exit from restore or the property query, or unparseable
/// property output); in that case <see cref="Kind"/> carries no meaning and
/// <see cref="Tail"/> holds the captured output for a Refuse Reason.
/// <see cref="Succeeded"/> true means <see cref="Kind"/> is authoritative
/// and <see cref="Tail"/> is empty and unused.
/// </summary>
/// <param name="Succeeded">Whether classification completed cleanly.</param>
/// <param name="Kind">The decided project kind, meaningful only when <paramref name="Succeeded"/> is true.</param>
/// <param name="Tail">Captured output tail, meaningful only when <paramref name="Succeeded"/> is false.</param>
public sealed record ClassificationResult(bool Succeeded, ProjectKind Kind, string Tail);
