using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Gates;

/// <summary>
/// G8: every declared component in every <c>solutions/*/rootcomponents.yml</c>
/// whose type maps to a known on-disk source location must resolve to a real
/// file or directory under the solution root.
/// <para>
/// This exists because SolutionPackager's own documented behaviour makes the
/// failure silent: if a root component's source is absent, pack drops the
/// component, still exits 0, and the missing piece only surfaces later as an
/// import failure (or, worse, a runtime failure) against a build that
/// reported success. This gate turns that exit-code-0 lie into a refusal
/// before the pack ever runs. This closes standing obligation O2.
/// </para>
/// <para>
/// GROUND TRUTH: decompiling pac 2.10.1's SolutionPackagerLib.dll with
/// ilspycmd (same tool and pac version as slice 4.1's G9 precedent) shows
/// <c>Microsoft.Crm.Tools.SolutionPackager.DiskReader.GetRootComponents()</c>
/// reading <c>solutions/&lt;name&gt;/rootcomponents.yml</c> and converting it
/// with <c>Helper.ConvertYamlToXml</c>, the exact same YAML-to-XML path
/// <c>GetSolutionComponentPaths()</c> uses for solutioncomponents.yml (G9's
/// precedent, which found the real '@path' attribute convention). The
/// resulting <c>&lt;RootComponents&gt;&lt;RootComponent&gt;</c> elements are
/// parsed by the shared <c>Helper.LoadSolutionInformation</c> code path
/// (used for both legacy XML and the new YAML-derived XML) via
/// <c>GetAttributeValue(item3, "type"|"schemaName"|"behavior"|"id", ...)</c>,
/// with "type" read through <c>int.TryParse</c>. Given the shared JSON round
/// trip's '@'-attribute convention (confirmed by G9's '@path' finding), the
/// pac-verified shape is a <c>RootComponents: RootComponent:</c> mapping
/// whose entries carry literal <c>@type</c> (the numeric
/// <c>ComponentType</c> id, since the reader parses it with
/// <c>int.TryParse</c>) and <c>@schemaName</c> keys, mirroring
/// solutioncomponents.yml's <c>@path</c> shape. The empty form is a mapping
/// (<c>RootComponents: {}</c>), never a YAML list; this is a pac-verified
/// fact carried over from slice 4.1's decompilation and confirmed by the
/// demo solution's shell fixture. ASSUMPTION surfaced honestly: no
/// non-empty rootcomponents.yml has been observed from a real pac unpack
/// (the demo solution has zero components), so the numeric-vs-named '@type'
/// value and the exact attribute casing are inferred from the shared reader
/// code path above, not from an empirical sample. A YAML-list-shaped
/// <c>RootComponents</c> value is refused as the wrong (undocumented,
/// unverified) shape rather than accepted, for the same reason G9 refuses
/// the documented flat-sequence shape for solutioncomponents.yml.
/// </para>
/// <para>
/// Type-to-path mapping starts minimal (Entity, CanvasApp) per the frozen
/// ruling. A declared component whose type has no known mapping is not
/// refused: it is counted in the Pass evidence with an explicit note naming
/// the unmapped type, because refusing on ignorance of a type this gate has
/// never been taught would make the gate cry wolf. Extend the map only for
/// types verified the same way Entity and CanvasApp were.
/// </para>
/// <para>
/// SLICE 4.4c: a RootComponent entry may legitimately carry '@id' with no
/// '@schemaName' at all. Ground truth: `pac solution clone --name
/// DVerseCore` (read-only, dverse-ci profile), quoted verbatim in
/// loop/specs/wave4-4c-canonical-plugin-shapes.md, shows the platform's own
/// export for a SdkMessageProcessingStep RootComponent
/// (<c>&lt;RootComponent type="92" id="{...}" behavior="0" /&gt;</c>) with
/// no schemaName attribute whatsoever -- GUID-identified component types are
/// named by '@id' alone in the platform's own writer, matching
/// Helper.LoadSolutionInformation's generic "type"|"schemaName"|"behavior"|
/// "id" read (cited above) reading all four independently rather than
/// requiring schemaName. This gate previously refused every blank
/// '@schemaName' unconditionally, which made the 4.4b executor add a
/// redundant schemaName workaround just to satisfy this gate -- a gate
/// defect the canonical export exposed. An entry with a non-empty '@id' and
/// no '@schemaName' now passes, its source verification skipped with an
/// explicit Evidence line (the same honest-skip pattern used for unmapped
/// types above, since no type-to-path template here is keyed by '@id').
/// An entry with NEITHER '@id' NOR '@schemaName' still refuses: there is no
/// way to resolve or even honestly skip a component with no identifier at
/// all.
/// </para>
/// </summary>
public sealed class RootComponentSourceGate : IGate
{
    public string Id => "G8";
    public string Name => "rootcomponent-sources";
    public bool RequiresTenant => false;

