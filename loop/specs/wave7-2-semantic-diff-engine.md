# Slice spec: wave 7.2, the semantic diff engine (walker + property-level verdicts)

Persisted before spawn. Executor: Sonnet, worktree `.worktrees/slice-7-2`, branch `slice/7.2`.

READ loop/LESSONS.md, docs/design/element-identity-model.md (with seat ratification), AND the 7.1i matching layer (harness/DVerse.Harness/Diff/**, its tests, and the 7.1i report's deviations recorded in the spec loop/specs/wave7-1i-identity-matching-layer.md) BEFORE WRITING ANYTHING. The matching layer is your foundation; do not modify it except where a ruling below explicitly permits.

## Mission

Build on 7.1i's per-family matchers: the recursive document walker that orchestrates matcher calls across nesting levels, and property-level change verdicts on matched pairs. Output: a complete `ArtifactDiff` for a pair of artifact documents.

## Frozen rulings

1. LOCATION: `harness/DVerse.Harness/Diff/` alongside the matching layer; tests in `harness/DVerse.Harness.Tests/Diff/`. Canonical commands unchanged; no new csproj.
2. PUBLIC SURFACE (small and deep):
   - `DiffVerdict` per element: Added | Removed | Changed | Unchanged, where Changed carries property-level detail (property path, value in A, value in B) and every verdict carries the element's identity and any inherited match warnings
   - `ArtifactDiff` (artifact class, ordered verdict list, warnings, summary counts)
   - `ArtifactDiffer` with `ArtifactDiff Diff(YamlNode a, YamlNode b, string artifactClass)` that WALKS: match at the top family, recurse into matched pairs with the child family's matcher, per the model's recursion rule
3. THE RECURSION RULE IS LAW (model section 5): an Added/Removed parent short-circuits its subtree (one verdict, no recursion); a Matched parent ALWAYS recurses, because identical id chains can still hide a single changed datafieldname (lesson 14 is the canonical case; a fixture must prove exactly that shape).
4. Property comparison: scalar properties compare by string value; the `=` prefix on canvas formulas is part of the value; attribute order is NOT significant (YAML mappings); missing-vs-empty distinction is preserved (an absent key and an empty value are different, per the canonical-shape lessons).
5. Positional-match warnings (rows/columns) PROPAGATE into every verdict under that pairing, and the ratified doctrine rides with them: a positional Changed verdict is "unverified pairing, content-confirmed only" and the verdict text must say so.
6. The walker must handle every family chain the model defines for the artifact classes we ship: solution manifests, entity (attributes), FormXml full chain (form-tab-column-section-row-cell-control), savedqueries, relationships, appmodule chain, sitemap chain, plugin chain, canvas chain (screens-controls recursive by Children). Unsurveyed families inside a walk produce the 7.1i warning and a conservative Changed-with-warning verdict, never silence.
7. FIXTURES: extend harness/fixtures/diff/ with at minimum: the lesson-14 shape (identical structure, one datafieldname changed), a canvas Children-nesting change two levels deep, a property change UNDER a positionally-matched row (proving warning propagation), and an unchanged-everything pair (all Unchanged, zero warnings). Mutation-check per lesson 6 on the recursion-rule path (disable recursion into matched pairs, prove the lesson-14 fixture goes red).
8. 7.1i's matching layer may be modified ONLY to add the internal hooks recursion genuinely needs (e.g. exposing child-node access); any such change is listed in the report with its reason, and all 7.1i tests must stay green unmodified.
9. VERIFY: full suite green (baseline 211 plus yours, zero skips), ladder 10 PASS, no absolute paths (lesson 1), no em dashes.

## Owned files
- harness/DVerse.Harness/Diff/** (additive; 7.1i files per ruling 8)
- harness/DVerse.Harness.Tests/Diff/**
- harness/fixtures/diff/**

Forbidden: everything else. No org access, no pac.

## Done means
Committed "Slice 7.2:" with DDomingo author flags. Report: public surface shipped, the family-chain walk table, fixture inventory, mutation-check evidence, any 7.1i modifications with reasons, verbatim suite and ladder outputs, commit hash, assumptions.
