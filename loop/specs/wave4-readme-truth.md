# Slice spec: wave 4 README truth pass (8.2 pulled forward)

Persisted before spawn. Executor: Sonnet, worktree `.worktrees/slice-readme`, branch `slice/readme`.

READ loop/LESSONS.md BEFORE WRITING ANYTHING.

## The defect

`README.md` still describes day zero: "Status: day zero", "Nothing here is built yet", a component table with every row "not started", "Testing: Nothing is tested, because nothing is built." All of it is now false, which in a repo about honest claims is the worst kind of bug.

## Frozen rulings

1. Rewrite README.md against current reality. EVERY claim must be traceable to something in the worktree or the git log: a commit hash, a receipt file under docs/receipts/, a test count you personally re-ran, a workflow file. Claims you cannot trace, you do not write.
2. What reality now includes (verify each yourself before writing it): 9 gates live (G1-G4, G6-G10; G5 deferred with its reason), 156 tests (re-run to confirm; note lesson 13's known flake honestly if you hit it), two CI tiers (ubuntu offline every push, Windows online path-filtered with OIDC and zero secrets), the refusal pair receipt (docs/receipts/wave4-3-refusal-pair.md, the flagship: pack accepts both directions, the gate refuses the inverted one), three golden imports (0.1.0.0 shell, 0.2.0.0 table, 0.3.0.0 relationship), a running model-driven app in the org, the dv-architect skill with gate cross-references, loop/LESSONS.md and loop/specs/ as the process substrate.
3. Keep the house structure: honest badges only (a real CI badge for each workflow is now TRUE and should be added, pointing at the actual workflow files), what-it-is intro, the layered-positioning section and its claims are still accurate and stay, provenance inherited-vs-built, practices marked code-enforced vs written-rule (this table changes: many rows moved from planned to code-enforced), honest testing section with real numbers, For Hiring Managers close. Update the known-limits section: keep what remains true (canvas Preview tooling, fork-PR G7), drop what resolved, add what emerged (docs-vs-tooling contradictions, pack-vs-import gap, all citable to LESSONS).
4. Also update `docs/plan.md`'s gate-ladder table statuses (it still shows wave-1-era Built column) in the same pass; plan.md is otherwise historical and stays.
5. No em dashes anywhere. Suite sanity run at the end (you change no code; expect 156 green, lesson-13 caveat applies).

## Owned files
- README.md
- docs/plan.md (the gate table statuses only)

Forbidden: everything else, including ARCHITECTURE.md, ROADMAP.md, all code, all receipts.

## Done means
Committed "Slice README:" with DDomingo author flags. Report: the claims-to-evidence map (each major claim and its trace), what was dropped as stale, verbatim suite sanity output, commit hash, assumptions.
