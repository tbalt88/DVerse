using YamlDotNet.RepresentationModel;

namespace DVerse.Harness.Diff.Internal;

/// <summary>
/// One family in a walk. Bundles the family name, the matcher that pairs
/// this family's elements between two versions (root-scoped to the PARENT
/// element the walker just matched, per <see cref="IElementMatcher"/>'s own
/// doc comment -- "the direct parent node whose children are the family"),
/// which top-level property keys belong to a child family and must be
/// excluded from this element's own scalar comparison, whether this
/// family's match is one of the model's positional honest fallbacks
/// (FormXml column/row) so warnings propagate per mission ruling 5, an
/// optional identity-key rule for describing an Added/Removed orphan that
/// never had a matched counterpart, and the child families to recurse into
/// for every matched pair (mission ruling 3: a Matched parent always
/// recurses).
/// </summary>
internal sealed class ChainStep
{
    public required string Family { get; init; }
    public required IElementMatcher Matcher { get; init; }
    public IReadOnlySet<string> ExcludeKeys { get; init; } = new HashSet<string>();
    public bool IsPositional { get; init; }
    public Func<YamlNode, string?>? KeyOf { get; init; }

    // Settable, not init-only: the canvas control family recurses into itself (a control's own
    // "Children" holds more controls, arbitrarily deep per the ratified model row 27), which needs
    // a self-reference assigned after construction.
    public IReadOnlyList<ChainStep> Children { get; set; } = [];

    /// <summary>Best-effort identity text for an Added/Removed element this family's own key rule could not name (or that never carried a key at all, per the model's honest fallbacks).</summary>
    public string DescribeOrphan(YamlNode node) => KeyOf?.Invoke(node) ?? ChainDescribe.Preview(node);
}

