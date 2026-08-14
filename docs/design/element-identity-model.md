# Element identity model

Slice 7.1d. Design document, no code. Ground truth is the real artifact tree
under `demo-solution/**` in this worktree, plus one empirical test against the
live org (`pac solution clone`, read only, `dverse-ci` auth profile). Every
class below is quoted from a real committed instance, not invented. Where no
real instance exists (MissingDependencies), that is stated honestly instead
of guessed.

This document answers, for every declarative artifact class DVerse currently
gates: what keys an element's identity across two versions of the same
artifact, so a future diff engine reports added / removed / changed instead
of a positional false match.

## 1. The empirical clone-stability result (verbatim)

This is the single most valuable fact this survey produced, so it is given
first, in full, before the table.

Procedure: two independent `pac solution clone --name DVerseCore
--outputDirectory <dir>` runs against the live org (both READ operations,
`dverse-ci` universal auth profile, no import, no publish, no write),
several minutes apart, output diffed byte for byte.

```
Clone A: pac solution clone --name DVerseCore --outputDirectory
         C:\Users\dmdom\AppData\Local\Temp\dverse-identity-clone-a
         started 2026-08-14 07:00:18 UTC, finished 07:01:12 UTC, exit 0

Clone B: pac solution clone --name DVerseCore --outputDirectory
         C:\Users\dmdom\AppData\Local\Temp\dverse-identity-clone-b
         started 2026-08-14 07:07:32 UTC, finished 07:07:53 UTC, exit 0

Gap between the two reads: approximately 6 minutes 20 seconds.
```

File list comparison (`find` on both `DVerseCore/src` trees, sorted):
identical, zero differences.

Full recursive `diff -r` over the entire clone output, including the DLL and
every generated XML file:

```
diff -r <clone-a> <clone-b>
Only in difference: DVerseCore/DVerseCore.cdsproj
  Clone A: <ProjectGuid>cb94fe45-8b86-41f6-857d-4d20946e07b8</ProjectGuid>
  Clone B: <ProjectGuid>b6033298-b7af-4e41-b04f-3df2359391c0</ProjectGuid>
```

SHA-256 over every file in both trees (21 files total): 20 of 21 hashes
identical between clone A and clone B. The one mismatch is
`DVerseCore.cdsproj`, and only that one line inside it (`ProjectGuid`).

**Verdict: every Dataverse platform id in the exported tree is stable
across two independent reads of the same org state.** This includes:

- `formid` on the FormXml systemform (`{8d5fde37-9293-4b1e-97bb-2691167f3ee0}`),
  used verbatim as the exported file name in both clones.
- The tab id, section id, and all four cell ids inside that FormXml
  (`{58DD9B95-...}`, `{C949F461-...}`, `{040EF0AB-...}`, `{7FD3466F-...}`,
  `{A5583A68-...}`, `{A5136483-...}`), byte-identical in both clones.
- `savedqueryid` (`{4521f2c8-0e1b-4e51-b549-8161ccbe8979}`).
- `PluginAssemblyId` and the DLL bytes themselves (identical SHA-256).
- `SdkMessageProcessingStepId`.
- The AppModuleSiteMap's short platform-generated hex ids (`area_400223f7`,
  `group_0598b9c1`, `subarea_eb161827`).
- Every other element's full text content (labels, descriptions, metadata
  flags) in the entire tree.

**The one thing that is NOT stable is `DVerseCore.cdsproj`'s `ProjectGuid`.**
This file is not a Dataverse component at all; it is a local Visual
Studio/`pac` scaffolding project file that `pac solution clone` regenerates
fresh on every invocation as a client-side convenience artifact. It carries
no metadata id, is not part of `customizations.xml`, and is outside the
`demo-solution/**` layout this repo actually uses (that layout has no
`.cdsproj` file at all). It is excluded from the identity model below for
that reason, but its instability is recorded here so nobody mistakes it for
a counterexample to the stability finding: it is scaffolding noise, not a
platform-generated component id, and it is the ONLY unstable value found
anywhere in either clone.

