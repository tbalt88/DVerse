using DVerse.Harness;
using DVerse.Harness.Gates;

namespace DVerse.Harness.Tests.Gates;

/// <summary>
/// G6 shells the real <c>dotnet test</c>, so what this gate owns is (1)
/// parsing its console output into pass/fail/skip counts and skipped test
/// names, and (2) mapping that parse to a verdict: build failure or any
/// failed test refuses, a nonzero skip count on an otherwise green project
/// also refuses (tests that skip are not tests that pass), and everything
/// else passes. Per the architect ruling for this slice, the parsing and
/// mapping rules are proven with synthetic output strings
/// (<see cref="BuildAndTestsGate.ParseDotnetTestOutput"/> and
/// <see cref="BuildAndTestsGate.BuildVerdict"/>, both pure), while a small
/// second group of tests runs the REAL dotnet against two tiny fixture
/// projects under <c>harness/fixtures/g6/</c> to prove the whole gate end to
/// end, including process execution.
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

    // Real dotnet, real fixtures. These start an actual process and must
    // stay proportionate to that cost, hence exactly two tiny fixtures:
    // one project with a single passing test, one with a single
    // deliberately failing test.

    [Fact]
    public void Evaluate_real_dotnet_against_the_pass_fixture_yields_a_pass()
    {
        var context = ContextFor(Fixture("pass"));

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
        var context = ContextFor(Fixture("refuse-failing-test"));

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
