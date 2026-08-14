using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Gates;

/// <summary>
/// G4. Refuses any relationship to <c>SharePointDocumentLocation</c> that is not
/// one-to-many, with the custom entity on the "one" side.
/// <para>
/// This is the flagship gate because its violation fails <b>silently</b>.
/// Microsoft documents the behaviour directly:
/// </para>
/// <para>
/// "Power Apps and Dataverse support only a one-to-many relationship (1:N)
/// between any entity and a SharePoint document entity. A many-to-one or a
/// many-to-many relationship between an entity and a SharePoint document entity
/// results in the app not listing the documents that exist in the SharePoint
/// document library."
/// </para>
/// <para>
/// Nothing catches this. The solution imports cleanly, publishes cleanly, and
/// Power Apps Checker says nothing. The only symptom is that the Documents tab
/// is empty, at which point the cause is several layers away from the effect.
/// That combination, legal to author and invisible when wrong, is precisely
/// what a governance gate is for.
/// </para>
/// <para>
/// REALIGNMENT, slice 4.3, following the G9 precedent exactly: the wave 1
/// shape below was an inferred YAML mapping (<c>Name:</c> as a plain child,
/// <c>RelationshipType:</c>) never checked against a real pac reader. Decompiling
/// pac 2.10.1's SolutionPackagerLib.dll with ilspycmd
/// (<c>Microsoft.Crm.Tools.SolutionPackager.EntityRelationshipProcessor</c> and
/// its base <c>ComponentProcessorBase</c>) shows the real reader:
/// <c>ReadFromFilesInNewFormat</c> walks every file under
/// <c>entityrelationships/</c>, loads each one via <c>XUtils.LoadElement</c> ->
/// <c>Helper.ConvertYamlToXml</c> (the same YAML-to-XML round trip behind every
/// other <c>@attr</c> file in this repo, and the one G9 first identified), and
/// appends the file's root element as-is under an <c>&lt;EntityRelationships&gt;</c>
/// collection. <c>ComponentProcessorBase.CreateComponent</c> then requires
/// <c>Helper.GetAttributeValue(element, "Name", throwIfNull: true)</c>: the
/// relationship's name is an XML ATTRIBUTE on the root <c>EntityRelationship</c>
/// element, not a child element. This is confirmed directly by the
/// platform-authored reference embedded in the slice 4.3 spec
/// (<c>&lt;EntityRelationship Name="contact_SharePointDocumentLocations"&gt;</c>),
/// and by an empirical pack round trip run this slice (see
/// docs/receipts/wave4-3-refusal-pair.md): a file with <c>Name</c> as a child
/// element produces a component pac's own writer never emits.
/// </para>
/// <para>
/// This gate therefore parses only the pac-verified shape, the same low-level
/// representation-model technique G9, G8, and G3 already use for every other
/// <c>@attr</c>-keyed file in this repo, rather than a POCO mapping that cannot
/// see the "@Name" attribute distinctly from a same-named child element. The
/// SEMANTICS are unchanged from wave 1: an entity-to-SharePointDocumentLocation
/// (or SharePointSite) relationship must be one-to-many with the document table
/// as the REFERENCING (many) side; anything else refuses, naming the silent
/// empty-Documents-tab symptom. This pin is tied to pac 2.10.1's observed
/// behaviour and may need revisiting if a future pac release changes its parser.
/// </para>
/// </summary>
public sealed class DocumentLocationCardinalityGate : IGate
{
    /// <summary>
    /// The Dataverse document-location table. Comparison is case-insensitive
    /// because logical names are lowercase by convention but schema names are
    /// not (the platform-authored reference uses "SharePointDocumentLocation"),
    /// and both appear in relationship metadata.
    /// </summary>
    private const string DocumentLocationEntity = "sharepointdocumentlocation";

    /// <summary>Also covered: the site table participates in the same integration.</summary>
    private const string DocumentSiteEntity = "sharepointsite";

    private const string RelationshipsFolder = "entityrelationships";

    public string Id => "G4";
    public string Name => "document-location-cardinality";
    public bool RequiresTenant => false;