Conclusion for the diff engine: platform-generated GUID and hex-token ids
(formid, tab/section/cell ids, savedqueryid, PluginAssemblyId,
SdkMessageProcessingStepId, AppModuleSiteMap Area/Group/SubArea ids) may be
trusted as stable identity keys across repeated reads of unchanged org
state. They are safe primary keys for a diff engine. This was tested only
across repeated READS with no org write between them; it does not by itself
prove ids survive an actual edit-and-republish cycle (see open question 1).

## 2. Method

Ground truth for content and shape: every file under `demo-solution/**`,
read directly and quoted below. Decompile citations reused in this
document (SolutionPackagerLib.dll, pac CLI 2.10.1, via ilspycmd 11.0.0.9375)
are the ones already recorded in the header comments of the corresponding
YAML files by earlier slices (4.2, 4.2b, 4.3, 4.4b, 4.4c, 4.6t); this slice
does not re-decompile, it cites those citations only where they settle an
identity question the artifact content alone cannot answer (for example,
that `EntityRelationship/@Name` is read as an XML attribute, not a child
element, which is exactly the shape that carries the relationship's
identity key).

Canvas control history: `git log` over
`demo-solution/canvasapps/MatterCanvas` shows four commits touching source
(`d760b00`, `1bdbc6b`, `df835b7`, `a7361fc`). Three of them add controls to
the `.pa.yaml` corpus (`d760b00` initial gallery, `1bdbc6b` the CRUD form
and buttons, `a7361fc` screen 2). The fourth (`df835b7`) only renames the
`.msapp` binary file on disk, not a control. `git diff` across all three
control-adding commits shows every change is a pure YAML-key addition (a
new `- Name:` entry) or a property change on an already-present control
(for example `Gallery1` gaining an `OnSelect` property in `a7361fc`); no
existing control's YAML key was ever renamed across this history. This
means the corpus cannot empirically confirm what Power Apps Studio does to
the source when a maker renames an existing control (see open question 2).

## 3. Class-by-class identity table

Every declarative artifact class present in `demo-solution/**`. "Stability
evidence" cites either the clone-stability test in section 1, direct
platform-immutability reasoning (Dataverse schema/logical names cannot be
changed after creation, only display names can), or "not testable from this
corpus" where the corpus has no real instance or no edit history to check
against.

