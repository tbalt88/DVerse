using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Gates;

/// <summary>
/// G3: every <c>solutions/*/missingdependencies.yml</c> must declare zero
/// missing dependencies.
/// <para>
/// This exists because a declared missing dependency is not a warning, it is
/// a guaranteed future failure: the platform records exactly which components
/// this solution needs but does not ship, and import on any environment that
/// lacks them fails at that moment, not before. Nothing upstream of import
/// catches this; pack and publish both succeed over a solution that already
/// knows it cannot be imported cleanly everywhere. This gate turns that known,
/// silent time bomb into a refusal before the pack ever runs.
/// </para>
/// <para>
/// WHY this shape: the shape of a non-empty missingdependencies.yml had never
/// been observed (only <c>MissingDependencies: {}</c> exists in demo-solution).
/// Decompiling pac 2.10.1's SolutionPackagerLib.dll (ilspycmd, same tool and
/// version used for G9 in slice 4.1) resolved it. Two call sites matter:
/// </para>
/// <para>
/// <c>DiskReader.GetMissingDependencies()</c> reads
/// <c>solutions/{name}/missingdependencies.yml</c> and feeds its text through
/// <c>Helper.ConvertYamlToXml</c>, which deserializes the YAML into an
/// <c>ExpandoObject</c> (YamlDotNet), re-serializes that to JSON
/// (Newtonsoft.Json), then converts the JSON to XML with
/// <c>JsonConvert.DeserializeXNode</c>. That last step is the load-bearing
/// fact: it is the standard Json.NET convention where a JSON property named
/// <c>@foo</c> becomes an XML attribute <c>foo</c>, the exact convention G9
/// already found governing <c>solutioncomponents.yml</c>'s <c>@path</c> keys.
/// The resulting <c>&lt;MissingDependencies&gt;</c> element is spliced into the
/// solution's XML and handed to <c>Helper.LoadSolutionInformation</c>, which
/// walks direct children named <c>MissingDependency</c>, and within each one,
/// descendants named <c>Required</c> (the dependency that is missing; read via
/// <c>ExtractRequredDependancy</c>, attributes <c>type</c>, <c>schemaName</c>,
/// <c>displayName</c>, <c>parentSchemaName</c>, <c>parentDisplayName</c>,
/// <c>solution</c>) and <c>Dependent</c> (the component in this solution that
/// needs it; read via <c>ExtractDependantInfo</c>, the same attribute set
/// minus <c>solution</c>). So the pac-verified YAML shape is:
/// <c>MissingDependencies: / MissingDependency: / Required: { '@schemaName': ..., '@displayName': ... } / Dependent: { ... }</c>,
/// with <c>MissingDependency</c> collapsing to a single mapping when there is
/// exactly one entry and a sequence when there is more than one (the same
/// single-vs-sequence behaviour G9 documented for <c>Component</c>, since both
/// go through the identical YAML to ExpandoObject to JSON to XML pipeline).
/// This gate parses that verified shape and names the dependency from
/// <c>Required</c>'s <c>displayName</c> (falling back to <c>schemaName</c>,
/// then <c>type</c>). Per the frozen ruling, any content found under
/// <c>MissingDependencies</c> that does not match this shape is still refused;
/// its presence alone is the finding regardless of exact shape.
/// </para>
/// </summary>
public sealed class DependencyIntegrityGate : IGate
{
    public string Id => "G3";
    public string Name => "dependency-integrity";
    public bool RequiresTenant => false;

    public IEnumerable<GateVerdict> Evaluate(GateContext context)
    {
        var solutionsDir = Path.Combine(context.SolutionRoot, "solutions");

        if (!Directory.Exists(solutionsDir))
        {
            yield return Refuse(
                context,
                RelativePath(context, solutionsDir),
                "Looked for a 'solutions' folder under the solution root; none exists.",
                "No 'solutions' folder was found, so no missingdependencies.yml can be checked. " +
                "There is nothing here for a pack to succeed over.");
            yield break;
        }

        var solutionDirs = Directory.GetDirectories(solutionsDir)
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToList();

        if (solutionDirs.Count == 0)
        {
            yield return Refuse(
                context,
                RelativePath(context, solutionsDir),
                "Listed the 'solutions' folder; it contains zero solution directories.",
                "The 'solutions' folder is empty. There is no missingdependencies.yml to verify, " +
                "which is indistinguishable from a declared missing dependency having been lost.");
            yield break;
        }

        foreach (var solutionDir in solutionDirs)
        {
            foreach (var verdict in EvaluateSolution(context, solutionDir))
                yield return verdict;
        }
    }

