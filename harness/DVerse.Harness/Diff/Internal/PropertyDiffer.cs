using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Diff.Internal;

/// <summary>
/// Generic scalar property comparison for one matched element pair (wave
/// 7.2 mission ruling 4): flattens both sides into path-to-scalar-value
/// maps and reports every path whose value differs. Shared by every
/// <c>ChainStep</c> in <see cref="ChainBuilder"/> so the comparison rule
/// itself -- string equality, order-insensitive, missing-vs-empty preserved
/// -- is written once rather than per family.
/// <para>
/// <paramref name="excludeTopLevelKeys"/> removes exactly the DIRECT child
/// keys of the element being compared that belong to a child family the
/// walker recurses into separately (a FormXml tab's "columns", a row's
/// "cell", AppModule's "AppModuleComponents", ...); those subtrees get
/// their own verdicts at their own nesting level and must not also be
/// flattened into this element's own property list, or every ancestor of a
/// changed leaf would spuriously show as "changed" too. The exclusion only
/// applies at the top level of the node being compared, not to every
/// occurrence of that key name at any depth, since the model's own family
/// boundaries are always exactly one level below the element being diffed.
/// </para>
/// </summary>
internal static class PropertyDiffer
{
    public static IReadOnlyList<PropertyChange> Diff(YamlNode a, YamlNode b, IReadOnlySet<string> excludeTopLevelKeys)
    {
        var flatA = new Dictionary<string, string?>(StringComparer.Ordinal);
        var flatB = new Dictionary<string, string?>(StringComparer.Ordinal);

        FlattenInto(a, "", excludeTopLevelKeys, isTop: true, flatA);
        FlattenInto(b, "", excludeTopLevelKeys, isTop: true, flatB);

        var allPaths = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var path in flatA.Keys) allPaths.Add(path);
        foreach (var path in flatB.Keys) allPaths.Add(path);

        var changes = new List<PropertyChange>();

        foreach (var path in allPaths)
        {
            // Missing-vs-empty (ruling 4): TryGetValue returning false means the path was never
            // written at all (the key is absent), reported as null, distinct from a present key
            // whose scalar value happens to be the empty string.
            var valueA = flatA.TryGetValue(path, out var va) ? va : null;
            var valueB = flatB.TryGetValue(path, out var vb) ? vb : null;

            if (!string.Equals(valueA, valueB, StringComparison.Ordinal))
                changes.Add(new PropertyChange(path, valueA, valueB));
        }

        return changes;
    }

    private static void FlattenInto(
        YamlNode? node,
        string prefix,
        IReadOnlySet<string> excludeTopLevelKeys,
        bool isTop,
        Dictionary<string, string?> into)
    {
        switch (node)
        {
            case null:
                return;

            case YamlScalarNode scalar:
                // The "=" prefix on a canvas formula (and every other scalar's exact text) is
                // preserved verbatim: no trimming, no prefix-stripping, per ruling 4.
                into[prefix.Length == 0 ? "(value)" : prefix] = scalar.Value ?? "";
                return;

            case YamlMappingNode mapping:
                // Ruling 4: attribute order is not significant. Values are stored by path in a
                // dictionary, never compared positionally, so re-ordering a mapping's keys between
                // A and B can never itself produce a spurious property change.
                foreach (var child in mapping.Children)
                {
                    var key = (child.Key as YamlScalarNode)?.Value ?? "?";
                    if (isTop && excludeTopLevelKeys.Contains(key))
                        continue;

                    var childPrefix = prefix.Length == 0 ? key : $"{prefix}.{key}";
                    FlattenInto(child.Value, childPrefix, excludeTopLevelKeys, isTop: false, into);
                }
                return;

            case YamlSequenceNode sequence:
                for (var i = 0; i < sequence.Children.Count; i++)
                    FlattenInto(sequence.Children[i], $"{prefix}[{i}]", excludeTopLevelKeys, isTop: false, into);
                return;
        }
    }
}
