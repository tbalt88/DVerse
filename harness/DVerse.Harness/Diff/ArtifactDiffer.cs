using DVerse.Harness.Diff.Internal;
using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Diff;

/// <summary>
/// The semantic diff engine (wave 7.2): given two versions of a declarative
/// artifact document, walks the ratified family chain for the given
/// artifact class (docs/design/element-identity-model.md section 3, grouped
/// per mission ruling 6 into the family-chain table <see cref="ChainBuilder"/>
/// documents) and produces a complete <see cref="ArtifactDiff"/> -- one
/// <see cref="DiffVerdict"/> per matched, added, or removed element at every
/// nesting level, each carrying property-level detail for Changed verdicts.
/// <para>
/// THE RECURSION RULE IS LAW (model section 5, mission ruling 3): an
/// Added/Removed parent short-circuits its own subtree (one verdict, no
/// recursion below it); a Matched parent ALWAYS recurses into its child
/// family, whether or not that parent's own properties changed, because
/// identical id chains can still hide a single changed leaf property
/// (lesson 14: same formid, same tab/section/cell ids, only
/// <c>datafieldname</c> differs on one control -- a fixture proves exactly
/// this shape).
/// </para>
/// <para>
/// Positional-match warnings (FormXml column/row, the model's honest
/// fallback families) propagate down into every verdict produced under
/// that pairing (mission ruling 5), and a Changed verdict anywhere in that
/// subtree carries the literal phrase "unverified pairing, content-confirmed
/// only" in its description, because the only reason that pairing can be
/// trusted at all is the content match underneath it, never a real identity
/// key.
/// </para>
/// <para>
/// Unsurveyed families encountered mid-walk (for example MissingDependencies
/// carrying real, never-before-seen content) never resolve to silence
/// either: if a step's matcher produces warnings but no matched, added, or
/// removed element at all, the walk synthesizes one explicit, conservative
/// Changed-with-warning verdict instead (mission ruling 6).
/// </para>
/// </summary>
public sealed class ArtifactDiffer
{
    private readonly IReadOnlyDictionary<string, ChainStep> _chains;

    public ArtifactDiffer()
    {
        _chains = ChainBuilder.BuildChains(MatcherRegistry.CreateDefault());
    }

    /// <summary>Every artifact class this differ can walk (the family-chain table's top-level entries), for diagnostics and tests.</summary>
    public IReadOnlyCollection<string> SupportedArtifactClasses => (IReadOnlyCollection<string>)_chains.Keys;

    public ArtifactDiff Diff(YamlNode a, YamlNode b, string artifactClass)
    {
        if (!_chains.TryGetValue(artifactClass, out var top))
        {
            throw new KeyNotFoundException(
                $"No family chain registered for artifact class '{artifactClass}'. Every artifact " +
                "class this differ ships must be wired in ChainBuilder.BuildChains; an unregistered " +
                "lookup is a defect in the walker, not a legitimate 'no opinion' (the same rule " +
                "7.1i's MatcherRegistry.Get already applies to class families).");
        }

        var verdicts = new List<DiffVerdict>();
        var warnings = new List<MatchWarning>();

        Walk(top, a, b, inherited: [], underPositionalAncestor: false, verdicts, warnings);

        var summary = new DiffSummary(
            verdicts.Count(v => v.Kind == DiffVerdictKind.Added),
            verdicts.Count(v => v.Kind == DiffVerdictKind.Removed),
            verdicts.Count(v => v.Kind == DiffVerdictKind.Changed),
            verdicts.Count(v => v.Kind == DiffVerdictKind.Unchanged));

        return new ArtifactDiff(artifactClass, verdicts, warnings, summary);
    }

    private static void Walk(
        ChainStep step,
        YamlNode a,
        YamlNode b,
        IReadOnlyList<MatchWarning> inherited,
        bool underPositionalAncestor,
        List<DiffVerdict> verdicts,
        List<MatchWarning> aggregateWarnings)
    {
        var result = step.Matcher.Match(a, b);
        aggregateWarnings.AddRange(result.Warnings);

        // Added/Removed parents short-circuit: one verdict, no recursion below it (ruling 3).
        foreach (var added in result.Added)
        {
            var identity = new ElementIdentity(step.Family, step.DescribeOrphan(added), IsWarning: step.IsPositional);
            verdicts.Add(new DiffVerdict(
                DiffVerdictKind.Added,
                identity,
                $"{step.Family}[{identity.Key}]: added",
                [],
                inherited));
        }

        foreach (var removed in result.Removed)
        {
            var identity = new ElementIdentity(step.Family, step.DescribeOrphan(removed), IsWarning: step.IsPositional);
            verdicts.Add(new DiffVerdict(
                DiffVerdictKind.Removed,
                identity,
                $"{step.Family}[{identity.Key}]: removed",
                [],
                inherited));
        }

        // A Matched parent ALWAYS recurses (ruling 3), independent of whether its own properties changed.
        for (var i = 0; i < result.Matched.Count; i++)
        {
            var pair = result.Matched[i];

            var pairInherited = inherited;
            if (step.IsPositional)
                pairInherited = [.. inherited, result.Warnings[i]]; // PositionalWarningMatcher: one warning per matched pair, same order, always (verified invariant of that matcher's own source).

            var underPositional = underPositionalAncestor || step.IsPositional;

            var changes = PropertyDiffer.Diff(pair.A, pair.B, step.ExcludeKeys);
            var kind = changes.Count > 0 ? DiffVerdictKind.Changed : DiffVerdictKind.Unchanged;

            var description = changes.Count > 0
                ? $"{step.Family}[key={pair.Identity.Key}]: Changed, {changes.Count} propert{(changes.Count == 1 ? "y" : "ies")} differ"
                : $"{step.Family}[key={pair.Identity.Key}]: {kind}";

            if (kind == DiffVerdictKind.Changed && underPositional)
                description += ": unverified pairing, content-confirmed only";

            verdicts.Add(new DiffVerdict(kind, pair.Identity, description, changes, pairInherited));

            foreach (var child in step.Children)
                Walk(child, pair.A, pair.B, pairInherited, underPositional, verdicts, aggregateWarnings);
        }

        // Ruling 6: an unsurveyed family inside a walk must never resolve to silence. If the matcher
        // produced warnings but no matched/added/removed element at all (MissingDependencies carrying
        // real, never-before-seen content is the shipped example), synthesize one explicit,
        // conservative Changed-with-warning verdict rather than letting the step vanish.
        if (result.Matched.Count == 0 && result.Added.Count == 0 && result.Removed.Count == 0 && result.Warnings.Count > 0)
        {
            verdicts.Add(new DiffVerdict(
                DiffVerdictKind.Changed,
                new ElementIdentity(step.Family, "(unsurveyed)", IsWarning: true),
                $"{step.Family}: unsurveyed content present; conservative Changed-with-warning verdict, " +
                "per mission ruling 6, never silence.",
                [],
                [.. inherited, .. result.Warnings]));
        }
    }
}
