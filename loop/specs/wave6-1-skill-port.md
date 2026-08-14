# Slice spec: wave 6.1, port the d365-architect skill

Persisted before spawn. Executor: Sonnet, worktree `.worktrees/slice-6-1`, branch `slice/6.1`. Pulled forward in the fan-out because it touches nothing any other slice owns.

## Frozen rulings

1. Source: the archived public seed https://github.com/tbalt88/DVerseClaudeSkills (clone shallow to temp, read-only): `d365-architect/SKILL.md` (81 lines) plus `references/` (6 files: ce-alm, ce-bootstrap, ce-data-access, ce-integration, ce-plugin-dev, ce-security).
2. Destination layout follows Microsoft's marketplace plugin convention (deferred D2 option (a) stays open): `skills/plugins/dv-architect/` containing `skill/SKILL.md` and `skill/references/*.md`. Add a plugin-level `README.md` stating provenance (evolved from d365-architect v3, seed archived) honestly.
3. Evolve, do not just copy: (a) rename identity to dv-architect, DVerse v2 context; (b) add a new section "Rules with mechanical enforcement" cross-referencing gate IDs for every rule the harness now enforces: publisher prefix (G2), doc-location cardinality 1:N (G4), dependency declarations (G3), rootcomponent source presence (G8), solutioncomponents paths (G9), YAML layout (G10), well-formedness (G1), build+tests (G6), Solution Checker (G7); each line names the rule as prose AND its gate; (c) update stale v1 claims: publisher prefix is dv_ not dexx_, solutions are YAML source format, the harness exists. Keep the seed's 12 plug-in best practices and reference content substantively intact; correct only what v2 reality contradicts.
4. Every factual claim about this repo you write must be verifiable in the worktree (gate IDs, file paths); do not invent capabilities.
5. No em dashes anywhere you write. Harness suite untouched and green (sanity run, baseline 145).

## Owned files
- skills/** (the README.md at skills/ root may be updated to reflect the new content)

Forbidden: everything else.

## Done means
Committed "Slice 6.1:" with DDomingo author flags. Report: files, what was evolved vs preserved, the gate cross-reference table as written, suite sanity output, commit hash, assumptions.