| # | Class | Real instance quoted | Identity key | Match rule | Stability evidence |
|---|---|---|---|---|---|
| 1 | Solution manifest | `solutions/DVerseCore/solution.yml`, `SolutionManifest/UniqueName: DVerseCore` | `UniqueName` | exact string equality | platform-enforced immutable after creation |
| 2 | SolutionComponents entry | `solutioncomponents.yml`, `Component/@path` (9 entries, e.g. `entities/dv_matter/FormXml/main`) | `@path` string | exact string equality | not a Dataverse id at all; a pac packaging-time filter path, stable by construction (it is literally the folder path) |
| 3 | RootComponents entry | `rootcomponents.yml`, 5 `RootComponent` entries | composite: `@id` when present, else `(@type, @schemaName)` | if `@id` present match by `(@type, @id)`; else match by `(@type, @schemaName)` | type 91 carries both id and schemaName (canonical export, slice 4.4c); type 92 carries id only, no schemaName, confirmed as the platform's own canonical shape, not an authoring gap |
| 4 | MissingDependencies | `solutions/DVerseCore/missingdependencies.yml`: `MissingDependencies: {}` | undetermined | undetermined | **no real instance exists in this corpus; honest fallback, cannot derive a key from an empty element** |
| 5 | Publisher | `publishers/dversepublisher/publisher.yml`, `Publisher/UniqueName: dversepublisher` | `UniqueName` | exact string equality | platform-enforced immutable after creation |
| 6 | Entity | `entities/dv_matter/Entity.yml`, `Entity/Name: dv_Matter` and `EntityInfo/entity/@Name: dv_Matter` | schema name (`Name` / `EntityInfo/entity/@Name`) | case-insensitive string equality (SchemaName and LogicalName are the same identifier under two casing conventions; Dataverse always lowercases the LogicalName form) | platform-enforced immutable after creation; `IsRenameable: '1'` in the file governs the DISPLAY name only, never the schema name |
| 7 | Attribute | `attributes/dv_name.yml`, `dv_matternumber.yml`, `dv_openedon.yml`, `dv_matterid.yml`; each `attribute/LogicalName` | `LogicalName` | case-insensitive string equality, but see note below on the stricter FormXml binding rule | platform-enforced immutable after creation; `@PhysicalName` is the same name in PascalCase display casing, not a second identity |
| 8 | FormXml systemform | `entities/dv_matter/FormXml/main/dv_matter_main.yml`, `systemform/formid: 8d5fde37-9293-4b1e-97bb-2691167f3ee0` | `formid` (GUID) | exact GUID equality | **empirically confirmed stable, section 1** (also used as the exported file name) |
| 9 | FormXml tab | same file, `tab/@id: '{58DD9B95-BC51-4F57-BDFE-28A3853BEE52}'` | `@id` (GUID) | exact GUID equality | empirically confirmed stable, section 1 |
| 10 | FormXml column | same file, `column/@width: '100%'`, no id attribute anywhere in the real shape | **none** | n/a | **honest fallback: positional-with-warning** (ordinal position among sibling columns); the real committed form has exactly one column so this could not be stress-tested against a multi-column form, see open question 1 |
| 11 | FormXml section | same file, `section/@id: '{C949F461-933B-4528-896F-9D56D730B197}'` | `@id` (GUID) | exact GUID equality | empirically confirmed stable, section 1 |
| 12 | FormXml row | same file, `rows/row` is a YAML sequence of objects that each hold a single `cell`; the `row` node itself carries no id attribute anywhere | **none** | n/a | **honest fallback: positional-with-warning**, OR derive a proxy key from the row's own cell id(s) when there is exactly one cell per row (true in this corpus); the proxy breaks the moment a row holds zero or more than one cell independently reordered, so it must never be treated as a guaranteed key, only a best-effort fallback |
| 13 | FormXml cell | same file, four cells, e.g. `cell/@id: '{040EF0AB-C03F-4F37-9A4C-B87C47DA740E}'` | `@id` (GUID) | exact GUID equality | empirically confirmed stable, section 1 |
| 14 | FormXml control | same file, e.g. `control/@id: dv_name`, `@classid: '{4273EDBD-...}'`, `@datafieldname: dv_name` | `@id` (string, observed equal to the bound attribute's LogicalName for every control in this corpus) | exact string equality on `@id` | not independently GUID-stability-tested (control ids in this corpus are plain logical-name strings, not GUIDs, so the clone-diff byte-identity already covers them); **the corpus has only simple field-bound controls (textbox, datetime, lookup), so it cannot confirm whether other control kinds (subgrid, quick-view, web resource, iframe) also use a logical-name-style id or something else**, open question 3. `@classid` is the control's TYPE/renderer (a fixed platform enum value), a PROPERTY of the identified control, never an identity key itself: if `@classid` changes for the same `@id`, that is a changed(control) verdict, not delete+add |
| 15 | SavedQuery | `entities/dv_matter/SavedQueries/dv_matter_active.yml`, `savedquery/savedqueryid: 4521f2c8-0e1b-4e51-b549-8161ccbe8979` | `savedqueryid` (GUID) | exact GUID equality | empirically confirmed stable, section 1 |
| 16 | SavedQuery layoutxml column | same file, `layoutxml/grid/row/cell/@name` (e.g. `dv_name`, `createdon`) | `@name` (attribute LogicalName) | exact string equality, unique within the grid | derived from the Attribute class's own platform immutability, no separate id exists at this level |
| 17 | EntityRelationship | `entityrelationships/dv_matter_SharePointDocumentLocations.yml`, `EntityRelationship/@Name: dv_matter_SharePointDocumentLocations` | `@Name` (XML attribute, not a child element) | exact string equality | platform-enforced immutable after creation; decompile of `EntityRelationshipProcessor.CreateComponent` (`Helper.GetAttributeValue(element, "Name", throwIfNull: true)`) confirms Name is read as an attribute, settling that this is the identity-bearing field rather than any child element in the relationship body |
| 18 | AppModule | `appmodules/dv_MatterApp/appmodule.yml`, `AppModule/UniqueName: dv_MatterApp` | `UniqueName` | exact string equality | platform-enforced immutable after creation |
| 19 | AppModuleComponent | same file, `AppModuleComponents/AppModuleComponent`, e.g. `@type: '1', @schemaName: dv_matter` | composite `(@type, @schemaName)` | exact equality on both fields | schemaName side inherits the Entity/AppModule platform-immutability already covered above; decompile confirms `AppModuleProcessor.WriteToFiles` orders these children by `(type, schemaName)`, i.e. the platform itself treats that pair as the row's identity for serialization ordering |
| 20 | AppModuleRoleMaps Role | same file, `AppModuleRoleMaps/Role/@id`, e.g. `'{627090ff-40a3-4053-8790-584edc5be201}'` | `@id` (GUID, security role id) | exact GUID equality | not independently tested by this slice's clone-diff (AppModule.xml as a whole was byte-identical across both clones, which covers this field); **security role GUIDs are commonly environment-specific in Dataverse (a role created in one org has a different GUID in another org with "the same" role by name), a cross-environment portability risk distinct from the intra-environment stability this slice tested**, open question 6 |
| 21 | AppModuleSiteMap | `appmodulesitemaps/dv_MatterApp/appmodulesitemap.yml`, `AppModuleSiteMap/SiteMapUniqueName: dv_MatterApp` | `SiteMapUniqueName` | exact string equality | platform-enforced immutable after creation |
| 22 | SiteMap Area / Group / SubArea | same file, `Area/@Id: area_400223f7`, `Group/@Id: group_0598b9c1`, `SubArea/@Id: subarea_eb161827` | `@Id` (short platform-generated hex token, hierarchical: Area contains Group contains SubArea) | exact string equality per level, matched top-down (an Area match is required before its Groups are compared, etc.) | empirically confirmed stable, section 1 (whole file byte-identical across both clones) |
| 23 | PluginAssembly | `pluginassemblies/DVerse.Plugins.yml`, `PluginAssembly/@PluginAssemblyId: 5af6f42c-9a97-f111-b8de-70a8a59a66f9` | `@PluginAssemblyId` (GUID, XML attribute) | exact GUID equality | empirically confirmed stable, section 1 (DLL bytes and wrapper folder name both byte/hash identical across both clones); decompile confirms pac itself requires this attribute (`Helper.GetAttributeValue`, `throwIfNull: true`), settling that it, not `@FullName`, is the field pac treats as mandatory identity, though `@FullName` (assembly strong name) is also effectively immutable once registered and could serve as a secondary key |
| 24 | PluginType | same file, `PluginTypes/PluginType/@PluginTypeId: 0f998a3b-9a97-f111-b8de-70a8a59a66f9` | `@PluginTypeId` (GUID) | exact GUID equality | covered by the same whole-file byte-identity in section 1; `@AssemblyQualifiedName` is a secondary, human-readable identity signal (the full CLR type name) that moves in lockstep with the type's actual code identity and could serve as a fallback if the GUID were ever unavailable (for example comparing a not-yet-registered authored file against a live org export) |
| 25 | SdkMessageProcessingStep | `sdkmessageprocessingsteps/dv_matter_Create_MatterNumberValidator.yml`, `@SdkMessageProcessingStepId: '{10998a3b-9a97-f111-b8de-70a8a59a66f9}'` | `@SdkMessageProcessingStepId` (GUID, XML attribute) | exact GUID equality | empirically confirmed stable, section 1; decompile confirms this is the ONLY field pac itself reads (`throwIfNull: true`), everything else in the file (Stage, Mode, PrimaryEntity, Description, `@Name`) is opaque passthrough and therefore CHANGED-verdict material, never identity material |
| 26 | Canvas App screen | `canvasapps/MatterCanvas/Src/Screen1.pa.yaml`, `Screens/Screen1`; `Screen2.pa.yaml`, `Screens/Screen2` | YAML mapping key under `Screens:` | exact string equality | not GUID-backed at all; per ruling 3 a rename is structurally a delete-of-old-key plus add-of-new-key, matching the platform's own semantics for this format |
| 27 | Canvas App control (any depth) | e.g. `Screen1.pa.yaml`, `Gallery1`, `Form1`, `Matter Name_DataCard1`, `DataCardKey1`, `DataCardValue1`, nested arbitrarily deep under `Children:` | YAML mapping key at that nesting level | exact string equality, scoped to the immediate parent's `Children:` list (siblings only, not global) | not GUID-backed; **rename = delete + add is asserted by ruling 3 as true platform semantics, but this corpus's own commit history (section 2) contains zero observed renames to confirm it empirically**, open question 2 |
| 28 | Canvas DataCard child role (`MetadataKey`) | e.g. `Screen1.pa.yaml`, `DataCardValue1/MetadataKey: FieldValue`, `DataCardKey1/MetadataKey: FieldName` | `MetadataKey` value (e.g. `FieldName`, `FieldValue`, `ErrorMessage`, `FieldRequired`, `DateFieldValue`, `HourFieldValue`, `MinuteFieldValue`, `HourMinuteSeparator`) | exact string equality, but only unique WITHIN one DataCard's own `Children:` list, not globally across the screen | **not a primary identity key by itself** (every DataCard on the screen repeats the same small set of MetadataKey values); it is a candidate SECONDARY signal for re-linking a renamed child control within its own parent DataCard, not yet formalized in this model, open question 4 |

Note on row 7 (Attribute) and row 14 (FormXml control): identity matching
across two versions of the same solution should be case-insensitive
(SchemaName/PhysicalName vs LogicalName are the same identifier). This is
separate from, and must not be confused with, the stricter RUNTIME rule
already burned into `loop/LESSONS.md` entry 14: `datafieldname` on a
FormXml control binds by exact-case match to the attribute's lowercase
LogicalName, and any other casing drops the control silently at render.
That is a platform rendering rule, not an identity-matching rule; the diff
engine's identity comparison should normalize case, but a CHANGED verdict
on `datafieldname` casing itself is still meaningful content to report,
because that exact casing bug is the one lesson 14 documents.

## 4. Honest-fallback list (no stable key found)

These are the only classes in the entire surveyed corpus where no stable,
platform-guaranteed identity key exists. Per mission ruling 1, none of
these get silent positional treatment; each must carry an explicit warning
from the diff engine.

1. **FormXml column** (`form/tabs/tab/columns/column`). No id attribute of
   any kind in the real committed shape, only `@width`. Fallback: ordinal
   position among sibling columns, engine must emit a warning that column
   identity is positional and unverified beyond a single-column form.
2. **FormXml row** (`form/.../sections/section/rows/row`). No id attribute
   at all. Fallback: ordinal position, OR a derived proxy key from the
   row's own cell id when the row holds exactly one cell (true for every
   row in this corpus). The proxy is not a guarantee; multi-cell rows or
   reordered cells within a row break it. Engine must emit a warning either
   way.
