# Wave 3 closing

Closed 2026-08-13, the first wave run fully under the HFLA loop discipline: four slice specs persisted and committed before any executor spawned (`8d0b89c`), four Sonnet executors in isolated worktrees, seat integration and grading, one push.

## Delivered

| Slice | Gate | Commit | Ground truth method |
|---|---|---|---|
| 3.1 | G1 well-formedness | `29770b7` | parser errors passed through verbatim |
| 3.2 | G3 dependency-integrity | `f422510` | decompiled `DiskReader.GetMissingDependencies` and `Helper.LoadSolutionInformation`: shape is `MissingDependencies:/MissingDependency:/Required:` with `@type @schemaName @displayName` attributes |
| 3.3 | G8 rootcomponent-sources, **closes O2** | `10e0495` | decompiled `DiskReader.GetRootComponents`: `RootComponents:/RootComponent:` with numeric `@type` (Entity=1, CanvasApp=300) and `@schemaName` |
| 3.5 | G6 build-and-tests | `71c94b5` | parse layer derived from captured real `dotnet test` output |
| ~~3.4~~ | **G5 deferred to wave 4.4+ by seat refusal at the plan gate** | spec commit `8d0b89c` | its input shape is unobservable until a real plugin exists; building from docs is the G9 mistake repeated |

Suite: 96 to **145 tests**, zero failures, zero skips. Gate catalogue live: **G1 G2 G3 G4 G6 G7 G8 G9 G10** (all but the deferred G5).

Full offline run over the real solution at close: **8 gates, 8 PASS, exit 0.**

## Integration findings (the seam earned its keep)

1. **The leak scan made its second real catch.** G6's Refuse embedded raw `dotnet test` output, which carries absolute machine paths straight into a ledger bound for a public repo. Fixed at integration with a path sanitizer: known roots rewritten relative, remaining drive-rooted prefixes stripped to file names so `file(line,col)` detail survives. First catch was the wave 1 runner defect; the scan is now two-for-two on defects nothing else saw.
2. **Warm-run test discovery was nondeterministic.** Once the integration sweep had built G6's fixture projects, `dotnet test` against the slnx also executed `Fixture.Fail.dll` (whose one test fails by design). Cold CI checkouts never see this because discovery runs before the fixtures are ever built, which would have made it a works-in-CI-fails-locally trap. Canonical test command is now the Tests csproj explicitly, in the workflow and everywhere else.
3. **The GateFor seam behaved as designed.** All four executors hit the same three expected failures (the seat-owned integration switch not knowing their fixture family), all four refused to touch the forbidden file, all four flagged it honestly. The carve-out cost three red tests per slice in exchange for zero merge conflicts across four parallel slices; the trade held.

## Process notes for the ratchet

- Spec-before-spawn at M2 held for all four slices; specs live in `loop/specs/` with the rulings that were actually enforced.
- Decompilation-before-parsing is now standing policy and paid off twice more (G3, G8 shapes both differ from anything documented).
- One executor surfaced that the numeric `@type` convention in rootcomponents is inferred from `int.TryParse` in the shared reader, not from an empirical sample, because no real pac unpack has ever produced a non-empty file here. Flagged for confirmation when wave 4.2 creates the first real entity.

## L3 assurance

Not required for this close, by owner ruling of 2026-08-13: no production-worthy external-facing output yet. The requirement arms at latest before the wave 8 public flip.

## Next

Wave 4.2: the first real table with attributes and a main form, which also produces the first real non-empty `rootcomponents.yml` (confirming G8's inferred convention) and the substrate for 4.3, the document-location refusal demonstration.