    public IEnumerable<GateVerdict> Evaluate(GateContext context)
    {
        var root = Path.Combine(context.SolutionRoot, RelationshipsFolder);

        if (!Directory.Exists(root))
        {
            // No relationships folder means no relationships to get wrong. This
            // is a genuine pass, not a skip: the rule holds vacuously and the
            // evidence says exactly why.
            yield return Verdict(
                context,
                GateOutcome.Pass,
                RelativeArtifact(context, root),
                $"No {RelationshipsFolder}/ directory present, so no relationship "
                + "to a SharePoint document table exists to violate the rule.");
            yield break;
        }

        var files = Directory
            .EnumerateFiles(root, "*.yml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(root, "*.yaml", SearchOption.AllDirectories))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        var inspected = 0;
        var documentRelationships = 0;
        var refused = false;

        foreach (var file in files)
        {
            var relative = RelativeArtifact(context, file);
            var relationship = Parse(file, relative);
            inspected++;

            if (!TouchesDocumentTable(relationship))
                continue;

            documentRelationships++;

            var problem = Diagnose(relationship);
            if (problem is null)
                continue;

            refused = true;
            yield return Verdict(
                context,
                GateOutcome.Refuse,
                relative,
                $"Inspected relationship '{relationship.Name}': type "
                + $"{relationship.EntityRelationshipType ?? "(unspecified)"}, referencing "
                + $"'{relationship.ReferencingEntityName ?? "(none)"}', referenced "
                + $"'{relationship.ReferencedEntityName ?? "(none)"}'.",
                problem);
        }

        if (refused)
            yield break;

        yield return Verdict(
            context,
            GateOutcome.Pass,
            RelativeArtifact(context, root),
            $"Inspected {inspected} relationship file(s) under {RelationshipsFolder}/; "
            + $"{documentRelationships} touch a SharePoint document table and all are "
            + "one-to-many with the document table on the many side.");
    }

    /// <summary>
    /// Returns the reason this relationship is wrong, or null when it conforms.
    /// </summary>
    private static string? Diagnose(RelationshipModel r)
    {
        var type = r.EntityRelationshipType?.Replace(" ", string.Empty) ?? string.Empty;

        if (type.Equals("ManyToMany", StringComparison.OrdinalIgnoreCase))
        {
            return "Many-to-many relationship to a SharePoint document table. "
                 + "Dataverse supports only 1:N here. Documents will not be listed, "
                 + "and nothing will report an error.";
        }

        // For a correct 1:N, the document table is the MANY side, which means it
        // must be the referencing entity. If it is the referenced entity, the
        // cardinality is inverted.
        var referencing = r.ReferencingEntityName ?? string.Empty;
        var referenced = r.ReferencedEntityName ?? string.Empty;

        if (IsDocumentTable(referenced) && !IsDocumentTable(referencing))
        {
            return $"Relationship is inverted: '{referenced}' is the referenced (one) "
                 + $"side and '{referencing}' is the referencing (many) side. Dataverse "
                 + "requires the document table on the MANY side (1:N from the custom "
                 + "entity). As authored, the Documents tab will be silently empty.";
        }

        // Not empirically observed from a real pac writer (Dataverse's own
        // EntityRelationshipType enum carries only OneToMany and ManyToMany; a
        // many-to-one authoring shows up as the inverted OneToMany case above),
        // but Microsoft's own documentation names "many-to-one" as an authorable
        // failure mode in its own right. Kept as a defensive second check so an
        // explicitly-authored ManyToOne value is still caught rather than
        // silently falling through an unrecognised type.
        if (type.Equals("ManyToOne", StringComparison.OrdinalIgnoreCase)
            && IsDocumentTable(referencing) is false)
        {
            return "Many-to-one relationship to a SharePoint document table. "
                 + "Dataverse supports only 1:N here. Documents will not be listed.";
        }

        return null;
    }

    private static bool TouchesDocumentTable(RelationshipModel r) =>
        IsDocumentTable(r.ReferencingEntityName) || IsDocumentTable(r.ReferencedEntityName);

    private static bool IsDocumentTable(string? name) =>
        name is not null
        && (name.Equals(DocumentLocationEntity, StringComparison.OrdinalIgnoreCase)
            || name.Equals(DocumentSiteEntity, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Parses one entityrelationships/*.yml file with the low-level
    /// representation model rather than a POCO mapping, so the pac-verified
    /// '@Name' ATTRIBUTE can be told apart from a same-named child element,
    /// the same technique G9, G8, and G3 use for every other '@attr'-keyed
    /// file in this repo. A malformed document (bad YAML syntax, or no
    /// EntityRelationship mapping at all) throws rather than returning a
    /// half-populated model: GateRunner converts that into a Refuse, so
    /// failing closed here is correct, not merely convenient.
    /// </summary>
    private static RelationshipModel Parse(string file, string relative)
    {
        using var reader = new StreamReader(file);
        var yamlStream = new YamlStream();
        yamlStream.Load(reader);

        if (yamlStream.Documents.Count == 0)
            throw new InvalidDataException($"{relative}: empty relationship document.");

        var root = yamlStream.Documents[0].RootNode;

        if (root is not YamlMappingNode rootMapping ||
            !TryGetMappingValue(rootMapping, "EntityRelationship", out var relationshipNode) ||
            relationshipNode is not YamlMappingNode relationshipMapping)
        {
            throw new InvalidDataException(
                $"{relative}: no EntityRelationship mapping found.");
        }

        return new RelationshipModel(
            Name: TryGetScalar(relationshipMapping, "@Name"),
            EntityRelationshipType: TryGetScalar(relationshipMapping, "EntityRelationshipType"),
            ReferencingEntityName: TryGetScalar(relationshipMapping, "ReferencingEntityName"),
            ReferencedEntityName: TryGetScalar(relationshipMapping, "ReferencedEntityName"));
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

    /// <summary>
    /// Repo-relative form of an absolute path, per the frozen rule that every
    /// Artifact is expressed relative to <see cref="GateContext.RepositoryRoot"/>,
    /// never the solution root, so the ledger uses one path base across every
    /// gate and stays reproducible across machines.
    /// </summary>
    private static string RelativeArtifact(GateContext context, string absolute)
    {
        var rel = Path.GetRelativePath(context.RepositoryRoot, absolute);
        return rel.Replace('\\', '/');
    }

    private static GateVerdict Verdict(
        GateContext context,
        GateOutcome outcome,
        string artifact,
        string evidence,
        string? reason = null) => new()
        {
            GateId = "G4",
            GateName = "document-location-cardinality",
            Outcome = outcome,
            Artifact = artifact,
            Evidence = evidence,
            Reason = reason,
            At = context.Now,
            Stage = context.Stage
        };

    /// <summary>
    /// Serialization shape, pac-verified this slice (see the class-level
    /// REALIGNMENT remarks): '@Name' is the XML attribute pac's own
    /// CreateComponent requires; every other field is a plain child element,
    /// matching the platform-authored reference's element names exactly.
    /// </summary>
    private sealed record RelationshipModel(
        string? Name,
        string? EntityRelationshipType,
        string? ReferencingEntityName,
        string? ReferencedEntityName);
}
