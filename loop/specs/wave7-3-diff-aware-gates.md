# Slice spec: wave 7.3, diff-aware gating (G12) and the diff CLI verb

Persisted before spawn. Executor: Sonnet, worktree `.worktrees/slice-7-3`, branch `slice/7.3`.

READ loop/LESSONS.md, docs/design/element-identity-model.md (with ratification), and the shipped diff layer (harness/DVerse.Harness/Diff/**) BEFORE WRITING ANYTHING.

## Mission

Make the diff engine GATE: "what changed" becomes refusable. Two deliverables:

1. **G12 `structural-diff`**, a gate that takes a BASELINE solution tree and the current one, runs ArtifactDiffer over every artifact pair, and refuses on the change classes the house has burned:
   - a Changed verdict whose property path is a `datafieldname` where the new value is not the exact lowercase of itself (lesson 14's class, now caught at DIFF time)
   - any verdict carrying an unsurveyed-type or unsurveyed-control warning (rulings 3 and 8: unsurveyed changes are unverifiable changes)
   - a Removed verdict on a RootComponentsEntry or SolutionComponentsEntry whose counterpart component still exists on disk (a packaging removal that will silently drop the component at pack, lesson 2's class caught before pack)
   Everything else (adds, removes, ordinary changes, positional-warning pairings) PASSES with the full verdict summary in Evidence; positional-pairing warnings ride in Evidence text verbatim.
2. **CLI verb**: `dverse diff --solution <path> --baseline <path> --repo <root> [--ledger <path>]` printing the per-artifact verdict summary (counts + changed property paths + warnings) and exiting 0/1/2 per the house contract; G12 refusals are ledger entries like any other gate.

## Frozen rulings

1. G12 is OFFLINE and needs a baseline to compare against; when no baseline is supplied (the normal `gate run` over a single tree), G12 SKIPS with the honest reason "no baseline provided; structural diff requires two trees". It therefore joins the catalogue but does not change the standard ladder's 10-PASS shape; the CLI diff verb is its natural entry point, and `gate run` gains an optional `--baseline <path>` that activates G12 in the ladder.
2. Gate contract as everywhere: IGate, Evidence mandatory on all verdicts, Reason on Refuse/Skip, fail closed, repo-relative forward-slash paths, red fixture(s), integration-sweep discovery. Register in the catalogue; update the CLI-registry test's id list (precedent: 5.4's CliTests maintenance).
3. Baseline semantics: the baseline is a directory tree of the same layout as the solution dir (the caller supplies it; in practice a git worktree or an unpacked prior version). G12 pairs artifacts by repo-relative path, diffs pairs that exist in both, and reports whole-file adds/removes as Added/Removed verdicts at the artifact level (no recursion into an added file, per the model's short-circuit rule).
4. FIXTURES: a green pair (benign change: a label edit), and red fixtures for each refuse class: the datafieldname-casing change, an unsurveyed-type change, and the packaging-removal-with-source-present shape. Mutation-check per lesson 6 on at least the datafieldname refuse path.
5. The demo-solution ladder run must stay 10 PASS + G12 SKIP (visible, honest). Suite baseline 238 plus yours, zero skips. No absolute paths. No em dashes.

## Owned files
- harness/DVerse.Harness/Gates/StructuralDiffGate.cs (new)
- harness/DVerse.Harness/Diff/** (additive helpers only if genuinely needed; list any)
- harness/DVerse.Harness.Cli/** (the diff verb, --baseline plumbing, registry line)
- harness/DVerse.Harness.Tests/** (gate + CLI tests; registry-list maintenance)
- harness/fixtures/g12/** (new)

Forbidden: everything else. Existing gates, workflows, demo-solution, docs read-only. No org access, no pac.

## Done means
Committed "Slice 7.3:" with DDomingo author flags. Report: refuse-class table with fixture mapping, CLI verb usage output verbatim, the ladder output showing G12 SKIP, a real diff run over two fixture trees verbatim, mutation-check evidence, verbatim suite output, commit hash, assumptions.