3. **MissingDependencies**. Zero real instances anywhere in this corpus
   (`MissingDependencies: {}`). No identity key can be derived from an
   empty element. This class is entirely undetermined pending a populated
   real example.
4. **Canvas App control rename detection**. Not a missing key so much as
   an unverified one: the YAML key IS asserted as the identity key (ruling
   3), but this corpus has no recorded Studio rename to confirm the
   platform truly treats it as delete+add rather than, say, preserving some
   hidden internal control id across a Studio rename. Treated here as
   ratified-but-unverified, flagged again in open questions.
5. **Canvas DataCard MetadataKey** as a standalone key. Real, and stable in
   spirit, but not globally unique (repeats per DataCard), so it cannot
   serve alone; only usable as a locally-scoped secondary signal alongside
   the YAML key.

## 5. Diff verdict classes

Three verdicts, applied per identified element using the keys and match
rules in section 3, recursively from the artifact root down through every
nested level (systemform down to control; AppModule down to
AppModuleComponent; SiteMap down to SubArea; screen down to the deepest
nested canvas control):

- **added**: an element whose identity key exists in the target version but
  not in the baseline version, at that nesting level under its already-
  matched parent.
- **removed**: an element whose identity key exists in the baseline version
  but not in the target version, at that nesting level under its already-
  matched parent.
