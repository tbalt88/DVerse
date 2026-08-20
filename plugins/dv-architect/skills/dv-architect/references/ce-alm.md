# ALM Reference

> Corrected from the seed `ce-alm.md`. The seed assumed the legacy XML unpack
> format and a `dexx` example prefix; both are wrong for this repo. The `pac`
> command shapes and the managed/unmanaged distinction are unchanged and still
> the official behavior.

## Managed vs Unmanaged

- **Unmanaged:** open solution, components addable/removable/modifiable. Dev only.
- **Managed:** locked for customization unless `isCustomizable=true`. Test/Prod only.
- Cannot convert locally: import unmanaged to Dataverse, export as managed.

## Solution source format: YAML, not legacy XML [G1, G10]

DVerse solutions are the YAML source-control format, not the seed's `Entities/`
+ `Other/Customizations.xml` layout. This is forced, not a style choice: canvas
app sources are supported only in the YAML format. The manifest set:

```
<solution-root>/
├── solutions/<SolutionUniqueName>/
│     solution.yml · solutioncomponents.yml
│     rootcomponents.yml · missingdependencies.yml
├── publishers/<PublisherUniqueName>/publisher.yml
├── entities/<dv_entity>/attributes|FormXml|SavedQueries
├── entityrelationships/
├── canvasapps/<name>/Src/*.pa.yaml
├── appmodules/<name>/ · appmodulesitemaps/<name>/
├── pluginassemblies/ · plugins/<AssemblyProject>/
└── sdkmessageprocessingsteps/
```

Every manifest file (`solution.yml`, `solutioncomponents.yml`,
`rootcomponents.yml`, `missingdependencies.yml`, `publisher.yml`) belongs
under its `solutions/<name>/` or `publishers/<name>/` subdirectory, never at
the solution root [G10]. SolutionPackager's format auto-detection looks only
for `solutions/*/solution.yml`; a manifest left at the root is invisible to
it and the packer silently falls back to the legacy XML format, then fails
with a misleading missing-`Customizations.xml` error whose real cause is the
misplaced YAML manifest. Every `.yml`/`.yaml`/`.xml` file under the solution
root must also parse before any other gate runs [G1]; a syntax error is
caught here, not misreported as a domain violation further down the ladder.

`solution.yml`'s `ImportExportXml` element must carry `@generatedBy: CrmLive`
and a version attribute, or Dataverse rejects the import as an On-Premises
package with a blank version. `pac solution pack` accepts any `generatedBy`
value silently; only a real import enforces this [L3] (pack-vs-import gap:
this rule has no offline gate today, only the golden-import rung of the
verification ladder proves it).

## Publisher and schema prefix [G2]

`publishers/<name>/publisher.yml` must declare `CustomizationPrefix: dv`
exactly (not the seed's `dexx` placeholder), and every directory under
`entities/` must be named with the `dv_` prefix (prefix plus underscore).
These are two different strings checked independently by G2, not the same
value: the publisher prefix has no trailing underscore, the derived schema
prefix does.

## pac CLI — Primary ALM Interface

```bash
# Auth
pac auth create --url https://yourorg.crm.dynamics.com \
  --applicationId $PP_APP_ID --clientSecret $PP_CLIENT_SECRET \
  --tenant $PP_TENANT_ID --name dev-auth
pac auth list
pac org who

# Solution operations
pac solution export --name DVerseCore --path ./export/DVerseCore.zip --managed false
pac solution unpack --zipfile ./export/DVerseCore.zip \
  --folder ./demo-solution --packagetype Unmanaged
pac solution pack --zipfile ./dist/DVerseCore.zip \
  --folder ./demo-solution --packagetype Unmanaged
pac solution import --path ./dist/DVerseCore.zip \
  --activate-plugins true --force-overwrite true
pac solution check --path ./dist/DVerseCore.zip \
  --outputDirectory ./checker-output --geo UnitedStates --ruleSet "Solution Checker"
pac solution clone --name DVerseCore   # read-only; platform-mirror source of truth
```

`--activate-plugins` is not optional for any solution carrying plugin steps
[L17]: without it, import exits 0, publish reports clean, and every step
lands DISABLED. Nothing below a live runtime probe against the org detects
this. Every import of a step-bearing solution in this repo passes the flag,
and every import is followed by a post-import negative-input probe.

`pac solution check` needs a packed zip, not the unpacked YAML folder [G7];
pack first, then check. G7 refuses on any Critical or High severity finding
and requires a live tenant (skipped, not failed, without one).

## Pack acceptance is not import acceptance, is not component presence [L2, L3]

Three distinct silent-failure classes, each closed a different way:

1. **Pack exits 0 while a whole artifact class is silently dropped from the
   zip.** A component's on-disk path must be individually listed in
   `solutioncomponents.yml` (`'@path'` keys under a `SolutionComponents:
   Component:` mapping, NOT the flat `- Path: ...` sequence Microsoft's own
   docs show) [G9], and in `rootcomponents.yml` for any type G8 maps to a
   known path template [G8]. Both gates resolve every declared path against
   disk before a pack is trusted. `entityrelationships/` and FormXml
   subfolders both hit this class before G9/G8 existed.
2. **Pack acceptance is not import acceptance.** `generatedBy` and version
   (above), `DisplayMask` casing and the `PrimaryName` flag, and the full
   platform capability element set on new entities all pack clean and fail
   only at import. No offline gate covers this class yet; the golden-import
   rung of the verification ladder is the only thing that does.
3. **A non-empty `missingdependencies.yml` is a guaranteed future import
   failure**, not a warning: it names components this solution needs but does
   not ship. G3 refuses on any declared entry; the pac-verified empty state
   is `MissingDependencies: {}`, a mapping, never absent.

## Decompile before parsing, mirror before authoring [L4]

Microsoft's own documentation has contradicted Microsoft's own tooling
multiple times in this repo's history: the documented flat-sequence shape for
`solutioncomponents.yml` versus the real `SolutionComponents: Component:
'@path'` mapping SolutionPackagerLib.dll actually reads (G9), an obsolete
permission named in an app-registration tutorial, and undocumented pack-vs-
import gaps (above). Standing procedure: decompile `SolutionPackagerLib.dll`
(ilspycmd) before trusting a documented YAML shape, and platform-mirror
(author or register a component, clone it back via `pac solution clone`,
transcribe the real export) before trusting a doc-derived shape for anything
import-enforced. Never author a new component type's shape from documentation
alone.

## .gitignore (corrected from the seed)

```
bin/
obj/
*.dll
export/
dist/
*.user
.vs/
```

The seed's template also gitignored `*.zip` and, critically, `*.snk`. Neither
survives here: a solution zip built for a receipt may be worth keeping
deliberately (call it out per-file rather than blanket-ignoring), and the
strong-name key MUST be committed [L9, L15] — Sandbox isolation mode requires
a signed, public-key-tokened assembly, and gitignoring the key (not signing
itself) was the actual defect this repo hit and fixed. `DVerse.Plugins.snk`
is committed at `demo-solution/plugins/DVerse.Plugins/`.
