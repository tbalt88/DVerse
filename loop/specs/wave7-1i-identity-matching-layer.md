# Slice spec: wave 7.1i, the identity and matching layer

Persisted before spawn. Executor: Sonnet, worktree `.worktrees/slice-7-1i`, branch `slice/7.1i`.

READ loop/LESSONS.md AND docs/design/element-identity-model.md (including the seat ratification section) BEFORE WRITING ANYTHING. The ratified model is your contract; do not re-litigate it.

## Mission

The matching layer of the structural diff: given two versions of a declarative artifact document, produce matched element pairs, added, removed, and warnings, per the ratified identity model. NO property-level change detail yet (that is 7.2); this slice answers only "which element in B is the same element as this one in A".

## Frozen rulings

1. LOCATION: `harness/DVerse.Harness/Diff/` namespace folder inside the existing harness project; tests in the existing `harness/DVerse.Harness.Tests/` (Diff/ subfolder). The canonical build and test commands must keep working unchanged; no new csproj, no workflow edits.
2. PUBLIC SURFACE, small and deep (the seam matters more than the internals):
   - `ElementIdentity` (class kind + key string + warning flag)
   - `MatchResult` (Matched pairs with both nodes, Added, Removed, Warnings)
   - `IElementMatcher` with `MatchResult Match(YamlNode a, YamlNode b)` per artifact class family, plus a registry keyed by artifact class
   Exact names may vary; smallness may not. Every ratified class family from the model's table gets a matcher or is explicitly registered as unsurveyed.
3. THE RATIFIED RULES ARE LAW: GUID keys matched exactly; logical-name keys case-insensitively; RootComponents dual rule; FormXml columns/rows positional WITH a warning on every such match; unknown control classes and unsurveyed component types produce warnings, never silent matches (rulings 3 and 8); canvas keys are exact strings scoped to the parent (rename is delete+add).
4. Warnings are DATA, not logs: they ride in MatchResult with the element path and the reason, because 7.3's diff-aware gates will turn some of them into refusals.
5. FIXTURES from the real tree: pairs derived from the actual demo-solution artifacts with controlled mutations (an added control, a removed cell, a reordered row, a renamed canvas control, an unsurveyed component type). The reordered-row fixture is the flagship: it must match POSITIONALLY with warnings, and the report must show the warning text verbatim. Mutation-check per lesson 6 on at least the positional-warning path and the unsurveyed-type path.
6. VERIFY: full suite green (baseline 174 plus yours, zero skips), full gate ladder still 10 PASS (you add no gate, but the suite gates your code via G-none: the ladder run is a no-regression sanity), no absolute paths anywhere (lesson 1; ubuntu CI will run this).
7. No em dashes anywhere you write.

## Owned files
- harness/DVerse.Harness/Diff/** (new)
- harness/DVerse.Harness.Tests/Diff/** (new)
- harness/fixtures/diff/** (new)

Forbidden: everything else. All gates, the CLI, workflows, demo-solution, docs are read-only. No org access, no pac.

## Done means
Committed "Slice 7.1i:" with DDomingo author flags. Report: the public surface as shipped, the matcher registry table (class family to key rule to warning behavior), fixture inventory, mutation-check evidence, verbatim suite and ladder outputs, commit hash, assumptions.