- **changed**: an element whose identity key exists in both versions
  (the same parent match, the same key), where the emitted verdict carries
  property-level detail: the specific scalar attributes or child elements
  that differ (for example, `changed(control id=dv_openedon):
  classid unchanged, datafieldname unchanged, label description changed
  from "Opened On" to "Date Opened"`), not merely "form changed" or
  "solution changed" as an undifferentiated blob.

A parent-level added/removed verdict short-circuits recursion into that
subtree (an added tab does not also need every one of its sections
individually reported as added; the tab-level added verdict covers it).
A parent-level changed or unchanged verdict always continues recursion,
because sibling content can change independently under an unchanged
parent, which is the entire reason positional diffing produces false
matches: two forms can have the same formid, the same tab id, the same
section id, and still differ in exactly one control's `datafieldname`,
which lesson 14 shows the platform accepts, packs, imports, and publishes
without complaint while silently dropping the field at render.

## 6. Open questions for the seat's ratification

1. The clone-stability test (section 1) proves ids are stable across
   repeated READS of unchanged org state. It does not prove ids survive an
   actual edit-and-republish cycle (add a field to the form in Studio,
   republish, re-clone, check whether the untouched cells keep their ids).
   Org writes are forbidden in this slice; should a future slice run that
   edit-and-republish check before the model is fully trusted for real
   diffs, or is the read-stability evidence sufficient to ratify now?
