# DVerse v2

Governed, pro-code agentic development for Microsoft Dataverse.

Part of the DVerse series. Successor to [DVerseClaudeSkills](https://github.com/tbalt88/DVerseClaudeSkills) (d365-architect-v3).

[![offline-gates](https://github.com/tbalt88/DVerse-v2/actions/workflows/gates-offline.yml/badge.svg)](https://github.com/tbalt88/DVerse-v2/actions/workflows/gates-offline.yml)
[![online-gates](https://github.com/tbalt88/DVerse-v2/actions/workflows/gates-online.yml/badge.svg)](https://github.com/tbalt88/DVerse-v2/actions/workflows/gates-online.yml)

> **Status: waves 4 and 5 closed, wave 6 in flight.** Ten gates run offline and online over a real, imported Dataverse solution, with 174 tests, a running model-driven app with an active plugin, live SharePoint document management, and a two-screen canvas app mirrored into gated source. Still private; the public flip is a later, owner-run wave.

## What this is

The Power Platform expresses its systems as declarative artifacts: Dataverse solution XML, FormXML, sitemap, ribbon, FetchXML, workflow XAML, and canvas app sources unpacked to `.pa.yaml`. Declarative means diffable. Diffable means gateable.

DVerse v2 tests whether that property can carry real governance: whether AI DLC gates can run natively against those artifacts and mechanically refuse ungated output, rather than governance being asserted by human review.

Everything here is built by Claude models. No other vendor's models are used.

Three components:

| Path | Component | State |
|---|---|---|
| `plugins/dv-architect/` | `dv-architect` skill, evolved from the seed `d365-architect`, every rule cross-referenced to a gate ID or a burned lesson, laid out to the Microsoft marketplace convention | evolved, wave 6A |
| `harness/` | Verification gates over declarative artifacts, refuses ungated output | ten gates live: G1 to G4, G6 to G11 |
| `demo-solution/` | Solution built BY the agent UNDER the harness, with receipts | dv_matter table, form, registered plugin, document-location relationship, app module, canvas app; five golden imports |

## Where this sits in the Microsoft ecosystem

DVerse layers on top of Microsoft's official tooling. It does not replace or vendor it.

[`microsoft/power-platform-skills`](https://github.com/microsoft/power-platform-skills) covers the app layer: canvas apps, model-driven apps, Power Pages, Power Automate, mobile, code apps, MCP apps. That territory is theirs and they ship it daily.

Their plugins help you **build**. DVerse **gates**. That is the distinction, and it holds across the whole surface: nothing in their marketplace mechanically refuses output that violates a rule, and nothing there produces an auditable refusal ledger.

DVerse covers two things their marketplace does not: **governance gates** over Power Platform declarative artifacts, and **Dataverse backend pro-code** (plugin assemblies, custom APIs, workflow activities), which has no plugin of theirs at all.

Scope note, stated honestly: DVerse gates canvas app sources as well as Dataverse solutions. That overlaps their `canvas-apps` territory on the build side. We are not competing on generation; we are adding the layer above it.

See [`docs/upstream-map.md`](docs/upstream-map.md) for exactly what is consumed, at what pinned version, and what was deliberately declined.

## Provenance: inherited vs built

Honesty about what is ours matters more here than anywhere, because this project's claim is auditability.

**Inherited (consumed, not written by us):**
- Power Apps Checker service, via `microsoft/powerplatform-actions`. The Solution Checker gate is Microsoft's, not ours. We compose it.
- `Microsoft.PowerPlatform.Dataverse.Client`, via NuGet. Note that Microsoft states this cannot be built outside Microsoft.
- Dataverse SDK core assemblies, via NuGet.
- `d365-architect` v3, the seed skill `dv-architect` was ported from, from the archived [`tbalt88/DVerseClaudeSkills`](https://github.com/tbalt88/DVerseClaudeSkills) repository.

**Built here:**
- Ten gates over declarative XML/YAML: `harness/DVerse.Harness/Gates/`.
- The refusal ledger and gate runner: `harness/DVerse.Harness/RefusalLedger.cs`, `Gate.cs`, `GateVerdict.cs`.
- The CLI entry point and exit-code contract (0 pass, 1 refusal, 2 CLI error): `harness/DVerse.Harness.Cli/`.
- Two CI tiers wiring the gates into GitHub Actions: `.github/workflows/gates-offline.yml`, `gates-online.yml`.
- The demo solution itself: `demo-solution/`, table, attributes, form, document-location relationship, plugin assembly.
- The evolved `dv-architect` skill: `plugins/dv-architect/`, every rule cross-referenced to a gate ID (G1-G4, G6-G11) or a burned lesson (`loop/LESSONS.md`).

## What this repo does not claim

The differentiator is narrow and specific: **nobody has shipped mechanical governance gates over Power Platform declarative artifacts.** Plenty of tooling helps you build. None of it refuses.

It is not a claim that nobody builds agentic Power Platform tooling. Microsoft does, publicly, with a funded team and a 572-star marketplace. Overstating that would not survive thirty seconds of scrutiny.

Two further limits worth stating before someone finds them:

- Canvas app gating depends on `pac canvas pack/unpack/validate`, which Microsoft currently marks **Preview**. That foundation may shift.
- The Power Apps Checker rung requires a live Dataverse environment and cannot run on fork pull requests. Every gate authored here runs offline with no credentials; that one does not.

## Engineering notes

Practices are marked by how they are enforced, because a written rule and a gate are not the same thing.

| Practice | Enforcement |
|---|---|
| Ten AI DLC gates over declarative artifacts (G1 to G4, G6 to G11) | code-enforced, `harness/DVerse.Harness/Gates/` |
| Every gate ships a fixture it refuses | code-enforced, `harness/fixtures/`, discovered from disk by the integration sweep |
| Refusal at generation and again in CI, one ledger | code-enforced, `GateRunner`, JSONL append-only |
| A gate that throws refuses (fail closed) | code-enforced, `GateRunner.EvaluateSafely` |
| No verdict carries an absolute path | code-enforced, leak-scan test, `WaveOneIntegrationTests` |
| Offline tier runs on every push and PR, including forks, zero credentials | code-enforced, `gates-offline.yml` |
| Online tier via OIDC federation, zero stored secrets, trusted branches only | code-enforced, `gates-online.yml` |
| Upstream deps pinned, never vendored | written rule |
| Honest provenance in docs | written rule |
| Public scrub before visibility flip | written rule, checklist in vault |

## The gate ladder

Ten gates live. G5 (plugin registration sanity, correlating registration YAML against plugin C# source) is deferred but now UNBLOCKED: wave 4.4 produced a real, canonical, platform-mirrored registration shape (`demo-solution/sdkmessageprocessingsteps/`), which is exactly the ground truth G5 was waiting for. It is backlog, not built.

| Gate | Rule | Failure mode it catches |
|---|---|---|
| G1 | artifacts parse and match schema | loud |
| G2 | `CustomizationPrefix: dv`, entity dirs `dv_` prefixed | loud |
| G3 | no dangling component references, `missingdependencies.yml` honest | loud at import |
| G4 | entity to `SharePointDocumentLocation` is 1:N | **silent**, flagship |
| G5 | plugin stage, mode, `FilteringAttributes` match code | deferred |
| G6 | plugins compile, xUnit suite green | loud |
| G7 | Power Apps Checker ruleset (Microsoft's, composed not authored) | loud, online only |
| G8 | `rootcomponents.yml` declarations have source on disk | silent, exit 0 |
| G9 | `solutioncomponents.yml` paths resolve | silent, exit 0 |
| G10 | manifests under `solutions/<name>/`, not root | misleading error |
| G11 | canvas `.pa.yaml` parses, controls declared, Properties are `=`-prefixed formulas | **silent**, controls drop at render |

Running the CLI against the live demo solution, this pass, offline:

```
dverse gate run  stage=integration  gates=9

PASS   G1   well-formedness                    demo-solution
PASS   G2   publisher-prefix                   demo-solution
PASS   G3   dependency-integrity               demo-solution/solutions/DVerseCore/missingdependencies.yml
PASS   G4   document-location-cardinality      demo-solution/entityrelationships
PASS   G6   build-and-tests                    demo-solution/plugins/DVerse.Plugins.Tests/DVerse.Plugins.Tests.csproj
PASS   G6   build-and-tests                    demo-solution/plugins/DVerse.Plugins/DVerse.Plugins.csproj
PASS   G8   rootcomponent-sources              demo-solution/solutions/DVerseCore/rootcomponents.yml
PASS   G9   solution-component-paths           demo-solution/solutions/DVerseCore/solutioncomponents.yml
PASS   G10  yaml-layout                        demo-solution/solutions
PASS   G11  canvas-yaml                        demo-solution/canvasapps

10 passed, 0 refused, 0 skipped.
```

(G7 needs a tenant connection and is not shown; it runs, and only runs, in the online CI tier.)

### The flagship: the refusal pair

Wave 4.3 produced the artifact this project exists to produce. Two runs of the same offline CLI over the same relationship, one relationship direction apart, 22 seconds apart:

- The correct solution: 8 gates, 8 PASS, exit 0.
- An inverted copy (same relationship, `ReferencingEntityName` and `ReferencedEntityName` swapped, never committed): 7 PASS, 1 REFUSE, exit 1.

Both pack clean with `pac solution pack` at exit 0. Dataverse's own documentation says the inverted direction "results in the app not listing the documents that exist in the SharePoint document library": no error, no import failure, no checker warning, just a Documents tab that opens empty. G4 catches it in milliseconds, offline, and names the exact symptom in the refusal reason. Full detail and verbatim ledger lines: [`docs/receipts/wave4-3-refusal-pair.md`](docs/receipts/wave4-3-refusal-pair.md).

## CI, two tiers

- **Offline** (`gates-offline.yml`): ubuntu-latest, zero credentials, runs on every push to main and every pull request including forks. Runs the full test suite and a CLI exit-code smoke test proving the 0/1/2 contract actually behaves that way in CI, not just locally.
- **Online** (`gates-online.yml`): windows-latest (net462 plugin projects need a .NET Framework runtime, which the Linux runner does not have), path-filtered to `demo-solution/**`, `harness/**`, and its own workflow file. Authenticates to the tenant via OIDC federation, no stored secret. Runs the full gate set including G7 against the real environment and uploads the ledger and checker output as artifacts.

## Five golden imports

Reality checked against a real Dataverse environment five times, not just against the packer:

| Version | What | Receipt |
|---|---|---|
| 0.1.0.0 | Shell: publisher and container, zero components, imported to `dexevo` | [`docs/receipts/wave2-solutions-list.png`](docs/receipts/wave2-solutions-list.png), [`wave2-solution-detail.png`](docs/receipts/wave2-solution-detail.png) |
| 0.2.0.0 | `dv_matter` table, attributes, main form, imported on the fifth attempt after mirroring a platform-authored reference; live FetchXML confirmed all three custom columns | [`docs/receipts/wave4-matter-imported.png`](docs/receipts/wave4-matter-imported.png) |
| 0.3.0.0 | Document-location relationship, imported first try after the refusal pair was proven offline | [`docs/receipts/wave4-3-refusal-pair.md`](docs/receipts/wave4-3-refusal-pair.md) |
| 0.4.0.0 | App module and sitemap in source; app still renders all four form fields after the overwrite | [`docs/receipts/wave4-6-form-fixed-all-fields.png`](docs/receipts/wave4-6-form-fixed-all-fields.png) |
| 0.5.0.0 | Declarative plugin registration (canonical shapes via platform-mirror); step ACTIVE only with `--activate-plugins`, proven by a negative-input probe that caught the disabled-step escape | [`docs/receipts/wave4-4-plugin-blocks-invalid-number.png`](docs/receipts/wave4-4-plugin-blocks-invalid-number.png) |

Beyond imports, runtime receipts: SharePoint document upload with the auto-created 1:N document location ([`wave5-3`](docs/receipts/wave5-3-documents-tab-live-upload.png)), and a two-screen canvas app driven through full CRUD against the same table ([`wave5-5`](docs/receipts/wave5-5-canvas-crud-created.png), [`wave5-2`](docs/receipts/wave5-2-canvas-screen2-detail.png)).

A model-driven Matter App renders `dv_matter` in the org today, and its main form renders all four authored fields (Matter Name, Matter Number, Opened On, Owner); a record was created and saved through the running app as the receipt ([`docs/receipts/wave4-6-form-fixed-all-fields.png`](docs/receipts/wave4-6-form-fixed-all-fields.png)). The root cause of the earlier Owner-only rendering was `datafieldname` casing in FormXml, another member of the silently-accepted class: see `loop/LESSONS.md` entry 14.

## Testing

**174 tests**, zero skips, re-run for this pass. The G6 parallel-execution flake previously reported here (lesson 13) was closed by slice O11 with per-test isolated fixture copies; five consecutive full-suite runs proved the fix and it has not recurred since.

Suite command: `dotnet test harness/DVerse.Harness.Tests/DVerse.Harness.Tests.csproj --nologo -v minimal`.

Every gate ships a fixture it refuses (`harness/fixtures/g*/refuse-*`), discovered from disk by the integration sweep rather than hand-enumerated, so a new red fixture is covered automatically and a deleted one shows up as a drop in the executed count.

## Known limits

Stated here rather than discovered by a reader.

- **Canvas gating rests on Preview tooling.** `pac canvas pack` and `unpack` are marked Preview by Microsoft, and `pac canvas validate` is REMOVED in pac 2.10.1 while its own help text still documents it (verified live, wave 5). G11 validates `.pa.yaml` itself for that reason, and canvas gating is kept in its own module so churn stays contained.
- **The Power Apps Checker gate (G7) cannot run on fork pull requests.** It needs a live, OIDC-authenticated tenant connection, which GitHub does not issue to forks. Every other gate is credential-free and reproducible by a stranger; this one composes Microsoft's own service and inherits its constraint.
- **The rendered UI remains a verification rung nothing mechanical covers yet.** The live example: the Matter form imported green through every gate while three of its four controls silently dropped at render because their `datafieldname` values were PascalCase instead of the attribute's lowercase logical name (fixed; `loop/LESSONS.md` entries 8 and 14). No gate yet validates datafieldname casing against entity attribute logical names; today that guard is spec-only.
- **`pac` acceptance is not import acceptance, and import acceptance is not pack completeness.** Three separate silent-failure classes have been found and fixed this way: `generatedBy` and `DisplayMask` shapes that pack cleanly but fail import (wave 2), and `solutioncomponents.yml` entries missing for a whole artifact folder (`entityrelationships/`) that pack cleanly to exit 0 with the artifact simply absent from the zip (wave 4.3). G8 and G9 close the second class for the shapes they cover; nothing mechanically closes the first, which is why the golden imports above exist as a rung of their own.
- **Microsoft's own documentation has contradicted Microsoft's own tooling multiple times**: the documented `solutioncomponents.yml` shape versus the real one SolutionPackagerLib reads, an obsolete permission named in the app-registration tutorial, and (per the note above) undocumented pack-vs-import gaps. Standing procedure in response: decompile before parsing, and mirror a platform-authored reference before trusting a doc-derived shape. See `loop/LESSONS.md` entries 2 to 4.
- **G5 (plugin registration sanity) is not built.** Its shape was unobservable until wave 4.4 produced a real plugin; building it from documentation alone was judged the same mistake that produced the G9 documented-versus-real contradiction, so it waits for grounding rather than shipping guessed.

## For hiring managers

This repo argues that agentic development on enterprise platforms can be governed structurally rather than procedurally, using a property specific to Dataverse: its artifacts are declarative. It is built pro-code, by an agent, under gates that refuse ungated output. The receipts are the point, so the machinery is public and the gate logs are real.
