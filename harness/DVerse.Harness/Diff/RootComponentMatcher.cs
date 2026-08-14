using DVerse.Harness.Diff.Internal;
using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Diff;

/// <summary>
/// RootComponents entry (ratified model row 3, seat ruling 8). Key rule:
/// <c>@id</c> when present, paired with <c>@type</c> so two different
/// component types can never collide over the same GUID text; else
/// <c>(@type, @schemaName)</c>, with <c>@schemaName</c> compared case
/// insensitively since it inherits the underlying Entity/AppModule schema
/// name's own platform immutability (docs/design/element-identity-model.md
/// row 19's reasoning, carried over here). An entry with neither attribute
/// cannot be matched at all and is warned rather than silently dropped.
/// <para>
/// Ruling 8: a component <c>@type</c> outside the five this project has
/// actually surveyed (1 Entity, 62 SiteMap, 80 AppModule, 91
/// PluginAssembly, 92 SdkMessageProcessingStep) still gets matched by the
/// same dual rule -- ruling 8 does not forbid matching an unsurveyed type,
/// it forbids TRUSTING that match silently. Every element of an unsurveyed
/// type therefore carries an explicit warning alongside whatever verdict
/// (matched, added, or removed) it received, per lessons 15 and 16's
/// precedent that the platform's own export can violate assumptions
/// reasoned from the ComponentType enum alone.
/// </para>
/// <para>
/// Expects <paramref name="a"/>/<paramref name="b"/> to be the parsed root
/// of a <c>rootcomponents.yml</c> file (the node whose child is
/// <c>RootComponents</c>).
/// </para>
/// </summary>
public sealed class RootComponentMatcher : IElementMatcher
{
    public const string FamilyName = "RootComponentsEntry";

    /// <summary>
    /// The five ComponentType values this project has actually observed and
    /// platform-mirror confirmed (docs/design/element-identity-model.md
    /// section 3 rows 3/23/24/25 and demo-solution/solutions/DVerseCore/
    /// rootcomponents.yml). Any other value is unsurveyed per ruling 8.
    /// </summary>
    public static readonly IReadOnlyCollection<string> SurveyedTypes = ["1", "62", "80", "91", "92"];

    public string ClassFamily => FamilyName;

    public MatchResult Match(YamlNode a, YamlNode b)
    {
        var aEntries = Entries(a).ToList();
        var bEntries = Entries(b).ToList();

        var warnings = new List<MatchWarning>();
        var matched = new List<MatchedPair>();
        var added = new List<YamlNode>();
        var removed = new List<YamlNode>();

        var aByKey = new Dictionary<string, YamlNode>(StringComparer.Ordinal);

        foreach (var entry in aEntries)
        {
            var key = ExtractKey(entry, out _);
            if (key is null)
            {
                warnings.Add(NoIdentifierWarning(entry, "baseline", "removed"));
                continue;
            }

            aByKey.TryAdd(key, entry);
        }

        var consumed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in bEntries)
        {
            var key = ExtractKey(entry, out var type);
            if (key is null)
            {
                warnings.Add(NoIdentifierWarning(entry, "target", "added"));
                continue;
            }

            var unsurveyedWarning = type is not null && !SurveyedTypes.Contains(type)
                ? $"{FamilyName}: component type '{type}' is unsurveyed (docs/design/" +
                  "element-identity-model.md seat ruling 8); matched by the default dual rule " +
                  "(id when present, else type+schemaName) but not yet platform-mirror confirmed " +
                  "for this type. Do not trust this verdict without confirmation."
                : null;

            if (aByKey.TryGetValue(key, out var aEntry) && !consumed.Contains(key))
            {
                consumed.Add(key);
                matched.Add(new MatchedPair(
                    aEntry, entry, new ElementIdentity(FamilyName, key, unsurveyedWarning is not null)));
            }
            else
            {
                added.Add(entry);
            }

            if (unsurveyedWarning is not null)
                warnings.Add(new MatchWarning(Describe(entry), unsurveyedWarning));
        }

        foreach (var (key, entry) in aByKey)
        {
            if (!consumed.Contains(key))
                removed.Add(entry);
        }

        return new MatchResult(matched, added, removed, warnings);
    }

    private static IEnumerable<YamlNode> Entries(YamlNode root) =>
        YamlNodeHelpers.OneOrMany(YamlNodeHelpers.Descend(root, "RootComponents", "RootComponent"));

    /// <summary>
    /// Dual rule: id (paired with type) when present, else type+schemaName
    /// (schemaName compared case insensitively via the lowercase form baked
    /// into the key itself, since <see cref="RootComponentMatcher.Match"/>
    /// uses an ordinal dictionary).
    /// </summary>
    private static string? ExtractKey(YamlNode entry, out string? type)
    {
        type = YamlNodeHelpers.ScalarChild(entry, "@type");
        var id = YamlNodeHelpers.ScalarChild(entry, "@id");
        var schemaName = YamlNodeHelpers.ScalarChild(entry, "@schemaName");

        if (!string.IsNullOrWhiteSpace(id))
            return $"{type ?? "?"}|id:{id}";

        if (!string.IsNullOrWhiteSpace(schemaName))
            return $"{type ?? "?"}|name:{schemaName.ToLowerInvariant()}";

        return null;
    }

    private static MatchWarning NoIdentifierWarning(YamlNode entry, string side, string verdict) =>
        new(Describe(entry),
            $"{FamilyName}: {side} entry carries neither '@id' nor '@schemaName'; it cannot be " +
            $"matched and is reported as {verdict} only because every entry must land somewhere, " +
            "not because its identity is known.");

    private static string Describe(YamlNode entry)
    {
        var type = YamlNodeHelpers.ScalarChild(entry, "@type") ?? "?";
        var id = YamlNodeHelpers.ScalarChild(entry, "@id");
        var schemaName = YamlNodeHelpers.ScalarChild(entry, "@schemaName");
        var identifier = id ?? schemaName ?? "(no identifier)";
        return $"{FamilyName}[type={type}, {identifier}]";
    }
}
