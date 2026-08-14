using DVerse.Harness.Diff.Internal;
using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Diff;

/// <summary>
/// Stable class-family names, one per row of docs/design/element-identity-
/// model.md section 3 that this slice covers (every row except section 3
/// row 28, Canvas DataCard MetadataKey, which seat ruling 4 explicitly
/// rules out as an identity key at all -- "may appear in changed-verdict
/// detail as advisory context only" -- so it has no matcher and no entry
/// here, on purpose, not by omission). These are the keys
/// <see cref="MatcherRegistry"/> is built from and the values every
/// <see cref="IElementMatcher.ClassFamily"/> returns.
/// </summary>
public static class DiffClassFamilies
{
    public const string SolutionManifest = "SolutionManifest";
    public const string SolutionComponentsEntry = "SolutionComponentsEntry";
    public const string RootComponentsEntry = RootComponentMatcher.FamilyName;
    public const string MissingDependencies = "MissingDependencies";
    public const string Publisher = "Publisher";
    public const string Entity = "Entity";
    public const string Attribute = "Attribute";
    public const string FormXmlSystemForm = "FormXmlSystemForm";
    public const string FormXmlTab = "FormXmlTab";
    public const string FormXmlColumn = "FormXmlColumn";
    public const string FormXmlSection = "FormXmlSection";
    public const string FormXmlRow = "FormXmlRow";
    public const string FormXmlCell = "FormXmlCell";
    public const string FormXmlControl = "FormXmlControl";
    public const string SavedQuery = "SavedQuery";
    public const string SavedQueryLayoutColumn = "SavedQueryLayoutColumn";
    public const string EntityRelationship = "EntityRelationship";
    public const string AppModule = "AppModule";
    public const string AppModuleComponent = "AppModuleComponent";
    public const string AppModuleRoleMapRole = "AppModuleRoleMapRole";
    public const string AppModuleSiteMap = "AppModuleSiteMap";
    public const string SiteMapArea = "SiteMapArea";
    public const string SiteMapGroup = "SiteMapGroup";
    public const string SiteMapSubArea = "SiteMapSubArea";
    public const string PluginAssembly = "PluginAssembly";
    public const string PluginType = "PluginType";
    public const string SdkMessageProcessingStep = "SdkMessageProcessingStep";
    public const string CanvasScreen = "CanvasScreen";
    public const string CanvasControl = "CanvasControl";

    /// <summary>Every family name above, in the same order, for completeness tests and diagnostics.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        SolutionManifest, SolutionComponentsEntry, RootComponentsEntry, MissingDependencies,
        Publisher, Entity, Attribute,
        FormXmlSystemForm, FormXmlTab, FormXmlColumn, FormXmlSection, FormXmlRow, FormXmlCell, FormXmlControl,
        SavedQuery, SavedQueryLayoutColumn,
        EntityRelationship,
        AppModule, AppModuleComponent, AppModuleRoleMapRole,
        AppModuleSiteMap, SiteMapArea, SiteMapGroup, SiteMapSubArea,
        PluginAssembly, PluginType,
        SdkMessageProcessingStep,
        CanvasScreen, CanvasControl
    ];
}

/// <summary>
/// One matcher per ratified class family, keyed by <see cref="DiffClassFamilies"/>.
/// <see cref="CreateDefault"/> is the single place every class family from
/// docs/design/element-identity-model.md section 3 is wired to its matcher,
/// so the "matcher registry table" the mission's Done means asks for
/// (class family -> key rule -> warning behaviour) is this method, read
/// top to bottom. Building the registry never touches disk; it only wires
/// pure functions describing how to walk a YAML tree once one is handed in.
/// </summary>
public sealed class MatcherRegistry
{
    private readonly IReadOnlyDictionary<string, IElementMatcher> _matchers;

    public MatcherRegistry(IReadOnlyDictionary<string, IElementMatcher> matchers)
    {
        _matchers = matchers;
    }

    /// <summary>Every ratified class family, wired to its matcher (or explicit unsurveyed registration).</summary>
    public static MatcherRegistry CreateDefault() => new(BuildDefaults());