/// <summary>Fallback human-readable preview for an element with no dedicated key rule (the model's honest-fallback families, and canvas nodes whose wrapper key was already consumed during matching -- see CanvasMatchersTests' own note on this same limitation in 7.1i).</summary>
internal static class ChainDescribe
{
    public static string Preview(YamlNode node)
    {
        if (node is YamlScalarNode scalar)
            return scalar.Value ?? "(empty)";

        if (node is not YamlMappingNode mapping)
            return "(unnamed)";

        var parts = new List<string>();
        foreach (var child in mapping.Children)
        {
            if (parts.Count >= 2)
                break;

            if (child.Key is not YamlScalarNode keyNode)
                continue;

            if (child.Value is YamlScalarNode valueNode)
                parts.Add($"{keyNode.Value}={valueNode.Value}");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "(unnamed)";
    }
}

/// <summary>
/// Wires every ratified family chain the mission ships (ruling 6) into a
/// <see cref="ChainStep"/> tree, one entry per artifact class
/// <see cref="ArtifactDiffer.Diff"/> accepts. This is the "family-chain walk
/// table" the mission's Done means asks for, read top to bottom:
/// <list type="bullet">
/// <item>solution manifests: SolutionManifest, SolutionComponentsEntry,
/// RootComponentsEntry, MissingDependencies, and Publisher are each their
/// own leaf chain (they live in separate files in the real tree -- solution.yml,
/// solutioncomponents.yml, rootcomponents.yml, missingdependencies.yml,
/// publisher.yml -- so there is no single shared node to recurse through in
/// one <see cref="YamlNode"/> pair; each is diffed independently, exactly
/// the pattern already established for every other single-file family
/// below).</item>
/// <item>entity (attributes): Entity and Attribute are likewise separate
/// leaf chains for the same reason (Entity.yml vs attributes/*.yml are
/// different files).</item>
/// <item>FormXml full chain: FormXmlSystemForm -> FormXmlTab -> FormXmlColumn
/// (positional) -> FormXmlSection -> FormXmlRow (positional) -> FormXmlCell
/// -> FormXmlControl, all nested inside one file, walked with true
/// parent-scoped recursion.</item>
/// <item>savedqueries: SavedQuery -> SavedQueryLayoutColumn.</item>
/// <item>relationships: EntityRelationship, a leaf.</item>
/// <item>appmodule chain: AppModule -> {AppModuleComponent, AppModuleRoleMapRole}
/// (two independent child families at the same level).</item>
/// <item>sitemap chain: AppModuleSiteMap -> SiteMapArea -> SiteMapGroup -> SiteMapSubArea.</item>
/// <item>plugin chain: PluginAssembly -> PluginType, plus SdkMessageProcessingStep
/// as its own leaf (registered as its own file in the real tree).</item>
/// <item>canvas chain: CanvasScreen -> CanvasControl, CanvasControl recursing
/// into itself for arbitrarily deep nested Children (model row 27).</item>
/// </list>
/// <para>
/// Every nested-collection matcher here is built fresh, root-scoped to the
/// parent element the walker just matched, rather than reused directly from
/// <see cref="MatcherRegistry"/>'s own registrations for the same family
/// names: 7.1i's registered FormXml/AppModule/SiteMap/PluginType matchers
/// are, empirically (see their own tests), called with the FULL FILE ROOT
/// every time, flattening the entire nested path themselves in one call --
/// the shape 7.1i's own test suite exercises them with. That full-root
/// shape cannot be reused for genuine per-parent recursion (a matched
/// tab pair's own node is not a file root; descending "systemform" from it
/// would fail). This file therefore builds new matcher instances from the
/// SAME public engine classes (<see cref="KeyedElementMatcher"/>,
/// <see cref="PositionalWarningMatcher"/>) 7.1i already exposes, configured
/// with a parent-scoped locate function instead, keeping the identical key
/// rule and warning behaviour without touching 7.1i's own registrations or
/// tests. The two exceptions are CanvasScreen and CanvasControl, whose
/// registry matchers were already written parent-scoped (matching
/// <see cref="IElementMatcher"/>'s own doc comment), so those are reused
/// directly, including for CanvasControl's self-recursion.
/// </para>
/// </summary>
internal static class ChainBuilder
{
    public static IReadOnlyDictionary<string, ChainStep> BuildChains(MatcherRegistry registry)
    {
        var chains = new Dictionary<string, ChainStep>();

        AddSolutionLevelChains(chains, registry);
        AddEntityChains(chains, registry);
        AddFormXmlChain(chains, registry);
        AddSavedQueryChain(chains, registry);
        AddEntityRelationshipChain(chains, registry);
        AddAppModuleChain(chains, registry);
        AddSiteMapChain(chains, registry);
        AddPluginChain(chains, registry);
        AddCanvasChain(chains, registry);

        return chains;
    }

    // --- solution manifests: five independent leaf chains, one per real file -----------------------

    private static void AddSolutionLevelChains(Dictionary<string, ChainStep> chains, MatcherRegistry registry)
    {
        chains[DiffClassFamilies.SolutionManifest] = new ChainStep
        {
            Family = DiffClassFamilies.SolutionManifest,
            Matcher = registry.Get(DiffClassFamilies.SolutionManifest),
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "UniqueName")
        };

        chains[DiffClassFamilies.SolutionComponentsEntry] = new ChainStep
        {
            Family = DiffClassFamilies.SolutionComponentsEntry,
            Matcher = registry.Get(DiffClassFamilies.SolutionComponentsEntry),
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "@path")
        };

        chains[DiffClassFamilies.RootComponentsEntry] = new ChainStep
        {
            Family = DiffClassFamilies.RootComponentsEntry,
            Matcher = registry.Get(DiffClassFamilies.RootComponentsEntry),
            KeyOf = e =>
            {
                var type = YamlNodeHelpers.ScalarChild(e, "@type") ?? "?";
                var identifier = YamlNodeHelpers.ScalarChild(e, "@id") ?? YamlNodeHelpers.ScalarChild(e, "@schemaName");
                return $"type={type}, {identifier ?? "(unknown)"}";
            }
        };