    /// <summary>
    /// Numeric <c>ComponentType</c> id (as decompiled from
    /// SolutionPackagerLib.dll's <c>ComponentType</c> enum) to the
    /// conventional on-disk folder template for that type's source.
    /// {0} is the component's <c>@schemaName</c> value.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> TypePathTemplates =
        new Dictionary<string, string>
        {
            ["1"] = "entities/{0}",             // Entity
            // CanvasApp: the wave-1 template guessed a directory named after the
            // schemaName; the first REAL canvas component (wave 8 addendum,
            // Matter Canvas as dv_mattercanvas_791bc) proved the platform's
            // YAML-format shape is a file, canvasapps/<name>.meta.yml
            // (decompile: CanvasAppsProcessor registers "$(PrimaryName).meta.xml"
            // and GetNewFormatFileName swaps to .yml). Lesson 16's class: when a
            // gate refuses platform-authored output, suspect the gate first.
            ["300"] = "canvasapps/{0}.meta.yml" // CanvasApp
        };

    /// <summary>Friendly names for the numeric ids above, used only in messages.</summary>
    private static readonly IReadOnlyDictionary<string, string> TypeFriendlyNames =
        new Dictionary<string, string>
        {
            ["1"] = "Entity",
            ["300"] = "CanvasApp"
        };