    public IReadOnlyCollection<string> RegisteredFamilies => (IReadOnlyCollection<string>)_matchers.Keys;

    public bool TryGet(string classFamily, out IElementMatcher matcher) =>
        _matchers.TryGetValue(classFamily, out matcher!);

    public IElementMatcher Get(string classFamily) =>
        _matchers.TryGetValue(classFamily, out var matcher)
            ? matcher
            : throw new KeyNotFoundException(
                $"No matcher registered for class family '{classFamily}'. Every ratified class " +
                "family must be registered, real or explicitly unsurveyed (mission ruling 2); an " +
                "unregistered lookup is a defect in the registry, not a legitimate 'no opinion'.");

    // Known control classids the ratified model has actually surveyed (section 3 row 14):
    // textbox, datetime, lookup. Any other classid on a matched control is unsurveyed per
    // seat ruling 3 and must warn, never silently match.
    private static readonly HashSet<string> SurveyedControlClassIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "{4273EDBD-AC1D-40d3-9FB2-095C621B552D}", // textbox
        "{5B773807-9FB2-42db-97C3-7A91EFF8ADFF}", // datetime
        "{270BD3DB-D9AF-4782-9025-509E298DEC0A}"  // lookup
    };

    private static Dictionary<string, IElementMatcher> BuildDefaults()
    {
        var matchers = new Dictionary<string, IElementMatcher>
        {
            // --- Solution-level singletons and collections -----------------------------------

            [DiffClassFamilies.SolutionManifest] = new KeyedElementMatcher(
                DiffClassFamilies.SolutionManifest,
                Singleton(root => YamlNodeHelpers.Descend(root, "ImportExportXml", "SolutionManifest"),
                          e => YamlNodeHelpers.ScalarChild(e, "UniqueName"))),

            [DiffClassFamilies.SolutionComponentsEntry] = new KeyedElementMatcher(
                DiffClassFamilies.SolutionComponentsEntry,
                Many(root => YamlNodeHelpers.Descend(root, "SolutionComponents", "Component"),
                     e => YamlNodeHelpers.ScalarChild(e, "@path"))),

            [DiffClassFamilies.RootComponentsEntry] = new RootComponentMatcher(),

            [DiffClassFamilies.MissingDependencies] = new UnsurveyedFamilyMatcher(
                DiffClassFamilies.MissingDependencies,
                root => YamlNodeHelpers.Child(root, "MissingDependencies")),

            [DiffClassFamilies.Publisher] = new KeyedElementMatcher(
                DiffClassFamilies.Publisher,
                Singleton(root => YamlNodeHelpers.Child(root, "Publisher"),
                          e => YamlNodeHelpers.ScalarChild(e, "UniqueName"))),

            // --- Entity and attributes: schema/logical name, case insensitive ------------------

            [DiffClassFamilies.Entity] = new KeyedElementMatcher(
                DiffClassFamilies.Entity,
                Singleton(root => YamlNodeHelpers.Child(root, "Entity"),
                          e => YamlNodeHelpers.ScalarChild(e, "Name")),
                comparer: StringComparer.OrdinalIgnoreCase),

            [DiffClassFamilies.Attribute] = new KeyedElementMatcher(
                DiffClassFamilies.Attribute,
                Singleton(root => YamlNodeHelpers.Child(root, "attribute"),
                          e => YamlNodeHelpers.ScalarChild(e, "LogicalName")),
                comparer: StringComparer.OrdinalIgnoreCase),

            // --- FormXml, nested five deep from the parsed file root ----------------------------

            [DiffClassFamilies.FormXmlSystemForm] = new KeyedElementMatcher(
                DiffClassFamilies.FormXmlSystemForm,
                Singleton(root => YamlNodeHelpers.Child(root, "systemform"),
                          e => YamlNodeHelpers.ScalarChild(e, "formid"))),

            [DiffClassFamilies.FormXmlTab] = new KeyedElementMatcher(
                DiffClassFamilies.FormXmlTab,
                root => FormXmlTabs(root).Select(t => (YamlNodeHelpers.ScalarChild(t, "@id"), t))),

            [DiffClassFamilies.FormXmlColumn] = new PositionalWarningMatcher(
                DiffClassFamilies.FormXmlColumn,
                FormXmlColumns,
                (node, i) => $"{DiffClassFamilies.FormXmlColumn}[#{i}, width={YamlNodeHelpers.ScalarChild(node, "@width") ?? "?"}]"),

            [DiffClassFamilies.FormXmlSection] = new KeyedElementMatcher(
                DiffClassFamilies.FormXmlSection,
                root => FormXmlSections(root).Select(s => (YamlNodeHelpers.ScalarChild(s, "@id"), s))),

            [DiffClassFamilies.FormXmlRow] = new PositionalWarningMatcher(
                DiffClassFamilies.FormXmlRow,
                FormXmlRows,
                (node, i) => $"{DiffClassFamilies.FormXmlRow}[#{i}, cell.id={YamlNodeHelpers.ScalarChild(YamlNodeHelpers.Child(node, "cell"), "@id") ?? "?"}]"),

            [DiffClassFamilies.FormXmlCell] = new KeyedElementMatcher(
                DiffClassFamilies.FormXmlCell,
                root => FormXmlCells(root).Select(c => (YamlNodeHelpers.ScalarChild(c, "@id"), c))),

            [DiffClassFamilies.FormXmlControl] = new KeyedElementMatcher(
                DiffClassFamilies.FormXmlControl,
                root => FormXmlControls(root).Select(c => (YamlNodeHelpers.ScalarChild(c, "@id"), c)),
                warnOnMatch: (_, targetControl) =>
                {
                    var classId = YamlNodeHelpers.ScalarChild(targetControl, "@classid");
                    if (classId is not null && SurveyedControlClassIds.Contains(classId))
                        return null;

                    return $"{DiffClassFamilies.FormXmlControl}: control class '{classId ?? "(none)"}' " +
                           "is not one of the three surveyed classids (textbox, datetime, lookup; " +
                           "docs/design/element-identity-model.md section 3 row 14, seat ruling 3). " +
                           "Matched by '@id' as usual, but this control class is unsurveyed: never " +
                           "trust the match silently.";
                },
                describe: node => $"{DiffClassFamilies.FormXmlControl}[id={YamlNodeHelpers.ScalarChild(node, "@id") ?? "?"}]"),

            // --- SavedQuery ----------------------------------------------------------------------

            [DiffClassFamilies.SavedQuery] = new KeyedElementMatcher(
                DiffClassFamilies.SavedQuery,
                Singleton(root => YamlNodeHelpers.Child(root, "savedquery"),
                          e => YamlNodeHelpers.ScalarChild(e, "savedqueryid"))),

            [DiffClassFamilies.SavedQueryLayoutColumn] = new KeyedElementMatcher(
                DiffClassFamilies.SavedQueryLayoutColumn,
                Many(root => YamlNodeHelpers.Descend(root, "savedquery", "layoutxml", "grid", "row", "cell"),
                     e => YamlNodeHelpers.ScalarChild(e, "@name"))),

            // --- EntityRelationship ----------------------------------------------------------------

            [DiffClassFamilies.EntityRelationship] = new KeyedElementMatcher(
                DiffClassFamilies.EntityRelationship,
                Singleton(root => YamlNodeHelpers.Child(root, "EntityRelationship"),
                          e => YamlNodeHelpers.ScalarChild(e, "@Name"))),

            // --- AppModule and its nested collections -----------------------------------------------

            [DiffClassFamilies.AppModule] = new KeyedElementMatcher(
                DiffClassFamilies.AppModule,
                Singleton(root => YamlNodeHelpers.Child(root, "AppModule"),
                          e => YamlNodeHelpers.ScalarChild(e, "UniqueName"))),

            [DiffClassFamilies.AppModuleComponent] = new KeyedElementMatcher(
                DiffClassFamilies.AppModuleComponent,
                Many(root => YamlNodeHelpers.Descend(root, "AppModule", "AppModuleComponents", "AppModuleComponent"),
                     e =>
                     {
                         var type = YamlNodeHelpers.ScalarChild(e, "@type");
                         var schemaName = YamlNodeHelpers.ScalarChild(e, "@schemaName");
                         return type is null || schemaName is null
                             ? null
                             : $"{type}|{schemaName.ToLowerInvariant()}";
                     })),

            [DiffClassFamilies.AppModuleRoleMapRole] = new KeyedElementMatcher(
                DiffClassFamilies.AppModuleRoleMapRole,
                Many(root => YamlNodeHelpers.Descend(root, "AppModule", "AppModuleRoleMaps", "Role"),
                     e => YamlNodeHelpers.ScalarChild(e, "@id"))),

            // --- AppModuleSiteMap and its hierarchical Area/Group/SubArea ---------------------------

            [DiffClassFamilies.AppModuleSiteMap] = new KeyedElementMatcher(
                DiffClassFamilies.AppModuleSiteMap,
                Singleton(root => YamlNodeHelpers.Child(root, "AppModuleSiteMap"),
                          e => YamlNodeHelpers.ScalarChild(e, "SiteMapUniqueName"))),

            [DiffClassFamilies.SiteMapArea] = new KeyedElementMatcher(
                DiffClassFamilies.SiteMapArea,
                Many(root => YamlNodeHelpers.Descend(root, "AppModuleSiteMap", "SiteMap", "Area"),
                     e => YamlNodeHelpers.ScalarChild(e, "@Id"))),

            [DiffClassFamilies.SiteMapGroup] = new KeyedElementMatcher(
                DiffClassFamilies.SiteMapGroup,
                root => SiteMapAreas(root)
                    .SelectMany(area => YamlNodeHelpers.OneOrMany(YamlNodeHelpers.Child(area, "Group")))
                    .Select(g => (YamlNodeHelpers.ScalarChild(g, "@Id"), g))),

            [DiffClassFamilies.SiteMapSubArea] = new KeyedElementMatcher(
                DiffClassFamilies.SiteMapSubArea,
                root => SiteMapAreas(root)
                    .SelectMany(area => YamlNodeHelpers.OneOrMany(YamlNodeHelpers.Child(area, "Group")))
                    .SelectMany(group => YamlNodeHelpers.OneOrMany(YamlNodeHelpers.Child(group, "SubArea")))
                    .Select(sa => (YamlNodeHelpers.ScalarChild(sa, "@Id"), sa))),

            // --- Plugin registration ------------------------------------------------------------------

            [DiffClassFamilies.PluginAssembly] = new KeyedElementMatcher(
                DiffClassFamilies.PluginAssembly,
                Singleton(root => YamlNodeHelpers.Child(root, "PluginAssembly"),
                          e => YamlNodeHelpers.ScalarChild(e, "@PluginAssemblyId"))),

            [DiffClassFamilies.PluginType] = new KeyedElementMatcher(
                DiffClassFamilies.PluginType,
                Many(root => YamlNodeHelpers.Descend(root, "PluginAssembly", "PluginTypes", "PluginType"),
                     e => YamlNodeHelpers.ScalarChild(e, "@PluginTypeId"))),

            [DiffClassFamilies.SdkMessageProcessingStep] = new KeyedElementMatcher(
                DiffClassFamilies.SdkMessageProcessingStep,
                Singleton(root => YamlNodeHelpers.Child(root, "SdkMessageProcessingStep"),
                          e => YamlNodeHelpers.ScalarChild(e, "@SdkMessageProcessingStepId"))),

            // --- Canvas app: YAML mapping key is the identity, never a platform id --------------------

            [DiffClassFamilies.CanvasScreen] = new KeyedElementMatcher(
                DiffClassFamilies.CanvasScreen,
                root =>
                {
                    var screens = YamlNodeHelpers.Child(root, "Screens") as YamlMappingNode;
                    return screens is null
                        ? []
                        : screens.Children.Select(kv => (YamlNodeHelpers.Scalar(kv.Key), kv.Value));
                }),

            // Expects a/b to be the parent control (or screen) node that directly holds a
            // Children: sequence, per ruling 3's "scoped to the parent" rule -- this matcher
            // never looks outside the one parent it is handed.
            [DiffClassFamilies.CanvasControl] = new KeyedElementMatcher(
                DiffClassFamilies.CanvasControl,
                root =>
                {
                    if (YamlNodeHelpers.Child(root, "Children") is not YamlSequenceNode children)
                        return [];

                    var result = new List<(string? Key, YamlNode Element)>();
                    foreach (var item in children.Children)
                    {
                        if (item is YamlMappingNode single && single.Children.Count == 1)
                        {
                            var pair = single.Children.First();
                            result.Add((YamlNodeHelpers.Scalar(pair.Key), pair.Value));
                        }
                        else
                        {
                            result.Add((null, item));
                        }
                    }

                    return result;
                })
        };

        return matchers;
    }

    // --- Shared enumerate-delegate builders -----------------------------------------------------

    /// <summary>A family with exactly zero or one instance per document (Publisher, Entity, a SavedQuery, ...).</summary>
    private static Func<YamlNode, IEnumerable<(string? Key, YamlNode Element)>> Singleton(
        Func<YamlNode, YamlNode?> locate, Func<YamlNode, string?> keyOf) =>
        root =>
        {
            var element = locate(root);
            return element is null
                ? []
                : [(keyOf(element), element)];
        };

    /// <summary>A family that is a flat OneOrMany collection directly under the given path.</summary>
    private static Func<YamlNode, IEnumerable<(string? Key, YamlNode Element)>> Many(
        Func<YamlNode, YamlNode?> locateCollection, Func<YamlNode, string?> keyOf) =>
        root => YamlNodeHelpers.OneOrMany(locateCollection(root)).Select(e => (keyOf(e), e));

    // --- FormXml tree flattening, file root down to control ---------------------------------------
    // Each level flattens across every instance of the level above it. The real corpus (and every
    // fixture derived from it) has exactly one tab, one column, and one section, so this flattening
    // is provably correct there; it is written generally rather than hard-coded to "the first one"
    // so a future multi-tab/multi-section form (obligation O13) does not silently drop siblings.

    private static IEnumerable<YamlNode> FormXmlTabs(YamlNode root) =>
        YamlNodeHelpers.OneOrMany(YamlNodeHelpers.Descend(root, "systemform", "form", "tabs", "tab"));

    private static IEnumerable<YamlNode> FormXmlColumns(YamlNode root) =>
        FormXmlTabs(root).SelectMany(tab => YamlNodeHelpers.OneOrMany(YamlNodeHelpers.Descend(tab, "columns", "column")));

    private static IEnumerable<YamlNode> FormXmlSections(YamlNode root) =>
        FormXmlColumns(root).SelectMany(col => YamlNodeHelpers.OneOrMany(YamlNodeHelpers.Descend(col, "sections", "section")));

    private static IEnumerable<YamlNode> FormXmlRows(YamlNode root) =>
        FormXmlSections(root).SelectMany(sec => YamlNodeHelpers.OneOrMany(YamlNodeHelpers.Descend(sec, "rows", "row")));

    private static IEnumerable<YamlNode> FormXmlCells(YamlNode root) =>
        FormXmlRows(root)
            .Select(row => YamlNodeHelpers.Child(row, "cell"))
            .Where(cell => cell is not null)!;

    private static IEnumerable<YamlNode> FormXmlControls(YamlNode root) =>
        FormXmlCells(root)
            .Select(cell => YamlNodeHelpers.Child(cell, "control"))
            .Where(control => control is not null)!;

    private static IEnumerable<YamlNode> SiteMapAreas(YamlNode root) =>
        YamlNodeHelpers.OneOrMany(YamlNodeHelpers.Descend(root, "AppModuleSiteMap", "SiteMap", "Area"));
}