        // MissingDependencies gets no KeyOf: it never produces Added/Removed on its own (the
        // UnsurveyedFamilyMatcher's only two outcomes are MatchResult.Empty or warnings-only), so
        // the walk's generic "unsurveyed content present" synthesis (ruling 6) covers it without one.
        chains[DiffClassFamilies.MissingDependencies] = new ChainStep
        {
            Family = DiffClassFamilies.MissingDependencies,
            Matcher = registry.Get(DiffClassFamilies.MissingDependencies)
        };

        chains[DiffClassFamilies.Publisher] = new ChainStep
        {
            Family = DiffClassFamilies.Publisher,
            Matcher = registry.Get(DiffClassFamilies.Publisher),
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "UniqueName")
        };
    }

    // --- entity (attributes): two independent leaf chains ------------------------------------------

    private static void AddEntityChains(Dictionary<string, ChainStep> chains, MatcherRegistry registry)
    {
        chains[DiffClassFamilies.Entity] = new ChainStep
        {
            Family = DiffClassFamilies.Entity,
            Matcher = registry.Get(DiffClassFamilies.Entity),
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "Name")
        };

        chains[DiffClassFamilies.Attribute] = new ChainStep
        {
            Family = DiffClassFamilies.Attribute,
            Matcher = registry.Get(DiffClassFamilies.Attribute),
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "LogicalName")
        };
    }

    // --- FormXml full chain: form -> tab -> column (positional) -> section -> row (positional)
    //     -> cell -> control -----------------------------------------------------------------------

    private static void AddFormXmlChain(Dictionary<string, ChainStep> chains, MatcherRegistry registry)
    {
        var control = new ChainStep
        {
            Family = DiffClassFamilies.FormXmlControl,
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "@id"),
            Matcher = new KeyedElementMatcher(
                DiffClassFamilies.FormXmlControl,
                cell => YamlNodeHelpers.OneOrMany(YamlNodeHelpers.Child(cell, "control"))
                    .Select(c => (YamlNodeHelpers.ScalarChild(c, "@id"), c)),
                warnOnMatch: (_, targetControl) =>
                {
                    var classId = YamlNodeHelpers.ScalarChild(targetControl, "@classid");
                    if (classId is not null && MatcherRegistry.SurveyedControlClassIds.Contains(classId))
                        return null;

                    return $"{DiffClassFamilies.FormXmlControl}: control class '{classId ?? "(none)"}' " +
                           "is not one of the three surveyed classids (textbox, datetime, lookup; " +
                           "docs/design/element-identity-model.md section 3 row 14, seat ruling 3). " +
                           "Matched by '@id' as usual, but this control class is unsurveyed: never " +
                           "trust the match silently.";
                })
        };

        var cell = new ChainStep
        {
            Family = DiffClassFamilies.FormXmlCell,
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "@id"),
            ExcludeKeys = new HashSet<string> { "control" },
            Matcher = new KeyedElementMatcher(
                DiffClassFamilies.FormXmlCell,
                row => YamlNodeHelpers.OneOrMany(YamlNodeHelpers.Child(row, "cell"))
                    .Select(c => (YamlNodeHelpers.ScalarChild(c, "@id"), c))),
            Children = [control]
        };

        var row = new ChainStep
        {
            Family = DiffClassFamilies.FormXmlRow,
            IsPositional = true,
            ExcludeKeys = new HashSet<string> { "cell" },
            KeyOf = e => YamlNodeHelpers.ScalarChild(YamlNodeHelpers.Child(e, "cell"), "@id"),
            Matcher = new PositionalWarningMatcher(
                DiffClassFamilies.FormXmlRow,
                section => YamlNodeHelpers.OneOrMany(YamlNodeHelpers.Descend(section, "rows", "row")),
                (node, i) => $"{DiffClassFamilies.FormXmlRow}[#{i}, cell.id={YamlNodeHelpers.ScalarChild(YamlNodeHelpers.Child(node, "cell"), "@id") ?? "?"}]"),
            Children = [cell]
        };

        var section = new ChainStep
        {
            Family = DiffClassFamilies.FormXmlSection,
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "@id"),
            ExcludeKeys = new HashSet<string> { "rows" },
            Matcher = new KeyedElementMatcher(
                DiffClassFamilies.FormXmlSection,
                column => YamlNodeHelpers.OneOrMany(YamlNodeHelpers.Descend(column, "sections", "section"))
                    .Select(s => (YamlNodeHelpers.ScalarChild(s, "@id"), s))),
            Children = [row]
        };

        var column = new ChainStep
        {
            Family = DiffClassFamilies.FormXmlColumn,
            IsPositional = true,
            ExcludeKeys = new HashSet<string> { "sections" },
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "@width"),
            Matcher = new PositionalWarningMatcher(
                DiffClassFamilies.FormXmlColumn,
                tab => YamlNodeHelpers.OneOrMany(YamlNodeHelpers.Descend(tab, "columns", "column")),
                (node, i) => $"{DiffClassFamilies.FormXmlColumn}[#{i}, width={YamlNodeHelpers.ScalarChild(node, "@width") ?? "?"}]"),
            Children = [section]
        };

        var tab = new ChainStep
        {
            Family = DiffClassFamilies.FormXmlTab,
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "@id"),
            ExcludeKeys = new HashSet<string> { "columns" },
            Matcher = new KeyedElementMatcher(
                DiffClassFamilies.FormXmlTab,
                systemform => YamlNodeHelpers.OneOrMany(YamlNodeHelpers.Descend(systemform, "form", "tabs", "tab"))
                    .Select(t => (YamlNodeHelpers.ScalarChild(t, "@id"), t))),
            Children = [column]
        };

        chains[DiffClassFamilies.FormXmlSystemForm] = new ChainStep
        {
            Family = DiffClassFamilies.FormXmlSystemForm,
            Matcher = registry.Get(DiffClassFamilies.FormXmlSystemForm),
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "formid"),
            ExcludeKeys = new HashSet<string> { "form" },
            Children = [tab]
        };
    }

    // --- savedqueries: SavedQuery -> SavedQueryLayoutColumn -----------------------------------------

    private static void AddSavedQueryChain(Dictionary<string, ChainStep> chains, MatcherRegistry registry)
    {
        var layoutColumn = new ChainStep
        {
            Family = DiffClassFamilies.SavedQueryLayoutColumn,
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "@name"),
            Matcher = new KeyedElementMatcher(
                DiffClassFamilies.SavedQueryLayoutColumn,
                savedquery => YamlNodeHelpers.OneOrMany(YamlNodeHelpers.Descend(savedquery, "layoutxml", "grid", "row", "cell"))
                    .Select(c => (YamlNodeHelpers.ScalarChild(c, "@name"), c)))
        };

        chains[DiffClassFamilies.SavedQuery] = new ChainStep
        {
            Family = DiffClassFamilies.SavedQuery,
            Matcher = registry.Get(DiffClassFamilies.SavedQuery),
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "savedqueryid"),
            // The layoutxml wrapper's own grid/row-level attributes (grid/@jump, row/@name, ...) are
            // not independently diffed: the ratified model defines identity/recursion only for the
            // cell rows beneath them (row 16), not for the grid wrapper itself, so this exclusion is
            // whole-subtree, not path-precise. Documented as an assumption in the slice report.
            ExcludeKeys = new HashSet<string> { "layoutxml" },
            Children = [layoutColumn]
        };
    }

    // --- relationships: EntityRelationship, a leaf --------------------------------------------------

    private static void AddEntityRelationshipChain(Dictionary<string, ChainStep> chains, MatcherRegistry registry)
    {
        chains[DiffClassFamilies.EntityRelationship] = new ChainStep
        {
            Family = DiffClassFamilies.EntityRelationship,
            Matcher = registry.Get(DiffClassFamilies.EntityRelationship),
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "@Name")
        };
    }

    // --- appmodule chain: AppModule -> {AppModuleComponent, AppModuleRoleMapRole} -------------------

    private static void AddAppModuleChain(Dictionary<string, ChainStep> chains, MatcherRegistry registry)
    {
        var component = new ChainStep
        {
            Family = DiffClassFamilies.AppModuleComponent,
            KeyOf = ComponentKey,
            Matcher = new KeyedElementMatcher(
                DiffClassFamilies.AppModuleComponent,
                appModule => YamlNodeHelpers.OneOrMany(YamlNodeHelpers.Descend(appModule, "AppModuleComponents", "AppModuleComponent"))
                    .Select(e => (ComponentKey(e), e)))
        };

        var roleMapRole = new ChainStep
        {
            Family = DiffClassFamilies.AppModuleRoleMapRole,
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "@id"),
            Matcher = new KeyedElementMatcher(
                DiffClassFamilies.AppModuleRoleMapRole,
                appModule => YamlNodeHelpers.OneOrMany(YamlNodeHelpers.Descend(appModule, "AppModuleRoleMaps", "Role"))
                    .Select(r => (YamlNodeHelpers.ScalarChild(r, "@id"), r)))
        };

        chains[DiffClassFamilies.AppModule] = new ChainStep
        {
            Family = DiffClassFamilies.AppModule,
            Matcher = registry.Get(DiffClassFamilies.AppModule),
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "UniqueName"),
            ExcludeKeys = new HashSet<string> { "AppModuleComponents", "AppModuleRoleMaps" },
            Children = [component, roleMapRole]
        };

        return;

        static string? ComponentKey(YamlNode e)
        {
            var type = YamlNodeHelpers.ScalarChild(e, "@type");
            var schemaName = YamlNodeHelpers.ScalarChild(e, "@schemaName");
            return type is null || schemaName is null ? null : $"{type}|{schemaName.ToLowerInvariant()}";
        }
    }

    // --- sitemap chain: AppModuleSiteMap -> SiteMapArea -> SiteMapGroup -> SiteMapSubArea -----------

    private static void AddSiteMapChain(Dictionary<string, ChainStep> chains, MatcherRegistry registry)
    {
        var subArea = new ChainStep
        {
            Family = DiffClassFamilies.SiteMapSubArea,
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "@Id"),
            Matcher = new KeyedElementMatcher(
                DiffClassFamilies.SiteMapSubArea,
                group => YamlNodeHelpers.OneOrMany(YamlNodeHelpers.Child(group, "SubArea"))
                    .Select(sa => (YamlNodeHelpers.ScalarChild(sa, "@Id"), sa)))
        };

        var group = new ChainStep
        {
            Family = DiffClassFamilies.SiteMapGroup,
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "@Id"),
            ExcludeKeys = new HashSet<string> { "SubArea" },
            Matcher = new KeyedElementMatcher(
                DiffClassFamilies.SiteMapGroup,
                area => YamlNodeHelpers.OneOrMany(YamlNodeHelpers.Child(area, "Group"))
                    .Select(g => (YamlNodeHelpers.ScalarChild(g, "@Id"), g))),
            Children = [subArea]
        };

        var area = new ChainStep
        {
            Family = DiffClassFamilies.SiteMapArea,
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "@Id"),
            ExcludeKeys = new HashSet<string> { "Group" },
            Matcher = new KeyedElementMatcher(
                DiffClassFamilies.SiteMapArea,
                appModuleSiteMap => YamlNodeHelpers.OneOrMany(YamlNodeHelpers.Descend(appModuleSiteMap, "SiteMap", "Area"))
                    .Select(ar => (YamlNodeHelpers.ScalarChild(ar, "@Id"), ar))),
            Children = [group]
        };

        chains[DiffClassFamilies.AppModuleSiteMap] = new ChainStep
        {
            Family = DiffClassFamilies.AppModuleSiteMap,
            Matcher = registry.Get(DiffClassFamilies.AppModuleSiteMap),
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "SiteMapUniqueName"),
            // Same whole-subtree-exclusion tradeoff as SavedQuery/layoutxml above: SiteMap's own
            // '@IntroducedVersion' attribute is not independently diffed at the AppModuleSiteMap
            // level, since the model defines no distinct family for the SiteMap wrapper itself,
            // only for the Area/Group/SubArea children beneath it.
            ExcludeKeys = new HashSet<string> { "SiteMap" },
            Children = [area]
        };
    }

    // --- plugin chain: PluginAssembly -> PluginType, plus SdkMessageProcessingStep as its own leaf --

    private static void AddPluginChain(Dictionary<string, ChainStep> chains, MatcherRegistry registry)
    {
        var pluginType = new ChainStep
        {
            Family = DiffClassFamilies.PluginType,
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "@PluginTypeId"),
            Matcher = new KeyedElementMatcher(
                DiffClassFamilies.PluginType,
                pluginAssembly => YamlNodeHelpers.OneOrMany(YamlNodeHelpers.Descend(pluginAssembly, "PluginTypes", "PluginType"))
                    .Select(pt => (YamlNodeHelpers.ScalarChild(pt, "@PluginTypeId"), pt)))
        };

        chains[DiffClassFamilies.PluginAssembly] = new ChainStep
        {
            Family = DiffClassFamilies.PluginAssembly,
            Matcher = registry.Get(DiffClassFamilies.PluginAssembly),
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "@PluginAssemblyId"),
            ExcludeKeys = new HashSet<string> { "PluginTypes" },
            Children = [pluginType]
        };

        chains[DiffClassFamilies.SdkMessageProcessingStep] = new ChainStep
        {
            Family = DiffClassFamilies.SdkMessageProcessingStep,
            Matcher = registry.Get(DiffClassFamilies.SdkMessageProcessingStep),
            KeyOf = e => YamlNodeHelpers.ScalarChild(e, "@SdkMessageProcessingStepId")
        };
    }

    // --- canvas chain: CanvasScreen -> CanvasControl, CanvasControl recursing into itself ------------

    private static void AddCanvasChain(Dictionary<string, ChainStep> chains, MatcherRegistry registry)
    {
        // Reused directly from the registry, unlike every nested FormXml/AppModule/SiteMap/PluginType
        // step above: the registry's own CanvasControl matcher already expects "the parent control
        // (or screen) node that directly holds a Children: sequence" (its own doc comment), which is
        // exactly the parent-scoped shape recursion needs, at any depth, including recursing into
        // itself. No new matcher instance required here.
        var canvasControl = new ChainStep
        {
            Family = DiffClassFamilies.CanvasControl,
            Matcher = registry.Get(DiffClassFamilies.CanvasControl),
            ExcludeKeys = new HashSet<string> { "Children" }
            // No KeyOf: the registry's CanvasControl matcher already consumes the wrapper key
            // ({ButtonName: {...}} -> the body alone) during enumeration, so an Added/Removed node
            // here never carries its own name any more -- the same limitation 7.1i's own
            // CanvasMatchersTests documents and accepts, identifying such nodes by content instead.
            // ChainDescribe.Preview covers this the same way.
        };
        canvasControl.Children = [canvasControl]; // self-recursion: a control's own Children may nest more controls, arbitrarily deep (model row 27).

        chains[DiffClassFamilies.CanvasScreen] = new ChainStep
        {
            Family = DiffClassFamilies.CanvasScreen,
            Matcher = registry.Get(DiffClassFamilies.CanvasScreen),
            ExcludeKeys = new HashSet<string> { "Children" },
            Children = [canvasControl]
        };
    }
}