    private IEnumerable<GateVerdict> EvaluateSolution(GateContext context, string solutionDir)
    {
        var dependenciesFile = Path.Combine(solutionDir, "missingdependencies.yml");
        var relDependenciesFile = RelativePath(context, dependenciesFile);

        if (!File.Exists(dependenciesFile))
        {
            yield return Refuse(
                context,
                relDependenciesFile,
                $"Looked for {relDependenciesFile}; the file is absent.",
                "missingdependencies.yml is missing. It is one of the four required manifests " +
                "(fail closed), and its absence means declared missing dependencies cannot be " +
                "ruled out before packing.");
            yield break;
        }

        YamlMappingNode? root;
        YamlException? parseError = null;
        try
        {
            root = ParseRoot(dependenciesFile);
        }
        catch (YamlException ex)
        {
            root = null;
            parseError = ex;
        }

        if (parseError is not null)
        {
            yield return Refuse(
                context,
                relDependenciesFile,
                $"Attempted to parse {relDependenciesFile} as YAML.",
                $"missingdependencies.yml could not be parsed as YAML: {parseError.Message}");
            yield break;
        }

        if (root is null || !TryGetMappingValue(root, "MissingDependencies", out var missingDependenciesNode) ||
            missingDependenciesNode is null)
        {
            yield return Refuse(
                context,
                relDependenciesFile,
                $"Parsed {relDependenciesFile}; it has no top level 'MissingDependencies' key.",
                "missingdependencies.yml does not contain the 'MissingDependencies' mapping pac's " +
                "own parser requires (DiskReader.GetMissingDependencies / " +
                "Helper.LoadSolutionInformation). Whether any dependency is missing cannot be " +
                "verified, so this fails closed rather than passing on an unreadable claim.");
            yield break;
        }

        if (missingDependenciesNode is not YamlMappingNode missingDependenciesMapping)
        {
            yield return Refuse(
                context,
                relDependenciesFile,
                $"Parsed {relDependenciesFile}; 'MissingDependencies' is present but is not a " +
                "mapping (the pac-verified empty state is 'MissingDependencies: {}').",
                "missingdependencies.yml declares 'MissingDependencies' as something other than a " +
                "mapping. This does not match the pac-verified empty shape, so its presence alone " +
                "is the finding regardless of its exact shape: a solution in this state cannot be " +
                "trusted to import without a missing dependency.");
            yield break;
        }

        if (missingDependenciesMapping.Children.Count == 0)
        {
            yield return Pass(
                context,
                relDependenciesFile,
                $"Parsed {relDependenciesFile}; 'MissingDependencies' is an empty mapping " +
                "('MissingDependencies: {}'), the pac-verified state for zero declared missing " +
                "dependencies.");
            yield break;
        }

        var refusedAny = false;

        foreach (var child in missingDependenciesMapping.Children)
        {
            if (child.Key is not YamlScalarNode childKey)
                continue;

            if (childKey.Value == "MissingDependency")
            {
                foreach (var entry in EnumerateEntries(child.Value))
                {
                    refusedAny = true;
                    yield return RefuseDeclaredDependency(context, relDependenciesFile, entry);
                }
            }
            else
            {
                refusedAny = true;
                yield return Refuse(
                    context,
                    relDependenciesFile,
                    $"Parsed {relDependenciesFile}; 'MissingDependencies' declares a child " +
                    $"'{childKey.Value}' that is not 'MissingDependency'.",
                    $"missingdependencies.yml declares a non-empty 'MissingDependencies' mapping " +
                    $"with an unrecognised child '{childKey.Value}'. Its exact shape was not " +
                    "resolved by decompilation, but per the frozen ruling any declared entry is " +
                    "the finding regardless of shape: a solution carrying declared missing " +
                    "dependencies fails at import on any environment that lacks them, and the " +
                    "platform only tells you at import time.");
            }
        }

        // Defensive: a non-empty mapping whose only children were scalar-keyed
        // entries we skipped (Key not a YamlScalarNode) would otherwise fall
        // through with no verdict at all, which is a silent pass in disguise.
        if (!refusedAny)
        {
            yield return Refuse(
                context,
                relDependenciesFile,
                $"Parsed {relDependenciesFile}; 'MissingDependencies' is a non-empty mapping but " +
                "no recognisable entry could be extracted from it.",
                "missingdependencies.yml declares a non-empty 'MissingDependencies' mapping whose " +
                "content could not be resolved to a specific entry. Per the frozen ruling, its " +
                "presence alone is the finding: a solution carrying declared missing dependencies " +
                "fails at import on any environment that lacks them, and the platform only tells " +
                "you at import time.");
        }
    }

