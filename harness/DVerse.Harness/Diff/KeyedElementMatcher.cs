using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Diff;

/// <summary>
/// Generic matcher for the shape the ratified identity model uses
/// repeatedly (docs/design/element-identity-model.md section 3): a
/// container node holds zero or more elements, each carrying a key
/// extractable by a fixed rule (a GUID attribute, a schema name, a logical
/// name, a YAML mapping key, ...). Matching is then just: same key on both
/// sides is a match; key only in B is added; key only in A is removed.
/// This one class implements every "exact string equality" and "case
/// insensitive string equality" row of that table; only
/// <paramref name="enumerate"/> (how to locate and key this family's
/// elements from the container) and the comparer change per class family,
/// which is why <see cref="MatcherRegistry"/> can build most of its 27
/// entries from one constructor call each instead of 27 near-duplicate
/// classes.
/// <para>
/// <paramref name="warnOnMatch"/> exists for the one extra rule the model
/// layers on top of a plain key match: FormXml control identity is exact
/// on <c>@id</c>, but a control whose <c>@classid</c> is not one of the
/// three this model has ever surveyed (textbox, datetime, lookup) must
/// still warn, per mission ruling 3 ("unknown control classes ... produce
/// warnings, never silent matches"). Every other family passes null here.
/// </para>
/// </summary>
public sealed class KeyedElementMatcher(
    string classFamily,
    Func<YamlNode, IEnumerable<(string? Key, YamlNode Element)>> enumerate,
    StringComparer? comparer = null,
    Func<YamlNode, YamlNode, string?>? warnOnMatch = null,
    Func<YamlNode, string>? describe = null) : IElementMatcher
{
    private readonly StringComparer _comparer = comparer ?? StringComparer.Ordinal;

    public string ClassFamily { get; } = classFamily;

    public MatchResult Match(YamlNode a, YamlNode b)
    {
        var warnings = new List<MatchWarning>();
        var matched = new List<MatchedPair>();
        var added = new List<YamlNode>();
        var removed = new List<YamlNode>();

        var aByKey = new Dictionary<string, YamlNode>(_comparer);

        foreach (var (key, element) in enumerate(a))
        {
            if (key is null)
            {
                warnings.Add(new MatchWarning(
                    Describe(element),
                    $"{ClassFamily}: baseline element carries no extractable key; it cannot be " +
                    "matched or safely reported as removed."));
                continue;
            }

            if (aByKey.ContainsKey(key))
            {
                warnings.Add(new MatchWarning(
                    Describe(element),
                    $"{ClassFamily}: duplicate key '{key}' on the baseline side; only the first " +
                    "instance is available to match against."));
                continue;
            }

            aByKey[key] = element;
        }

        var consumed = new HashSet<string>(_comparer);

        foreach (var (key, element) in enumerate(b))
        {
            if (key is null)
            {
                warnings.Add(new MatchWarning(
                    Describe(element),
                    $"{ClassFamily}: target element carries no extractable key; it cannot be " +
                    "matched or safely reported as added."));
                continue;
            }

            if (consumed.Contains(key))
            {
                warnings.Add(new MatchWarning(
                    Describe(element),
                    $"{ClassFamily}: duplicate key '{key}' on the target side; treated as added " +
                    "since its baseline counterpart is already claimed."));
                added.Add(element);
                continue;
            }

            if (aByKey.TryGetValue(key, out var aElement))
            {
                consumed.Add(key);

                var extra = warnOnMatch?.Invoke(aElement, element);
                matched.Add(new MatchedPair(aElement, element, new ElementIdentity(ClassFamily, key, IsWarning: extra is not null)));

                if (extra is not null)
                    warnings.Add(new MatchWarning(Describe(element), extra));
            }
            else
            {
                added.Add(element);
            }
        }

        foreach (var (key, element) in aByKey)
        {
            if (!consumed.Contains(key))
                removed.Add(element);
        }

        return new MatchResult(matched, added, removed, warnings);
    }

    private string Describe(YamlNode node) =>
        describe?.Invoke(node) ?? ClassFamily;
}
