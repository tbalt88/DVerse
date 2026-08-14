using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Diff.Internal;

/// <summary>
/// Small YAML representation-model tree-walking helpers shared by every
/// matcher built in <see cref="MatcherRegistry"/>. Centralized here rather
/// than duplicated per matcher (unlike the Gates namespace, where each gate
/// repeats its own TryGetMappingValue in isolation because each gate owns
/// exactly one artifact shape) because this namespace configures roughly
/// two dozen matchers from the same handful of primitives against every
/// class family in the ratified model's table; duplicating that many times
/// would be the real readability cost here, not sharing it once.
/// </summary>
internal static class YamlNodeHelpers
{
    /// <summary>Finds a mapping child by scalar key, the low-level lookup every helper below builds on.</summary>
    public static bool TryGetMappingValue(YamlMappingNode mapping, string key, out YamlNode? value)
    {
        foreach (var child in mapping.Children)
        {
            if (child.Key is YamlScalarNode scalarKey && scalarKey.Value == key)
            {
                value = child.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <summary>Child node at <paramref name="key"/>, or null if <paramref name="node"/> is absent, not a mapping, or lacks the key.</summary>
    public static YamlNode? Child(YamlNode? node, string key) =>
        node is YamlMappingNode mapping && TryGetMappingValue(mapping, key, out var value) ? value : null;

    /// <summary>Walks a chain of mapping keys, short-circuiting to null at the first miss.</summary>
    public static YamlNode? Descend(YamlNode? node, params string[] keys)
    {
        var current = node;
        foreach (var key in keys)
            current = Child(current, key);

        return current;
    }

    /// <summary>Scalar text of a node, or null if it is absent or not a scalar.</summary>
    public static string? Scalar(YamlNode? node) => (node as YamlScalarNode)?.Value;

    /// <summary>Scalar text of a mapping's child at <paramref name="key"/>.</summary>
    public static string? ScalarChild(YamlNode? node, string key) => Scalar(Child(node, key));

    /// <summary>
    /// A YAML collection node may hold either a single mapping (cardinality
    /// one) or a sequence of mappings (cardinality many); the shared
    /// pac-verified YAML-to-XML round trip collapses a one-element
    /// collection to a bare mapping, the same convention G8 and G9 already
    /// document for rootcomponents.yml and solutioncomponents.yml. This
    /// normalizes both shapes to a uniform sequence so every matcher can
    /// enumerate without caring which shape it was handed.
    /// </summary>
    public static IEnumerable<YamlNode> OneOrMany(YamlNode? node)
    {
        switch (node)
        {
            case null:
                yield break;
            case YamlSequenceNode sequence:
                foreach (var item in sequence.Children)
                    yield return item;
                break;
            default:
                yield return node;
                break;
        }
    }
}
