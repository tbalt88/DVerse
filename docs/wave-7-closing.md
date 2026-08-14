# Wave 7 closing

Closed 2026-08-14. The structural-diff wave, the one the ROADMAP called the most demanding engineering in the project, run design-first: a ratified identity model before any code, because a diff that gets element identity wrong produces confident wrong answers, worse than no diff.

## Delivered

| Slice | What | Evidence |
|---|---|---|
| 7.1d | Element identity survey and model: 28 classes, identity keys, honest fallbacks (rows/columns positional-with-warning), and the empirical proof that platform ids are byte-stable across exports (two clones diffed; only pac's local ProjectGuid churns) | `docs/design/element-identity-model.md` + seat ratification (obligations O12-O14) |
| 7.1i | Matching layer: 27 matcher families, warnings as data, rename=delete+add for canvas, unsurveyed types never match silently | commit `4061cbe`, 37 tests, both mutation checks red-proven |
| 7.2 | Semantic diff engine: recursive walker over every family chain, property-level Changed verdicts, the recursion rule (matched parents always recurse) mutation-proven on the lesson-14 fixture | commit `9a2f56c`, 27 tests |
| 7.3 | G12 `structural-diff` gate + `dverse diff` CLI verb: refuses datafieldname-casing changes, unsurveyed-type changes, and packaging removals with source present; skips honestly without a baseline so the standard ladder keeps its 10-PASS shape | commit `0b7045e`, 15 tests, all refuse classes live-proven through the CLI |
| 7.4 | Diff receipts in the committed ledger: a real evolution diff (wave-4 baseline vs current tree, 19 PASS including honest canvas adds) and THE DIFF REFUSAL PAIR (one casing flip, REFUSE with lesson 14's reason verbatim) | `docs/receipts/wave7-diff-refusal-pair.md`, `loop/gates.jsonl` G12 lines |

Suite: 174 to **253 tests**, zero failures, zero skips, every slice mutation-checked. Ladder: 10 PASS + G12 honest SKIP without a baseline; self-diff over the full real tree clean.

## The wave's claim, earned

Wave 4's refusal pair proved the harness refuses silent defects statically. Wave 7's refusal pair proves it refuses silent CHANGES: the exact defect that cost a live diagnosis session in wave 4.6 (the Owner-only form) is now caught mechanically at diff time, named with its lesson, before pack runs. Identity is matched by stable platform ids (empirically proven stable), so a one-line change in a 19-artifact tree surfaces as exactly one verdict.

## Honest limits

- FormXml rows and columns have no stable ids in the real platform shape; they match positionally with an explicit unverified-pairing warning on every such match, never silently.
- Edit-and-republish id stability is assumed from read-stability, not yet proven (O12, verifies on the next real form edit). Canvas rename=delete+add is ratified from platform semantics but unobserved in this corpus (O14). Multi-column forms unexercised (O13).
- The packaging-removal check reuses G8's type-path templates and shares its documented type-coverage gap.
- Diff classification is a fixed path-shape table mirroring the surveyed model; unclassified files are out of scope by design, not silently misdiffed.

## Process notes

- Design-first paid for itself: the empirical clone-stability check (a design-slice deliverable) is the load-bearing fact under every GUID matcher, and it cost one read-only afternoon instead of being discovered as a production surprise.
- Three implementation slices, three executors, zero forbidden-file violations, two disclosed-and-justified boundary touches (a fixture-discovery switch, one visibility widening), every slice mutation-checked.

## L3 assurance

Not required for this close, by the standing owner ruling. Arms before the wave 8 public flip.

## Next: wave 8 (awaiting owner greenlight), the public flip

Owner-run visibility flip with the vault scrub checklist (Mac-only), seat-run README refresh so claims match enforcement, and the L3 assurance requirement arms. Backlog riding alongside: G5 (unblocked, not built), O12-O14.