2. FormXml column and row have no id in the one real form this corpus
   contains, which happens to be single-tab, single-section, single-column.
   Should the seat require a platform-mirrored reference with multiple
   columns or multiple rows-per-cell before finalizing the positional-with-
   warning fallback, given the mission's own precedent (4.2b) of mirroring
   a real platform reference rather than guessing shape?
3. FormXml control `@id` was observed equal to the bound attribute's
   LogicalName for every control in this corpus (textbox, datetime,
   lookup). No subgrid, quick-view, web resource, iframe, or spacer control
   exists in the corpus. Does the seat want a reference form containing one
   of those control kinds before ratifying "control id equals bound
   attribute LogicalName" as a general rule versus a coincidence of this
   corpus's simple field-bound controls?
4. Canvas DataCard `MetadataKey` (section 3 row 28) is a real, observed,
   locally-scoped secondary signal. Should the identity model formalize it
   as a fallback correlator for renamed DataCard children, or leave it out
   of the matching algorithm entirely and treat any DataCard child rename
   as delete+add like every other canvas control?
5. Canvas control rename semantics (ruling 3's "rename = delete + add") is
   asserted as platform-true but has zero supporting evidence in this
   corpus's own commit history (section 2: three control-adding commits,
   zero renames). Is the ruling's own assertion sufficient authority to
   ratify, or does this need a live Studio rename-and-remirror experiment
   in a later slice, the same way the plugin registration shape needed a
   live platform-mirror before its identity model could be trusted
   (lesson 15)?
6. AppModuleRoleMaps `Role/@id` is a security role GUID. Role GUIDs are
   commonly environment-specific in Dataverse (the "same" named role has a
   different GUID in a different org). Should the identity model's match
   rule for this class be GUID equality only within one environment, with
   an explicit cross-environment fallback to matching by role name (not
   captured anywhere in this file, since only the GUID is present), or is
   cross-environment AppModule diffing out of scope entirely for the diff
   engine this model feeds?
