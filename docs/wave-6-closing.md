# Wave 6 closing

Closed 2026-08-14. The skill wave, deliberately last among the build waves: the gates came first so that every rule the skill states is either mechanically enforced or empirically burned, never asserted.

## Delivered

| Slice | What | Evidence |
|---|---|---|
| 6.1 PORT | Seed `d365-architect` v3 imported verbatim to `seed/` (provenance commit 2c72b66), then carried forward with every claim re-verified; stale content dropped and listed (prefix, XML-era shapes, backwards snk guidance, wrong TFM) | executor commit `2d096bf` |
| 6.2 ENCODE | 10 gate-cited rules (G1-G4, G6-G11) + 14 lesson-cited rules (L1-L9, L12, L14-L17); rules without a citation were not written; G5 stated as reserved | `plugins/dv-architect/skills/dv-architect/SKILL.md` |
| 6.3 LAYOUT | Marketplace convention mirrored from the real `microsoft/power-platform-skills` tree (paths examined and recorded): `plugins/dv-architect/.claude-plugin/plugin.json` + `skills/dv-architect/` nesting | executor report; `plugins/dv-architect/` |
| 6.4 EVAL | Blind transfer test: fresh agent, skill-only knowledge, ten scenarios against the seat-held key. **20/20, zero misses**, all silent-failure flagships correct with citations | `docs/evals/wave6-skill-eval.md` |

Seat corrections at grading, disclosed: removed the superseded scaffold-era skill tree at `skills/` (pre-lessons, no G11; two divergent skill artifacts is a truth-repo defect), and ran the README truth pass to waves 4-5 reality (ten gates, 174 tests, five golden imports, O11 closed, `pac canvas validate` removal noted, G5 marked unblocked).

Suite: 174/174 zero skips throughout; 10-gate ladder green; both CI tiers green on every push of the wave.

## Findings

1. **The lessons file was the skill's raw material.** The evolved skill is, in large part, `loop/LESSONS.md` restructured for transfer with gate cross-references. The loop's learning substrate paid out directly as product.
2. **The eval design has a blinding gap**: the answer key rode in the committed spec before the spawn, so blinding rested on forbidden-list compliance. Process ratchet recorded in the eval doc: hold keys out of tree until grading.
3. **Q&A-shaped evals prove transfer, not build performance.** A build-shaped eval (fresh agent, greenfield use case, gates on) is the natural wave 7+ follow-on when a new use case arises.

## L3 assurance

Not required for this close, by the standing owner ruling. The requirement arms before the wave 8 public flip.

## Next: wave 7 (awaiting owner greenlight)

Structural diff: element identity model for FormXML (identity is not positional), semantic diff over declarative artifacts, diff-aware gates, diff receipts in the ledger. The most demanding engineering in the project; the honest version takes real time.