    public IEnumerable<GateVerdict> Evaluate(GateContext context)
    {
        var solutionsDir = Path.Combine(context.SolutionRoot, "solutions");

        if (!Directory.Exists(solutionsDir))
        {
            yield return Refuse(
                context,
                RelativePath(context, solutionsDir),
                "Looked for a 'solutions' folder under the solution root; none exists.",
                "No 'solutions' folder was found, so no rootcomponents.yml can be checked. " +
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
                "The 'solutions' folder is empty. There is no rootcomponents.yml to verify, " +
                "which is indistinguishable from every declared root component having been " +
                "lost.");
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
        var rootComponentsFile = Path.Combine(solutionDir, "rootcomponents.yml");
        var relRootComponentsFile = RelativePath(context, rootComponentsFile);

        if (!File.Exists(rootComponentsFile))
        {
            yield return Refuse(
                context,
                relRootComponentsFile,
                $"Looked for {relRootComponentsFile}; the file is absent.",
                "rootcomponents.yml is missing, so no declared root component can be verified " +
                "to have a source on disk before packing.");
            yield break;
        }

        ParsedManifest? manifest;
        YamlException? parseError = null;
        try
        {
            manifest = ParseRootComponents(rootComponentsFile);
        }
        catch (YamlException ex)
        {
            manifest = null;
            parseError = ex;
        }

        if (parseError is not null)
        {
            yield return Refuse(
                context,
                relRootComponentsFile,
                $"Attempted to parse {relRootComponentsFile} as YAML.",
                $"rootcomponents.yml could not be parsed as YAML: {parseError.Message}");
            yield break;
        }

        if (manifest!.IsListShape)
        {
            yield return Refuse(
                context,
                relRootComponentsFile,
                $"Parsed {relRootComponentsFile}; its 'RootComponents' value is a YAML " +
                "sequence rather than a mapping.",
                $"{relRootComponentsFile} declares 'RootComponents' as a YAML list. pac's " +
                "verified empty form is a mapping ('RootComponents: {}'), never a list " +
                "(confirmed by decompiling pac 2.10.1's SolutionPackagerLib.dll during slice " +
                "4.1); a list is not the shape pac's own writer produces, so this file cannot " +
                "be trusted to reflect what pac will actually pack. Rewrite " +
                "'RootComponents' as a mapping.");
            yield break;
        }

        var entries = manifest.Entries;

        if (entries.Count == 0)
        {
            yield return Pass(
                context,
                relRootComponentsFile,
                $"Parsed {relRootComponentsFile}; 'RootComponents' is an empty mapping. " +
                "Zero root components declared, zero sources to verify.");
            yield break;
        }

        var unresolvedCount = 0;
        var resolvedCount = 0;
        var unmappedNotes = new List<string>();
        var idOnlyNotes = new List<string>();

        foreach (var entry in entries)
        {
            var hasId = !string.IsNullOrWhiteSpace(entry.Id);

            if (string.IsNullOrWhiteSpace(entry.SchemaName))
            {
                if (hasId)
                {
                    var idTypeLabel = string.IsNullOrWhiteSpace(entry.Type) ? "(none)" : entry.Type;
                    idOnlyNotes.Add($"'{entry.Id}' (type {idTypeLabel})");
                    continue;
                }

                unresolvedCount++;
                yield return Refuse(
                    context,
                    relRootComponentsFile,
                    $"Read a RootComponent entry from {relRootComponentsFile}; both its " +
                    "'@schemaName' and '@id' values are blank.",
                    "rootcomponents.yml contains a RootComponent entry with neither a " +
                    "'@schemaName' nor an '@id' value, so no source path can be resolved for " +
                    "it and the packer will silently drop that component.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.Type) ||
                !TypePathTemplates.TryGetValue(entry.Type, out var template))
            {
                var typeLabel = string.IsNullOrWhiteSpace(entry.Type) ? "(none)" : entry.Type;
                unmappedNotes.Add($"'{entry.SchemaName}' (type {typeLabel})");
                continue;
            }

            var declaredPath = string.Format(template, entry.SchemaName);
            var target = Path.Combine(
                context.SolutionRoot,
                declaredPath.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(target) || Directory.Exists(target))
            {
                resolvedCount++;
                continue;
            }

            unresolvedCount++;
            var relTarget = RelativePath(context, target);
            var friendlyType = TypeFriendlyNames.GetValueOrDefault(entry.Type, entry.Type);

            yield return Refuse(
                context,
                relTarget,
                $"Resolved {friendlyType} root component '{entry.SchemaName}' from " +
                $"{relRootComponentsFile} to '{declaredPath}' against disk; no file or " +
                "directory exists there.",
                $"rootcomponents.yml declares {friendlyType} '{entry.SchemaName}' but no " +
                $"'{declaredPath}' exists under the solution root. SolutionPackager will omit " +
                "this root component and still exit 0, so import will fail (or the runtime " +
                "will misbehave) on a component the build reported as successful.");
        }

        if (unresolvedCount == 0)
        {
            var summary =
                $"Parsed {relRootComponentsFile}; resolved {resolvedCount} of " +
                $"{entries.Count} declared root component" +
                $"{(entries.Count == 1 ? "" : "s")} against disk under " +
                $"{RelativePath(context, context.SolutionRoot)}; all present.";

            if (unmappedNotes.Count > 0)
            {
                summary += " Skipped source verification for " +
                    $"{unmappedNotes.Count} entr{(unmappedNotes.Count == 1 ? "y" : "ies")} " +
                    $"with a type this gate has no path mapping for: " +
                    string.Join(", ", unmappedNotes) +
                    ". Not refused: an unmapped type is not evidence of a missing source, " +
                    "only of a gap in this gate's mapping table.";
            }

            if (idOnlyNotes.Count > 0)
            {
                summary += " Skipped source verification for " +
                    $"{idOnlyNotes.Count} entr{(idOnlyNotes.Count == 1 ? "y" : "ies")} " +
                    "identified by '@id' only, no '@schemaName': " +
                    string.Join(", ", idOnlyNotes) +
                    ". Not refused: GUID-identified component types carry no schema name in " +
                    "the platform's own canonical export (see " +
                    "loop/specs/wave4-4c-canonical-plugin-shapes.md), so an id-only entry is " +
                    "not evidence of a missing source, only of a component type this gate has " +
                    "no id-keyed path mapping for.";
            }

            yield return Pass(context, relRootComponentsFile, summary);
        }
    }

    /// <summary>
    /// Parses rootcomponents.yml with the low-level representation model so
    /// the shape of the 'RootComponents' value (mapping vs. sequence) can be
    /// inspected directly, the same technique G9 uses for solutioncomponents.yml.
    /// </summary>
    private static ParsedManifest ParseRootComponents(string rootComponentsFile)
    {
        using var reader = new StreamReader(rootComponentsFile);
        var yamlStream = new YamlStream();
        yamlStream.Load(reader);

        if (yamlStream.Documents.Count == 0)
            return new ParsedManifest(IsListShape: false, Entries: []);

        var root = yamlStream.Documents[0].RootNode;

        if (root is not YamlMappingNode rootMapping ||
            !TryGetMappingValue(rootMapping, "RootComponents", out var rootComponentsNode) ||
            rootComponentsNode is null)
        {
            return new ParsedManifest(IsListShape: false, Entries: []);
        }

        if (rootComponentsNode is YamlSequenceNode)
            return new ParsedManifest(IsListShape: true, Entries: []);

        var entries = new List<RootComponentEntry>();

        if (rootComponentsNode is YamlMappingNode rootComponentsMapping &&
            TryGetMappingValue(rootComponentsMapping, "RootComponent", out var componentNode) &&
            componentNode is not null)
        {
            foreach (var entryNode in EnumerateRootComponentEntries(componentNode))
            {
                string? type = null;
                string? schemaName = null;

                string? id = null;

                if (entryNode is YamlMappingNode entryMapping)
                {
                    if (TryGetMappingValue(entryMapping, "@type", out var typeNode) &&
                        typeNode is YamlScalarNode typeScalar)
                    {
                        type = typeScalar.Value;
                    }

                    if (TryGetMappingValue(entryMapping, "@schemaName", out var schemaNameNode) &&
                        schemaNameNode is YamlScalarNode schemaNameScalar)
                    {
                        schemaName = schemaNameScalar.Value;
                    }

                    if (TryGetMappingValue(entryMapping, "@id", out var idNode) &&
                        idNode is YamlScalarNode idScalar)
                    {
                        id = idScalar.Value;
                    }
                }

                entries.Add(new RootComponentEntry(type, schemaName, id));
            }
        }

        return new ParsedManifest(IsListShape: false, Entries: entries);
    }

    /// <summary>
    /// RootComponent holds either a single entry (a mapping) or a list of
    /// entries (a sequence of mappings); the shared JSON round trip produces
    /// both shapes depending on cardinality, mirroring G9's Component handling.
    /// </summary>
    private static IEnumerable<YamlNode> EnumerateRootComponentEntries(YamlNode componentNode)
    {
        if (componentNode is YamlSequenceNode sequence)
        {
            foreach (var item in sequence.Children)
                yield return item;
        }
        else
        {
            yield return componentNode;
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

    private static string RelativePath(GateContext context, string absolutePath) =>
        Path.GetRelativePath(context.RepositoryRoot, absolutePath).Replace('\\', '/');

    private static GateVerdict Pass(GateContext context, string artifact, string evidence) => new()
    {
        GateId = "G8",
        GateName = "rootcomponent-sources",
        Outcome = GateOutcome.Pass,
        Artifact = artifact,
        Evidence = evidence,
        At = context.Now,
        Stage = context.Stage
    };

    private static GateVerdict Refuse(GateContext context, string artifact, string evidence, string reason) => new()
    {
        GateId = "G8",
        GateName = "rootcomponent-sources",
        Outcome = GateOutcome.Refuse,
        Artifact = artifact,
        Evidence = evidence,
        Reason = reason,
        At = context.Now,
        Stage = context.Stage
    };

    private sealed record RootComponentEntry(string? Type, string? SchemaName, string? Id);

    private sealed record ParsedManifest(bool IsListShape, List<RootComponentEntry> Entries);
}