    private GateVerdict RefuseDeclaredDependency(GateContext context, string relDependenciesFile, YamlNode entry)
    {
        var name = ExtractRequiredName(entry);

        if (name is not null)
        {
            return Refuse(
                context,
                relDependenciesFile,
                $"Parsed {relDependenciesFile}; found a MissingDependency entry whose Required " +
                $"dependency is '{name}'.",
                $"missingdependencies.yml declares a missing dependency on '{name}'. A solution " +
                "carrying declared missing dependencies fails at import on any environment that " +
                "lacks them, and the platform only tells you at import time.");
        }

        return Refuse(
            context,
            relDependenciesFile,
            $"Parsed {relDependenciesFile}; found a MissingDependency entry whose Required " +
            "element could not be named (no '@displayName', '@schemaName', or '@type').",
            "missingdependencies.yml declares a MissingDependency entry. It could not be named " +
            "from its Required element, but per the frozen ruling any declared entry is the " +
            "finding regardless of its exact shape: a solution carrying declared missing " +
            "dependencies fails at import on any environment that lacks them, and the platform " +
            "only tells you at import time.");
    }

    /// <summary>
    /// Names a declared dependency from its Required element, preferring the
    /// human readable displayName, per pac's ExtractRequredDependancy which
    /// reads the same three attributes off Required in this order of
    /// usefulness for a reader (schemaName and type are stable identifiers,
    /// displayName is what a person actually recognises).
    /// </summary>
    private static string? ExtractRequiredName(YamlNode entry)
    {
        if (entry is not YamlMappingNode entryMapping)
            return null;

        if (!TryGetMappingValue(entryMapping, "Required", out var requiredNode) ||
            requiredNode is not YamlMappingNode requiredMapping)
            return null;

        return TryGetScalar(requiredMapping, "@displayName")
            ?? TryGetScalar(requiredMapping, "@schemaName")
            ?? TryGetScalar(requiredMapping, "@type");
    }

    /// <summary>
    /// Parses missingdependencies.yml with the low-level representation model
    /// rather than a POCO mapping, so the document's exact structure (mapping
    /// vs. sequence vs. scalar, and which keys are actually present) can be
    /// inspected directly, the same technique G9 uses to tell the documented
    /// legacy shape apart from the pac-verified one.
    /// </summary>
    private static YamlMappingNode? ParseRoot(string dependenciesFile)
    {
        using var reader = new StreamReader(dependenciesFile);
        var yamlStream = new YamlStream();
        yamlStream.Load(reader);

        if (yamlStream.Documents.Count == 0)
            return null;

        return yamlStream.Documents[0].RootNode as YamlMappingNode;
    }

    /// <summary>
    /// MissingDependency holds either a single entry (a mapping) or a list of
    /// entries (a sequence of mappings), matching the JSON array vs. single
    /// object behaviour of Helper.ConvertYamlToXml's YAML to JSON to XML
    /// pipeline: pac's own parser accepts both.
    /// </summary>
    private static IEnumerable<YamlNode> EnumerateEntries(YamlNode node)
    {
        if (node is YamlSequenceNode sequence)
        {
            foreach (var item in sequence.Children)
                yield return item;
        }
        else
        {
            yield return node;
        }
    }

    private static bool TryGetMappingValue(YamlMappingNode mapping, string key, out YamlNode? value)
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

    private static string? TryGetScalar(YamlMappingNode mapping, string key) =>
        TryGetMappingValue(mapping, key, out var value) && value is YamlScalarNode scalar
            ? scalar.Value
            : null;

    private static string RelativePath(GateContext context, string absolutePath) =>
        Path.GetRelativePath(context.RepositoryRoot, absolutePath).Replace('\\', '/');

    private static GateVerdict Pass(GateContext context, string artifact, string evidence) => new()
    {
        GateId = "G3",
        GateName = "dependency-integrity",
        Outcome = GateOutcome.Pass,
        Artifact = artifact,
        Evidence = evidence,
        At = context.Now,
        Stage = context.Stage
    };

    private static GateVerdict Refuse(GateContext context, string artifact, string evidence, string reason) => new()
    {
        GateId = "G3",
        GateName = "dependency-integrity",
        Outcome = GateOutcome.Refuse,
        Artifact = artifact,
        Evidence = evidence,
        Reason = reason,
        At = context.Now,
        Stage = context.Stage
    };
}
