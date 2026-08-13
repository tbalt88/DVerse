using DVerse.Harness;
using DVerse.Harness.Gates;

namespace DVerse.Harness.Tests.Gates;

/// <summary>
/// G7 composes Microsoft's Power Apps Checker rather than reinventing static
/// analysis, so what this gate owns is (1) parsing the checker's SARIF result
/// into findings with a resolved severity, and (2) mapping those findings to
/// verdicts: Critical/High refuse one-per-finding, Medium and below are only
/// counted. Per the architect ruling for this slice, no test here may invoke
/// pac or the network; <see cref="PowerAppsCheckerGate.ParseSarif"/> and
/// <see cref="PowerAppsCheckerGate.BuildVerdicts"/> are both pure and are
/// exercised directly with synthetic SARIF/findings instead.
/// </summary>
public sealed class PowerAppsCheckerGateTests
{
    private static readonly DateTimeOffset FixedNow =
        DateTimeOffset.Parse("2026-08-13T12:00:00-07:00");

    // Parsing: PowerAppsCheckerGate.ParseSarif.

    [Fact]
    public void ParseSarif_extracts_ruleid_severity_message_and_file_reference()
    {
        const string sarif = """
            {
              "version": "2.1.0",
              "runs": [
                {
                  "tool": {
                    "driver": {
                      "rules": [
                        {
                          "id": "web-avoid-eval",
                          "properties": { "severity": "Critical" }
                        }
                      ]
                    }
                  },
                  "results": [
                    {
                      "ruleId": "web-avoid-eval",
                      "message": { "text": "Avoid using eval()." },
                      "locations": [
                        { "physicalLocation": { "artifactLocation": { "uri": "webresources/foo.js" } } }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        var findings = PowerAppsCheckerGate.ParseSarif(sarif);

        var finding = Assert.Single(findings);
        Assert.Equal("web-avoid-eval", finding.RuleId);
        Assert.Equal("Critical", finding.Severity);
        Assert.Equal("Avoid using eval().", finding.Message);
        Assert.Equal("webresources/foo.js", finding.FileReference);
    }

    [Fact]
    public void ParseSarif_returns_empty_list_when_results_array_is_empty()
    {
        // The exact shape observed running the real checker against
        // demo-solution during this slice: a populated rules catalogue with
        // zero results is a genuine "nothing was found", not a parse failure.
        const string sarif = """
            {
              "version": "2.1.0",
              "runs": [
                {
                  "tool": { "driver": { "rules": [] } },
                  "results": []
                }
              ]
            }
            """;

        var findings = PowerAppsCheckerGate.ParseSarif(sarif);

        Assert.Empty(findings);
    }

    [Fact]
    public void ParseSarif_result_with_no_locations_has_a_null_file_reference()
    {
        const string sarif = """
            {
              "version": "2.1.0",
              "runs": [
                {
                  "tool": {
                    "driver": {
                      "rules": [
                        { "id": "meta-remove-inactive", "properties": { "severity": "Low" } }
                      ]
                    }
                  },
                  "results": [
                    { "ruleId": "meta-remove-inactive", "message": { "text": "Remove the inactive workflow." } }
                  ]
                }
              ]
            }
            """;

        var finding = Assert.Single(PowerAppsCheckerGate.ParseSarif(sarif));

        Assert.Equal("Low", finding.Severity);
        Assert.Null(finding.FileReference);
    }

    [Fact]
    public void ParseSarif_parses_multiple_findings_across_severities_in_order()
    {
        const string sarif = """
            {
              "version": "2.1.0",
              "runs": [
                {
                  "tool": {
                    "driver": {
                      "rules": [
                        { "id": "web-avoid-eval", "properties": { "severity": "Critical" } },
                        { "id": "web-avoid-2011-api", "properties": { "severity": "High" } },
                        { "id": "web-use-async", "properties": { "severity": "Medium" } }
                      ]
                    }
                  },
                  "results": [
                    { "ruleId": "web-avoid-eval", "message": { "text": "one" } },
                    { "ruleId": "web-avoid-2011-api", "message": { "text": "two" } },
                    { "ruleId": "web-use-async", "message": { "text": "three" } }
                  ]
                }
              ]
            }
            """;

        var findings = PowerAppsCheckerGate.ParseSarif(sarif);

        Assert.Equal(3, findings.Count);
        Assert.Equal(["Critical", "High", "Medium"], findings.Select(f => f.Severity).ToArray());
    }

    [Fact]
    public void ParseSarif_throws_on_malformed_json_rather_than_returning_no_findings()
    {
        var ex = Assert.Throws<InvalidDataException>(() => PowerAppsCheckerGate.ParseSarif("{ this is not json"));
        Assert.Contains("not valid JSON", ex.Message);
    }

    [Fact]
    public void ParseSarif_throws_when_the_runs_array_is_absent()
    {
        var ex = Assert.Throws<InvalidDataException>(
            () => PowerAppsCheckerGate.ParseSarif("""{ "version": "2.1.0" }"""));

        Assert.Contains("runs", ex.Message);
    }

    [Fact]
    public void ParseSarif_throws_when_a_result_names_a_rule_absent_from_the_rules_table()
    {
        // A result whose ruleId cannot be resolved to a severity cannot be
        // classified Critical/High/Medium/Low/Informational. Per the
        // fail-closed rule this must throw, not silently drop the finding or
        // guess a severity.
        const string sarif = """
            {
              "version": "2.1.0",
              "runs": [
                {
                  "tool": { "driver": { "rules": [] } },
                  "results": [
                    { "ruleId": "web-avoid-eval", "message": { "text": "orphaned finding" } }
                  ]
                }
              ]
            }
            """;

        var ex = Assert.Throws<InvalidDataException>(() => PowerAppsCheckerGate.ParseSarif(sarif));
        Assert.Contains("web-avoid-eval", ex.Message);
    }

    [Fact]
    public void ParseSarif_throws_on_a_severity_value_outside_the_five_known_levels()
    {
        const string sarif = """
            {
              "version": "2.1.0",
              "runs": [
                {
                  "tool": {
                    "driver": {
                      "rules": [
                        { "id": "some-rule", "properties": { "severity": "Extreme" } }
                      ]
                    }
                  },
                  "results": [
                    { "ruleId": "some-rule", "message": { "text": "x" } }
                  ]
                }
              ]
            }
            """;

        var ex = Assert.Throws<InvalidDataException>(() => PowerAppsCheckerGate.ParseSarif(sarif));
        Assert.Contains("Extreme", ex.Message);
    }

    // Severity mapping: PowerAppsCheckerGate.BuildVerdicts.

    [Fact]
    public void BuildVerdicts_refuses_once_per_critical_or_high_finding_with_file_reference_in_reason()
    {
        var context = Context(@"C:\repo", @"C:\repo\demo-solution");
        var findings = new[]
        {
            new CheckerFinding("web-avoid-eval", "Critical", "Avoid using eval().", "webresources/foo.js"),
            new CheckerFinding("web-avoid-2011-api", "High", "Avoid the 2011 API.", "webresources/bar.js")
        };

        var verdicts = PowerAppsCheckerGate.BuildVerdicts(context, findings);

        Assert.Equal(2, verdicts.Count);
        Assert.All(verdicts, v => Assert.Equal(GateOutcome.Refuse, v.Outcome));
        Assert.All(verdicts, v => Assert.Equal("demo-solution", v.Artifact));
        Assert.All(verdicts, v => Assert.False(string.IsNullOrWhiteSpace(v.Evidence)));

        Assert.Contains(verdicts, v =>
            v.Reason!.Contains("Avoid using eval().") && v.Reason.Contains("webresources/foo.js"));
        Assert.Contains(verdicts, v =>
            v.Reason!.Contains("Avoid the 2011 API.") && v.Reason.Contains("webresources/bar.js"));
    }

    [Fact]
    public void BuildVerdicts_counts_medium_and_below_in_pass_evidence_without_refusing()
    {
        var context = Context(@"C:\repo", @"C:\repo\demo-solution");
        var findings = new[]
        {
            new CheckerFinding("web-use-async", "Medium", "prefer async", null),
            new CheckerFinding("meta-remove-inactive", "Low", "remove inactive workflow", null),
            new CheckerFinding("meta-license-fieldservice-sdkmessages", "Informational", "fyi", null)
        };

        var verdicts = PowerAppsCheckerGate.BuildVerdicts(context, findings);

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Pass, verdict.Outcome);
        Assert.Null(verdict.Reason);
        Assert.Contains("1 Medium", verdict.Evidence);
        Assert.Contains("1 Low", verdict.Evidence);
        Assert.Contains("1 Informational", verdict.Evidence);
        Assert.Contains("0 Critical", verdict.Evidence);
        Assert.Contains("0 High", verdict.Evidence);
        Assert.Contains("out of 3 total", verdict.Evidence);
    }

    [Fact]
    public void BuildVerdicts_zero_findings_passes_with_zero_counts_naming_the_ruleset()
    {
        var context = Context(@"C:\repo", @"C:\repo\demo-solution");

        var verdicts = PowerAppsCheckerGate.BuildVerdicts(context, []);

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Pass, verdict.Outcome);
        Assert.Contains("Solution Checker", verdict.Evidence);
        Assert.Contains("0 Critical, 0 High, 0 Medium, 0 Low, 0 Informational", verdict.Evidence);
        Assert.Contains("out of 0 total", verdict.Evidence);
    }

    [Fact]
    public void BuildVerdicts_a_single_critical_finding_among_low_findings_still_only_yields_refusals()
    {
        // Per the ruling, any Critical/High finding produces a Refuse
        // verdict per finding; the lower-severity findings that happen to
        // ride along do not also produce a separate Pass verdict.
        var context = Context(@"C:\repo", @"C:\repo\demo-solution");
        var findings = new[]
        {
            new CheckerFinding("web-avoid-eval", "Critical", "Avoid using eval().", "webresources/foo.js"),
            new CheckerFinding("meta-remove-inactive", "Low", "remove inactive workflow", null)
        };

        var verdicts = PowerAppsCheckerGate.BuildVerdicts(context, findings);

        var verdict = Assert.Single(verdicts);
        Assert.Equal(GateOutcome.Refuse, verdict.Outcome);
    }

    [Fact]
    public void BuildVerdicts_artifact_is_the_repository_relative_solution_root_not_an_absolute_path()
    {
        var context = Context(@"C:\repo", @"C:\repo\nested\demo-solution");
        var findings = new[]
        {
            new CheckerFinding("web-avoid-eval", "Critical", "Avoid using eval().", "webresources/foo.js")
        };

        var verdicts = PowerAppsCheckerGate.BuildVerdicts(context, findings);

        var verdict = Assert.Single(verdicts);
        Assert.Equal("nested/demo-solution", verdict.Artifact);
        Assert.False(Path.IsPathRooted(verdict.Artifact));
        Assert.DoesNotContain(@"C:\", verdict.Artifact);
    }

    [Fact]
    public void Every_verdict_stamps_gate_id_and_stage_from_context()
    {
        var context = Context(@"C:\repo", @"C:\repo\demo-solution");

        var passVerdict = Assert.Single(PowerAppsCheckerGate.BuildVerdicts(context, []));
        Assert.Equal("G7", passVerdict.GateId);
        Assert.Equal("power-apps-checker", passVerdict.GateName);
        Assert.Equal(GateStage.Integration, passVerdict.Stage);
        Assert.Equal(FixedNow, passVerdict.At);
    }

    // Helpers.

    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static GateContext Context(string repositoryRoot, string solutionRoot) => new(
        RepositoryRoot: repositoryRoot,
        SolutionRoot: solutionRoot,
        Stage: GateStage.Integration,
        HasTenantCredentials: true)
    {
        Time = new FakeTime(FixedNow)
    };
}
