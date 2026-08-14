using DVerse.Harness.Diff;

namespace DVerse.Harness.Tests.Diff;

/// <summary>
/// MissingDependencies (ratified model section 4 item 3): the one class
/// family with zero real instances anywhere in the surveyed corpus. Every
/// committed missingdependencies.yml is <c>MissingDependencies: {}</c>
/// (demo-solution/solutions/DVerseCore/missingdependencies.yml); these
/// tests use that real empty shape plus a synthetic non-empty shape (which
/// has never actually been observed) to prove the matcher stays honestly
/// silent on the real shape and warns rather than guesses the moment
/// content it has never seen appears.
/// </summary>
public sealed class UnsurveyedFamilyMatcherTests
{
    private static readonly MatcherRegistry Registry = MatcherRegistry.CreateDefault();

    [Fact]
    public void Real_empty_shape_on_both_sides_produces_no_verdict_and_no_warning()
    {
        var a = DiffFixtures.ParseRoot("MissingDependencies: {}\n");
        var b = DiffFixtures.ParseRoot("MissingDependencies: {}\n");

        var result = Registry.Get(DiffClassFamilies.MissingDependencies).Match(a, b);

        // MatchResult's list-valued members use reference equality (List<T> does not
        // override Equals), so an empty-shape check compares emptiness per member
        // rather than record equality against MatchResult.Empty.
        Assert.Empty(result.Matched);
        Assert.Empty(result.Added);
        Assert.Empty(result.Removed);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Never_before_seen_non_empty_content_warns_rather_than_guessing_a_verdict()
    {
        var a = DiffFixtures.ParseRoot("MissingDependencies: {}\n");
        var b = DiffFixtures.ParseRoot("MissingDependencies:\n  MissingDependency:\n    - '@type': '1'\n");

        var result = Registry.Get(DiffClassFamilies.MissingDependencies).Match(a, b);

        Assert.Empty(result.Matched);
        Assert.Empty(result.Added);
        Assert.Empty(result.Removed);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("is unsurveyed", warning.Reason);
        Assert.Contains("no real instance existed", warning.Reason);
    }
}
