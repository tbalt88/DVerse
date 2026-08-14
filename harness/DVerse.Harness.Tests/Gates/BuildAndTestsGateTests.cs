using DVerse.Harness;
using DVerse.Harness.Gates;

namespace DVerse.Harness.Tests.Gates;

/// <summary>
/// G6 classifies each project first (slice G6b) via
/// <c>dotnet msbuild -getProperty:IsTestProject</c>, then routes it: test
/// projects go through <c>dotnet test</c> exactly as before wave G6b, and
/// non-test projects (a plain library) go through <c>dotnet build</c>, no
/// VSTest summary expected or required. What this gate owns is (1) parsing
/// <c>dotnet test</c> console output into pass/fail/skip counts and skipped
/// test names, (2) classifying a project from restore and property-query
/// results, and (3) mapping each of those to a verdict: for test projects,
/// build failure or any failed test refuses, a nonzero skip count on an
/// otherwise green project also refuses (tests that skip are not tests that
/// pass), and everything else passes; for non-test projects, a nonzero
/// build exit refuses and a clean build passes; classification itself
/// refuses fail-closed on a nonzero exit or unparseable property output.
/// Per the architect ruling for this slice, all three are proven with
/// synthetic values (<see cref="BuildAndTestsGate.ParseDotnetTestOutput"/>,
/// <see cref="BuildAndTestsGate.BuildVerdict"/>,
/// <see cref="BuildAndTestsGate.ClassifyProject"/>, and
/// <see cref="BuildAndTestsGate.BuildNonTestVerdict"/>, all pure), while a
/// small second group of tests runs the REAL dotnet against tiny fixture
/// projects under <c>harness/fixtures/g6/</c> to prove the whole gate end to
/// end, including process execution and real classification.
/// </summary>
public sealed class BuildAndTestsGateTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow =
        DateTimeOffset.Parse("2026-08-13T12:00:00-07:00");

    private readonly List<string> _tempDirs = [];

    // Parsing: BuildAndTestsGate.ParseDotnetTestOutput. Pure, synthetic
    // output only, no process ever started.

    [Fact]
    public void ParseDotnetTestOutput_reads_a_passing_summary_line()
    {
        const string stdout = """
            Test run for /fixtures/Fixture.Pass.dll (.NETCoreApp,Version=v10.0)
            A total of 1 test files matched the specified pattern.

            Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1, Duration: 65 ms - Fixture.Pass.dll (net10.0)
            """;

        var run = BuildAndTestsGate.ParseDotnetTestOutput(stdout, standardError: "");

        Assert.True(run.TestsRan);
        Assert.Equal(1, run.Passed);
        Assert.Equal(0, run.Failed);
        Assert.Equal(0, run.Skipped);
        Assert.Equal(1, run.Total);
        Assert.Empty(run.SkippedTestNames);
    }

    [Fact]
    public void ParseDotnetTestOutput_reads_a_failing_summary_line()
    {
        const string stdout = """
            Test run for /fixtures/Fixture.Fail.dll (.NETCoreApp,Version=v10.0)
              Failed Fixture.Fail.FailingTest.This_test_deliberately_fails [49 ms]
              Error Message:
               deliberate failure for G6 fixture coverage

            Failed!  - Failed:     1, Passed:     0, Skipped:     0, Total:     1, Duration: 93 ms - Fixture.Fail.dll (net10.0)
            """;

        var run = BuildAndTestsGate.ParseDotnetTestOutput(stdout, standardError: "");

        Assert.True(run.TestsRan);
        Assert.Equal(0, run.Passed);
        Assert.Equal(1, run.Failed);
        Assert.Equal(1, run.Total);
        Assert.Contains("deliberate failure for G6 fixture coverage", run.Tail);
    }

    [Fact]
    public void ParseDotnetTestOutput_names_every_skipped_test()
    {
        const string stdout = """
            Test run for /fixtures/Probe.dll (.NETCoreApp,Version=v10.0)
              Skipped Probe.ProbeTest.SkippedOne [1 ms]
              Skipped Probe.ProbeTest.SkippedTwo [2 ms]

            Passed!  - Failed:     0, Passed:     1, Skipped:     2, Total:     3, Duration: 63 ms - Probe.dll (net10.0)
            """;

        var run = BuildAndTestsGate.ParseDotnetTestOutput(stdout, standardError: "");

        Assert.Equal(2, run.Skipped);
        Assert.Equal(
            ["Probe.ProbeTest.SkippedOne", "Probe.ProbeTest.SkippedTwo"],
            run.SkippedTestNames);
    }

    [Fact]
    public void ParseDotnetTestOutput_sums_counts_across_multiple_summary_lines()
    {
        // A multi-TFM project prints one VSTest summary line per target
        // framework; the gate judges the project on the total across all of
        // them, not just the first one encountered.
        const string stdout = """
            Passed!  - Failed:     0, Passed:     2, Skipped:     0, Total:     2, Duration: 40 ms - Multi.dll (net8.0)
            Passed!  - Failed:     0, Passed:     2, Skipped:     0, Total:     2, Duration: 45 ms - Multi.dll (net10.0)
            """;

        var run = BuildAndTestsGate.ParseDotnetTestOutput(stdout, standardError: "");

        Assert.True(run.TestsRan);
        Assert.Equal(4, run.Passed);
        Assert.Equal(4, run.Total);
    }

    [Fact]
    public void ParseDotnetTestOutput_no_summary_line_means_tests_did_not_run()
    {
        // Shape observed running the real dotnet against a project with a
        // compile error: MSBuild error lines, exit code 1, no VSTest summary
        // is ever printed.
        const string stdout = """
            BadTest.cs(5,48): error CS1040: Preprocessor directives must appear as the first non-whitespace character on a line [Probe.csproj]
            BadTest.cs(6,1): error CS1002: ; expected [Probe.csproj]
            """;

        var run = BuildAndTestsGate.ParseDotnetTestOutput(stdout, standardError: "");

        Assert.False(run.TestsRan);
        Assert.Equal(0, run.Passed);
        Assert.Equal(0, run.Failed);
        Assert.Equal(0, run.Skipped);
        Assert.Equal(0, run.Total);
        Assert.Contains("error CS1002", run.Tail);
    }

    [Fact]
    public void ParseDotnetTestOutput_tail_keeps_only_the_last_40_lines()
    {
        var lines = Enumerable.Range(1, 100).Select(i => $"line {i}");
        var stdout = string.Join('\n', lines);

        var run = BuildAndTestsGate.ParseDotnetTestOutput(stdout, standardError: "");

        var tailLines = run.Tail.Split(Environment.NewLine);
        Assert.Equal(40, tailLines.Length);
        Assert.Equal("line 61", tailLines.First());
        Assert.Equal("line 100", tailLines.Last());
    }

    [Fact]
    public void ParseDotnetTestOutput_short_output_is_kept_in_full()
    {
        const string stdout = "line 1\nline 2\nline 3";

        var run = BuildAndTestsGate.ParseDotnetTestOutput(stdout, standardError: "");

        Assert.Contains("line 1", run.Tail);
        Assert.Contains("line 3", run.Tail);
    }

    // Verdict mapping: BuildAndTestsGate.BuildVerdict. Pure, synthetic
    // DotnetTestRun values only.

    // NARROWED at slice G6b: BuildVerdict is now reached only for
    // test-classified projects (Evaluate routes non-test projects to
    // BuildNonTestVerdict instead, tested separately below). The rule this
    // test proves is unchanged, just scoped: for a project already known to
    // be a test project, no VSTest summary means the run did not complete.
    [Fact]
    public void BuildVerdict_no_summary_refuses_with_tail_in_reason()
    {
        var run = new DotnetTestRun(
            TestsRan: false, Passed: 0, Failed: 0, Skipped: 0, Total: 0,
            SkippedTestNames: [], Tail: "error CS1002: ; expected");

        var verdict = BuildAndTestsGate.BuildVerdict(Context(), "harness/fixtures/g6/broken/Broken.csproj", run);

        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.Contains("no test-run summary", verdict.Evidence);
        Assert.Contains("error CS1002", verdict.Reason);
    }

    [Fact]
    public void BuildVerdict_failed_tests_refuse_and_reason_carries_the_tail()
    {
        var run = new DotnetTestRun(
            TestsRan: true, Passed: 0, Failed: 1, Skipped: 0, Total: 1,
            SkippedTestNames: [],
            Tail: "Error Message:\n deliberate failure for G6 fixture coverage");

        var verdict = BuildAndTestsGate.BuildVerdict(
            Context(), "harness/fixtures/g6/refuse-failing-test/Fixture.Fail.csproj", run);

        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.Contains("1 failed", verdict.Evidence);
        Assert.Contains("1 test(s) failed", verdict.Reason);
        Assert.Contains("deliberate failure for G6 fixture coverage", verdict.Reason);
    }

    [Fact]
    public void BuildVerdict_nonzero_skip_on_an_otherwise_green_project_refuses_and_names_the_skips()
    {
        var run = new DotnetTestRun(
            TestsRan: true, Passed: 1, Failed: 0, Skipped: 2, Total: 3,
            SkippedTestNames: ["Probe.ProbeTest.SkippedOne", "Probe.ProbeTest.SkippedTwo"],
            Tail: "...");

        var verdict = BuildAndTestsGate.BuildVerdict(Context(), "harness/fixtures/g6/pass/Fixture.Pass.csproj", run);

        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.Contains("Probe.ProbeTest.SkippedOne", verdict.Evidence);
        Assert.Contains("Probe.ProbeTest.SkippedTwo", verdict.Evidence);
        Assert.Contains("tests that skip are not tests that pass", verdict.Reason);
    }

    [Fact]
    public void BuildVerdict_all_green_zero_skips_passes_with_counts_in_evidence_and_a_null_reason()
    {
        var run = new DotnetTestRun(
            TestsRan: true, Passed: 3, Failed: 0, Skipped: 0, Total: 3,
            SkippedTestNames: [], Tail: "...");

        var verdict = BuildAndTestsGate.BuildVerdict(Context(), "harness/fixtures/g6/pass/Fixture.Pass.csproj", run);

        Assert.Equal(GateOutcome.Pass, verdict.Outcome);
        Assert.Contains("3 passed", verdict.Evidence);
        Assert.Contains("0 failed", verdict.Evidence);
        Assert.Contains("0 skipped", verdict.Evidence);
        Assert.Null(verdict.Reason);
    }

    [Fact]
    public void BuildVerdict_stamps_gate_id_name_stage_and_clock_from_context()
    {
        var run = new DotnetTestRun(true, 1, 0, 0, 1, [], "...");

        var verdict = BuildAndTestsGate.BuildVerdict(Context(), "harness/fixtures/g6/pass/Fixture.Pass.csproj", run);

        Assert.Equal("G6", verdict.GateId);
        Assert.Equal("build-and-tests", verdict.GateName);
        Assert.Equal(GateStage.Integration, verdict.Stage);
        Assert.Equal(FixedNow, verdict.At);
    }

    // Classification: BuildAndTestsGate.ClassifyProject. Pure, synthetic
    // ProcessResult values only, no process ever started. Mirrors the
    // frozen ruling for slice G6b: restore, then the property query;
    // nonzero exit or unparseable output fails closed.

    [Fact]
    public void ClassifyProject_property_true_classifies_as_test()
    {
        var restore = new ProcessResult(0, "Restored.", "");
        var property = new ProcessResult(0, "true\r\n", "");

        var result = BuildAndTestsGate.ClassifyProject(restore, property);

        Assert.True(result.Succeeded);
        Assert.Equal(ProjectKind.Test, result.Kind);
    }

    [Fact]
    public void ClassifyProject_property_empty_classifies_as_non_test()
    {
        // Observed shape for a plain library with no Test SDK reference:
        // IsTestProject is never set, so the property query prints nothing
        // but a blank line.
        var restore = new ProcessResult(0, "Restored.", "");
        var property = new ProcessResult(0, "\r\n", "");

        var result = BuildAndTestsGate.ClassifyProject(restore, property);

        Assert.True(result.Succeeded);
        Assert.Equal(ProjectKind.NonTest, result.Kind);
    }

    [Fact]
    public void ClassifyProject_property_explicit_false_classifies_as_non_test()
    {
        var restore = new ProcessResult(0, "Restored.", "");
        var property = new ProcessResult(0, "false\r\n", "");

        var result = BuildAndTestsGate.ClassifyProject(restore, property);

        Assert.True(result.Succeeded);
        Assert.Equal(ProjectKind.NonTest, result.Kind);
    }

    [Fact]
    public void ClassifyProject_is_case_insensitive_and_trims_whitespace()
    {
        var restore = new ProcessResult(0, "Restored.", "");
        var property = new ProcessResult(0, "  TRUE  \r\n", "");

        var result = BuildAndTestsGate.ClassifyProject(restore, property);

        Assert.True(result.Succeeded);
        Assert.Equal(ProjectKind.Test, result.Kind);
    }

    [Fact]
    public void ClassifyProject_restore_failure_refuses_classification_fail_closed()
    {
        var restore = new ProcessResult(1, "", "error NU1101: Unable to find package Foo");
        var property = new ProcessResult(0, "true\r\n", "");

        var result = BuildAndTestsGate.ClassifyProject(restore, property);

        Assert.False(result.Succeeded);
        Assert.Contains("NU1101", result.Tail);
    }

    [Fact]
    public void ClassifyProject_property_query_nonzero_exit_refuses_classification_fail_closed()
    {
        var restore = new ProcessResult(0, "Restored.", "");
        var property = new ProcessResult(1, "", "MSBUILD : error MSB1009: Project file does not exist.");

        var result = BuildAndTestsGate.ClassifyProject(restore, property);

        Assert.False(result.Succeeded);
        Assert.Contains("MSB1009", result.Tail);
    }

    [Fact]
    public void ClassifyProject_unparseable_property_output_refuses_classification_fail_closed()
    {
        var restore = new ProcessResult(0, "Restored.", "");
        var property = new ProcessResult(0, "maybe?\r\n", "");

        var result = BuildAndTestsGate.ClassifyProject(restore, property);

        Assert.False(result.Succeeded);
        Assert.Contains("maybe?", result.Tail);
    }

    // Non-test verdict mapping: BuildAndTestsGate.BuildNonTestVerdict. Pure,
    // synthetic ProcessResult values only.

    [Fact]
    public void BuildNonTestVerdict_clean_build_passes_with_no_test_project_evidence_and_a_null_reason()
    {
        var build = new ProcessResult(0, "Build succeeded.", "");

        var verdict = BuildAndTestsGate.BuildNonTestVerdict(
            Context(), "demo-solution/plugins/DVerse.Plugins/DVerse.Plugins.csproj", build);

        Assert.Equal(GateOutcome.Pass, verdict.Outcome);
        Assert.Contains("built clean", verdict.Evidence);
        Assert.Contains("not a test project", verdict.Evidence);
        Assert.Contains("IsTestProject false", verdict.Evidence);
        Assert.Contains("sibling projects", verdict.Evidence);
        Assert.Null(verdict.Reason);
    }

    [Fact]
    public void BuildNonTestVerdict_nonzero_exit_refuses_with_tail_in_reason()
    {
        var build = new ProcessResult(1, "BadLib.cs(3,1): error CS1002: ; expected", "");

        var verdict = BuildAndTestsGate.BuildNonTestVerdict(
            Context(), "demo-solution/plugins/DVerse.Plugins/DVerse.Plugins.csproj", build);

        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.Contains("dotnet build", verdict.Evidence);
        Assert.Contains("error CS1002", verdict.Reason);
    }

    [Fact]
    public void BuildNonTestVerdict_no_summary_is_never_required_unlike_the_test_path()
    {
        // The defining difference from BuildVerdict: an empty/no-summary
        // output on a clean exit is a Pass here, not a Refuse, because a
        // non-test project produces no VSTest summary by definition and
        // none is expected.
        var build = new ProcessResult(0, "", "");

        var verdict = BuildAndTestsGate.BuildNonTestVerdict(
            Context(), "harness/fixtures/g6/pass-library/Fixture.Library.csproj", build);

        Assert.Equal(GateOutcome.Pass, verdict.Outcome);
    }

    // Real dotnet, real fixtures. These start an actual process and must
    // stay proportionate to that cost, hence exactly three tiny fixtures:
    // one test project with a single passing test, one test project with a
    // single deliberately failing test, and one plain non-test library.
    //
    // Lesson 13 (O11): each real-dotnet test below runs against an ISOLATED
    // COPY of its fixture (IsolatedFixture), never the shared csproj under
    // harness/fixtures/g6/ directly. WaveOneIntegrationTests independently
    // runs BuildAndTestsGate against those same three shared fixture
    // directories (its RedFixtures theory, its g6/pass InlineData case, and
    // its leak-scan sweep over every fixture directory). xUnit's default
    // parallelization unit is the test collection, and a class with no
    // explicit [Collection] attribute is its own collection, so
    // BuildAndTestsGateTests and WaveOneIntegrationTests are two different
    // collections that run concurrently by default; within each class its
    // own tests still run sequentially. That meant two dotnet processes
    // (this class's and WaveOneIntegrationTests's) could invoke
    // restore/build/test against the exact same csproj's obj/bin at the
    // same wall-clock moment, on whatever schedule the two collections
    // happened to interleave on, which is exactly the flake from three
    // sightings, always green in isolation, occasionally red in the full
    // suite.
    //
    // An xUnit [Collection] tag would only serialize test classes that
    // both carry it, and WaveOneIntegrationTests.cs is out of scope for
    // this slice (owned files above), so it cannot be tagged into the same
    // collection here. Giving each test its own private on-disk copy
    // removes the shared obj/bin state directly, is narrower (touches only
    // this file), and needs no cooperation from the other class.

    [Fact]
    public void Evaluate_real_dotnet_against_the_pass_fixture_yields_a_pass()
    {
        var context = ContextFor(IsolatedFixture("pass"));

        var verdicts = new BuildAndTestsGate().Evaluate(context).ToList();

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Pass, verdict.Outcome);
        Assert.Equal("G6", verdict.GateId);
        Assert.Contains("Fixture.Pass.csproj", verdict.Artifact);
        Assert.Contains("1 passed", verdict.Evidence);
        Assert.Contains("0 failed", verdict.Evidence);
        Assert.Contains("0 skipped", verdict.Evidence);
        Assert.Null(verdict.Reason);
    }

    [Fact]
    public void Evaluate_real_dotnet_against_the_failing_fixture_refuses_for_the_stated_reason_only()
    {
        var context = ContextFor(IsolatedFixture("refuse-failing-test"));

        var verdicts = new BuildAndTestsGate().Evaluate(context).ToList();

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
        Assert.Equal("G6", verdict.GateId);
        Assert.Contains("Fixture.Fail.csproj", verdict.Artifact);
        Assert.Contains("1 failed", verdict.Evidence);

        // The stated reason: exactly one real test failed, and the failure
        // is this deliberate one, not a build problem or something else.
        Assert.NotNull(verdict.Reason);
        Assert.Contains("1 test(s) failed", verdict.Reason);
        Assert.Contains("deliberate failure for G6 fixture coverage", verdict.Reason);
        Assert.Contains("This_test_deliberately_fails", verdict.Reason);
    }

    [Fact]
    public void Evaluate_real_dotnet_against_the_pass_library_fixture_yields_a_pass_via_build_not_test()
    {
        // The whole point of slice G6b: a real, non-test class library with
        // no Test SDK reference must Pass via classification-then-build,
        // never Refuse for lacking a VSTest summary it was never going to
        // produce.
        var context = ContextFor(IsolatedFixture("pass-library"));

        var verdicts = new BuildAndTestsGate().Evaluate(context).ToList();

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Pass, verdict.Outcome);
        Assert.Equal("G6", verdict.GateId);
        Assert.Contains("Fixture.Library.csproj", verdict.Artifact);
        Assert.Contains("not a test project", verdict.Evidence);
        Assert.Contains("IsTestProject false", verdict.Evidence);
        Assert.Null(verdict.Reason);
    }

    [Fact]
    public void Evaluate_zero_projects_under_the_solution_root_is_an_honest_pass()
    {
        var empty = NewEmptyDirectory();
        var context = ContextFor(empty);

        var verdicts = new BuildAndTestsGate().Evaluate(context).ToList();

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Pass, verdict.Outcome);
        Assert.Contains("No *.csproj files found", verdict.Evidence);
        Assert.Null(verdict.Reason);
    }

    // Helpers.

    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>
    /// Platform-valid absolute base for context paths in the pure-mapping
    /// tests, which never touch the disk, so the path need not exist.
    /// </summary>
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "dverse-g6-repo");

    private static GateContext Context() => new(
        RepositoryRoot: Root,
        SolutionRoot: Path.Combine(Root, "demo-solution"),
        Stage: GateStage.Integration,
        HasTenantCredentials: false)
    {
        Time = new FakeTime(FixedNow)
    };

    /// <summary>
    /// Real context for the fixture-driven tests: the repository root is the
    /// fixtures directory itself, and the solution root is one fixture
    /// project's own directory, matching how <see cref="YamlLayoutGateTests"/>
    /// and the other gate integration tests point a <see cref="GateContext"/>
    /// at a fixture.
    /// </summary>
    private static GateContext ContextFor(string fixtureRoot) => new(
        RepositoryRoot: FixturesRoot(),
        SolutionRoot: fixtureRoot,
        Stage: GateStage.Integration,
        HasTenantCredentials: false)
    {
        Time = new FakeTime(FixedNow)
    };

    /// <summary>
    /// Resolves harness/fixtures relative to the test assembly's own
    /// location, so the tests run correctly regardless of which machine or
    /// working directory dotnet test is invoked from. Same pattern as
    /// <c>YamlLayoutGateTests.FixturesRoot</c>.
    /// </summary>
    private static string FixturesRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "fixtures", "g6");
            if (Directory.Exists(candidate))
                return Path.Combine(dir.FullName, "fixtures");

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate harness/fixtures/g6 above {AppContext.BaseDirectory}.");
    }

    private static string Fixture(string name) => Path.Combine(FixturesRoot(), "g6", name);

    /// <summary>
    /// Copies the named fixture into a fresh, unique temp directory and
    /// returns that copy's path, registered in <see cref="_tempDirs"/> for
    /// cleanup in <see cref="Dispose"/>. See the O11 comment above the
    /// real-dotnet tests for why: this class and
    /// <c>WaveOneIntegrationTests</c> both run <c>BuildAndTestsGate</c>
    /// against the same on-disk g6 fixtures from two xUnit collections that
    /// run in parallel by default, and two concurrent dotnet invocations
    /// against the same project's obj/bin is the lesson 13 flake. A private
    /// copy per test removes the shared state instead of trying to
    /// serialize two test classes only one of which this slice may edit.
    /// </summary>
    private string IsolatedFixture(string name)
    {
        var source = Fixture(name);
        var isolated = Path.Combine(
            Path.GetTempPath(), "dverse-g6-isolated", Guid.NewGuid().ToString("N"));

        CopyDirectory(source, isolated);
        _tempDirs.Add(isolated);
        return isolated;
    }

    /// <summary>
    /// Recursive copy that skips <c>obj</c> and <c>bin</c>: those are build
    /// output, never part of a fixture's source, and copying a stale local
    /// build into an isolated copy would defeat the isolation it exists to
    /// provide.
    /// </summary>
    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)));

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(subDir);
            if (string.Equals(name, "obj", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "bin", StringComparison.OrdinalIgnoreCase))
                continue;

            CopyDirectory(subDir, Path.Combine(destinationDir, name));
        }
    }

    private string NewEmptyDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dverse-g6-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
