# Wave 5 closing

Closed 2026-08-14, same day as its greenlight. The runtime wave: SharePoint document management live through the model-driven app, a canvas app authored, exercised, and mirrored into gated `.pa.yaml` source, and the first canvas gate (G11) built over that real corpus. One executor slice, the rest seat tenant work by design (Studio and org writes are seat-only).

## Delivered

| Slice | What | Receipt |
|---|---|---|
| 5.1 | Matter Canvas app: gallery bound to Matters, edit form (three field cards), Save/New/Delete buttons, published; mirrored via `pac canvas download` + `unpack --layout SourceCode` into `demo-solution/canvasapps/MatterCanvas/` | source in repo, round-trip `pac canvas pack` exit 0 |
| 5.2 | Multi-screen variant: Screen2 display form with `Gallery1.Selected` item, `Navigate(Screen2, ScreenTransition.Cover)` on gallery select, Back button; driven live both directions | `docs/receipts/wave5-2-canvas-screen2-detail.png` |
| 5.3 | SharePoint documents at runtime: `dv_matter` library created on the provisioned site (the original wizard run predated the table), upload through the app's Documents tab, per-record folder and `SharePointDocumentLocation` auto-created 1:N against the Matter record: the live behavior G4 exists to protect | `docs/receipts/wave5-3-documents-tab-live-upload.png`, provisioning-record addendum |
| 5.4 | G11 `canvas-yaml` gate: parse, `Control:` declarations, `=`-prefixed Properties formulas, empty-file refusal; six fixtures, mutation-checked; isolated module per risk R1 | executor commit `28d9723`, merged `afc3e06`-successor |
| 5.5 | Full CRUD through the running canvas app: created `Canvas Created Matter / M-5555` (passing the MatterNumberValidator plugin), updated, deleted; Web API confirms the org back to exactly one Matter record | `docs/receipts/wave5-5-canvas-crud-created.png` |

Suite: 159 to **174 tests**, zero failures, zero skips. Ladder at close: **10 gates, 10 PASS** over a solution that now carries table, form, relationship, app module, registered plugin, and a two-screen canvas app; ledger committed at `loop/gates.jsonl`. Both CI tiers green throughout.

## Findings

1. **`pac canvas validate` is removed in pac 2.10.1 while `pac canvas help` still lists it.** Lesson 4's docs-contradict-tooling class, verified live. Consequence: G11 validates `.pa.yaml` itself; there is no vendor validator to compose.
2. **The real `.pa.yaml` disagrees with Microsoft's published schema** on screen-level entries (no `Control:` on screens directly under `Screens:`). G11 follows the real file, with the disagreement recorded in its WHY comment. Real artifact over documentation, again.
3. **The document-management wizard's site configuration is a point-in-time snapshot.** Tables created after the wizard run silently lack libraries; the Documents tab then fails with a rename-or-deleted warning. Fixed by creating the library via SharePoint REST; recorded in the provisioning record.
4. **Risk R1 (Preview canvas tooling) held but did not bite.** download (GA) + unpack/pack (Preview) round-tripped byte-stable across three mirror cycles. The isolation of G11 into its own module stands as the blast-radius containment for when it does shift.

## L3 assurance

Not required for this close, by the standing owner ruling (no production-worthy external-facing output yet; arms before the public flip).

## Next: wave 6 (awaiting owner greenlight)

The evolved architect skill, deliberately late: port the seed's `d365-architect` v3, encode gate-backed rules cross-referenced to gate IDs (now G1 to G11 with real refusal evidence behind each), lay out to the Microsoft marketplace convention, and eval against the demo solution the gates built.