7. SolutionComponents `@path` entries (section 3 row 2) are confirmed as a
   packaging-time filter, not a Dataverse component identity. Should the
   diff engine treat a `solutioncomponents.yml` change as its own distinct
   "packaging manifest changed" verdict class, separate from and never
   conflated with "component identity changed," so that (for example)
   removing a `@path` entry is reported as "component no longer packaged"
   rather than misread as "component deleted from the org"?
8. RootComponents' dual match rule (id-when-present, else
   type+schemaName, row 3) was derived from exactly two observed
   RootComponent shapes (type 91 has both, type 92 has id only). Does the
   seat want this generalized as the standing rule for every future
   component type added to `rootcomponents.yml`, or does each new type
   need its own platform-mirror confirmation before the diff engine trusts
   its RootComponent match rule, consistent with lesson 16's finding that
   the platform's own export can violate assumptions reasoned from the
   ComponentType enum alone?

## 7. Files read for this survey

Every file under `demo-solution/**` as of this slice's checkout: `Entity.yml`,
four `attributes/*.yml`, `FormXml/main/dv_matter_main.yml`,
`SavedQueries/dv_matter_active.yml`,
`entityrelationships/dv_matter_SharePointDocumentLocations.yml`,
`appmodules/dv_MatterApp/appmodule.yml`,
`appmodulesitemaps/dv_MatterApp/appmodulesitemap.yml`,
`canvasapps/MatterCanvas/Src/{App,Screen1,Screen2,_EditorState}.pa.yaml`,
`solutions/DVerseCore/{solution,solutioncomponents,rootcomponents,
missingdependencies}.yml`, `publishers/dversepublisher/publisher.yml`,
`pluginassemblies/DVerse.Plugins.yml`,
`sdkmessageprocessingsteps/dv_matter_Create_MatterNumberValidator.yml`, plus
`git log`/`git diff` over `demo-solution/canvasapps/MatterCanvas` for the
Studio-authoring history referenced in section 2. Plugin C# source
(`plugins/DVerse.Plugins/*.cs`, `.csproj`, `.snk`) is compiled source, not a
declarative solution artifact, and is out of this survey's scope.

## Seat ratification (2026-08-14)

The model above is RATIFIED with the following rulings on the open questions, by number:

1. Read-stability is adopted as the working assumption. Edit-and-republish stability is NOT yet proven; standing obligation O12: on the next real form edit, re-clone and verify id stability across the edit BEFORE trusting diff verdicts that span it. No dedicated org write just to test this now.
2. FormXml column/row positional-with-warning is ratified. Generalization to multi-column forms is obligation O13, to be closed by the first real multi-column form this project authors.
3. Control identity via bound-attribute-LogicalName is ratified for field-bound controls. Any control class not yet surveyed (subgrid, quick view, web resource, iframe) must produce an unknown-class WARNING verdict, never a silent positional match.
4. Canvas DataCard MetadataKey is NOT an identity key. It may appear in changed-verdict detail as advisory context only.
5. Canvas rename = delete + add is ratified as matching the platform's own semantics. Obligation O14: a live Studio rename-and-remirror experiment (seat, cheap) verifies before the canvas diff ships as trusted.
6. Wave 7 diffs are INTRA-environment only. AppModuleRoleMaps role GUIDs across environments are out of scope; a cross-env diff must emit an environment-specific-id warning, not a match and not a silent removal.
7. Ratified: packaging-manifest changes (solutioncomponents paths, and rootcomponents entries when the underlying component is unchanged) are their own verdict class, "packaging changed", distinct from component add/remove/change.
8. The dual RootComponents rule (id if present, else type+schemaName) is the DEFAULT for known types. Any component type's FIRST appearance in a diff input that this model has not surveyed triggers a per-type platform-mirror confirmation before its verdicts are trusted (lessons 15 and 16 precedent). The diff engine must surface "unsurveyed type" explicitly.

Obligations opened: O12 (edit-cycle id stability), O13 (multi-column form generalization), O14 (canvas rename experiment).
