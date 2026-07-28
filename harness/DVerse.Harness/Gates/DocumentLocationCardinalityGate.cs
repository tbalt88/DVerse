using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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
/// </summary>
public sealed class DocumentLocationCardinalityGate : IGate
{
    /// <summary>
    /// The Dataverse document-location table. Comparison is case-insensitive
    /// because logical names are lowercase by convention but schema names are
    /// not, and both appear in relationship metadata.
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
                RelationshipsFolder,
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
            var relative = Relative(context, file);
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
                + $"{relationship.RelationshipType ?? "(unspecified)"}, referencing "
                + $"'{relationship.ReferencingEntityName ?? "(none)"}', referenced "
                + $"'{relationship.ReferencedEntityName ?? "(none)"}'.",
                problem);
        }

        if (refused)
            yield break;

        yield return Verdict(
            context,
            GateOutcome.Pass,
            RelationshipsFolder,
            $"Inspected {inspected} relationship file(s) under {RelationshipsFolder}/; "
            + $"{documentRelationships} touch a SharePoint document table and all are "
            + "one-to-many with the document table on the many side.");
    }

    /// <summary>
    /// Returns the reason this relationship is wrong, or null when it conforms.
    /// </summary>
    private static string? Diagnose(RelationshipModel r)
    {
        var type = r.RelationshipType?.Replace(" ", string.Empty) ?? string.Empty;

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

    private static RelationshipModel Parse(string file, string relative)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var text = File.ReadAllText(file);

        // A malformed relationship file is a gate defect surfaced as an
        // exception, which GateRunner converts into a Refuse. Failing closed is
        // correct: an unparseable relationship is exactly the case where we
        // cannot claim the rule holds.
        var doc = deserializer.Deserialize<RelationshipDocument>(text)
            ?? throw new InvalidDataException($"{relative}: empty relationship document.");

        return doc.EntityRelationship
            ?? throw new InvalidDataException(
                $"{relative}: no EntityRelationship node found.");
    }

    private static string Relative(GateContext context, string absolute)
    {
        var rel = Path.GetRelativePath(context.SolutionRoot, absolute);
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

    // Serialization shape.
    //
    // HONEST LIMIT, must be resolved in wave 2: this shape is INFERRED from the
    // documented YAML source-control format, not observed from a real
    // `pac solution clone`. No Dataverse tenant exists yet, so no genuine
    // unpacked solution has been seen. The RULE is grounded in Microsoft's
    // documentation and is correct; the exact YAML node names must be confirmed
    // against real clone output when the trial environment is created, and this
    // parser adjusted if they differ. Tracked as a wave 2 obligation.

    private sealed class RelationshipDocument
    {
        public RelationshipModel? EntityRelationship { get; set; }
    }

    private sealed class RelationshipModel
    {
        public string? Name { get; set; }
        public string? RelationshipType { get; set; }
        public string? ReferencingEntityName { get; set; }
        public string? ReferencedEntityName { get; set; }
    }
}
