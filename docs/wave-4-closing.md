# Wave 4 closing

Closed 2026-08-14. The tenant wave: everything before it ran against fixtures; this wave put the harness against a live Dataverse org and made the org prove every claim. Six slice specs persisted before spawn, six Sonnet executors in isolated worktrees, seat-only org writes throughout, five golden imports, two CI tiers green.

## Delivered

| Slice | What | Commit(s) | Receipt |
|---|---|---|---|
| 4.1 | OIDC federation, zero secrets: online CI authenticates via GitHub immutable subject, G7 Power Apps Checker live | `e0786b2` and priors | green `online-gates` runs on main |
| 4.2 | dv_matter table + attributes + main form, first real import (0.2.0.0) after the five-attempt platform-mirror saga | golden import #2 | `docs/receipts/wave4-matter-imported.png` |
| 4.3 | Document-location relationship + THE refusal pair: G4 passes the 1:N source and refuses the inverted N:1, pack exits 0 on both | golden import #3 | `docs/receipts/wave4-3-refusal-pair.md` |
| 4.4a | MatterNumberValidator plugin (net462, sibling tests, G6-gated) | merged pre-close | suite counts |
| 4.4b/c | Declarative plugin registration: PluginAssembly + SdkMessageProcessingStep authored in YAML source, canonical shapes via platform-mirror, G8 id-only fix | `8d2d246`, `f203540`, `1078fcf` | `docs/receipts/wave4-4-plugin-blocks-invalid-number.png` |
| 4.6 | Model-driven Matter App in source (appmodule + sitemap) and rendering all four form fields after the datafieldname-casing fix | `0fa633d`, `b4e9e3b` | `docs/receipts/wave4-6-form-fixed-all-fields.png` |
| O11 | G6 parallel-execution flake killed by per-test fixture isolation, five consecutive green suites | `439bca7` | lesson 13 closure |
| README | Truth pass: every claim traced to a commit, receipt, or re-run count | `7638049` | the README itself |

Suite: 145 to **159 tests**, zero failures, zero skips. Ladder at close: **9 gates, 9 PASS, exit 0**, ledger committed at `loop/gates.jsonl`. Org state: DVerseCore 0.5.0.0, one Matter record (`First DVerse Matter` / M-0001), plugin step ACTIVE.

## The two refusal pairs

The wave's thesis in two artifacts:

1. **Build-time (4.3):** `pac solution pack` exits 0 on both the correct 1:N document-location relationship and the inverted N:1 that silently empties the Documents tab. G4 passes one and refuses the other. The platform accepts both; the harness does not.
2. **Runtime (4.4):** the running app and the raw Web API both refuse `BAD-1`/`WRONG-100` with the plugin's message and accept `M-2026`. The refusal ladder now runs from source YAML to live org behavior.

## Burned lessons (14 to 17, appended this wave)

- 14: FormXml `datafieldname` binds by lowercase logical name; wrong casing drops controls silently at render.
- 15: plugin registration is import-enforced through sequential rungs (FullName schemaName, part-URI slash, mandatory strong-naming, canonical element sets); platform-mirror is mandatory for any new component type's first registration.
- 16: the platform's own export violated G8's assumption (id-only root component); when a gate refuses platform-authored output, suspect the gate first.
- 17: `pac solution import` leaves plugin steps DISABLED without `--activate-plugins`; only a post-import negative-input probe catches it. Every import of step-bearing solutions now uses the flag, and every grading includes the probe.

## Integration findings

1. **The import-rung pattern generalized.** Lesson 3 (pack acceptance is not import acceptance) compounded: import errors surface ONE rung at a time, so each fix reveals the next refusal. The 4.4b import failed four distinct ways in sequence before the seat switched from diagnosis-by-retry to platform-mirror via Web API registration. Mirror-first is now the standing rule for first registrations (lesson 15).
2. **The runtime probe is the top verification rung and it caught a real escape.** Import exit 0 + publish + correct step ids still shipped a disabled step (lesson 17). Nothing below live behavior proves live behavior.
3. **Seat corrections at grading are part of the loop, disclosed in commits:** real message GUIDs fetched from the org replaced the executor's flagged placeholders; the datafieldname hot-fix was seat-authored against a defect no spec anticipated. Both are on the record with their evidence.

## Process notes for the ratchet

- Spec-before-spawn held for all six slices; every executor booted from LESSONS.md; zero forbidden-file violations across the wave.
- Executor risk tables earned their keep: 4.4b's HIGH-risk flags (message GUIDs) were exactly where import failed, and its honest "self-generated placeholder" labeling made the seat correction a lookup instead of a diagnosis.
- The G6 flake closure (O11) held through every full-suite run this wave ran after the merge.

## L3 assurance

Not required for this close, by owner ruling of 2026-08-13 (no production-worthy external-facing output yet). The requirement arms at latest before the public flip.

## Next: wave 5 (owner greenlight 2026-08-14)

Canvas app + SharePoint document management at runtime: surface the dv_matter document-location relationship in the running app against the provisioned site (`https://dmdllc08.sharepoint.com/sites/DMDLLC`), and bring a canvas app into gated source (`.pa.yaml`), the artifact class the mission statement names and no gate yet covers.
