using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Diff;

/// <summary>
/// FormXml column and row (ratified model rows 10 and 12) carry no identity
/// attribute of any kind in the real committed shape, only <c>@width</c> on
/// column and nothing at all on row. Per mission ruling 3 ("FormXml
/// columns/rows positional WITH a warning on every such match"), this
/// matcher pairs same-index elements from A and B and emits one warning per
/// pair, every time, not only when a reorder is suspected: there is no way
/// to tell a genuine reorder apart from a coincidental same-position match
/// without the warning, which is exactly the false-match risk the model's
/// own honest-fallback section names (docs/design/element-identity-model.md
/// section 4). Elements beyond the shorter side's length are reported as
/// plain removed/added with no warning, since a pure length difference at
/// the tail is not a positional ambiguity.
/// <para>
/// This is deliberately the simpler of two options the model considered
/// (section 3 row 12): a proxy key derived from a row's own single cell id.
/// That proxy is not implemented here because mission ruling 3 states the
/// flat positional-with-warning rule as the frozen contract for this slice;
/// the cell-id-proxy refinement remains an open option for a future slice,
/// not a regression.
/// </para>
/// </summary>
public sealed class PositionalWarningMatcher(
    string classFamily,
    Func<YamlNode, IEnumerable<YamlNode>> enumerate,
    Func<YamlNode, int, string> describe) : IElementMatcher
{
    public string ClassFamily { get; } = classFamily;

    public MatchResult Match(YamlNode a, YamlNode b)
    {
        var aElements = enumerate(a).ToList();
        var bElements = enumerate(b).ToList();

        var matched = new List<MatchedPair>();
        var warnings = new List<MatchWarning>();
        var added = new List<YamlNode>();
        var removed = new List<YamlNode>();

        var commonCount = Math.Min(aElements.Count, bElements.Count);

        for (var i = 0; i < commonCount; i++)
        {
            var identity = new ElementIdentity(ClassFamily, $"#{i}", IsWarning: true);
            matched.Add(new MatchedPair(aElements[i], bElements[i], identity));
            warnings.Add(new MatchWarning(
                describe(bElements[i], i),
                $"{ClassFamily}: matched by ordinal position {i} only; no identity attribute " +
                "exists in the real committed shape (docs/design/element-identity-model.md " +
                "section 4, honest fallback). A reordered sibling reads as a false match at this " +
                "position; treat this pairing as unverified until property-level content confirms " +
                "it, per mission ruling 3."));
        }

        for (var i = commonCount; i < aElements.Count; i++)
            removed.Add(aElements[i]);

        for (var i = commonCount; i < bElements.Count; i++)
            added.Add(bElements[i]);

        return new MatchResult(matched, added, removed, warnings);
    }
}
