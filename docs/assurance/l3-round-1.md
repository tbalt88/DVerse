# L3 assurance round 1: pre-public-flip audit

Run 2026-08-14, per the standing owner ruling that external assurance arms before the wave 8 visibility flip.

**Auditor tier:** an independent Sonnet agent with no build history in this repo, read-only access, adversarial posture (claims unverified until evidence personally seen). **Auditee:** the dex-engineering-lead seat. The auditor independently re-ran the test suite and the gate ladder, read the enforcing code behind every code-enforced label, swept the full tree for public-readiness, and hunted for unstated limits.

## Findings and disposition

| ID | Severity | Finding (condensed) | Disposition |
|---|---|---|---|
| F1, F2, F4, F6, F7, F8 | NOTE | Load-bearing claims VERIFIED: 253 tests reproduced exactly; ladder output matches the README block; eleven gate classes on disk; the wave-7 diff refusal pair present in the committed ledger with matching verbatim text; exactly two refusal pairs, receipts consistent; all five golden imports traced to commits; zero orphan receipts either direction | No action; recorded |
| F3 | MINOR | README ladder snippet header said `gates=9`; the real run prints `gates=10` (stale capture pre-G12) | FIXED: snippet corrected |
| F5 | NOTE | Wave 4.3 G4 refusal lives in the receipt doc, not the committed ledger; README never claims otherwise | No action; consistent as stated |
| F9, F10 | NOTE | Every code-enforced label in the Engineering notes table verified against the actual implementing code; none overstate (slice 8.3 satisfied by independent verification) | No action; recorded |
| F11 | MAJOR | The stated G7 limit ("cannot run on fork PRs") was materially incomplete: the online tier has no pull_request trigger at all, so G7 never gates ANY PR pre-merge; it is a post-merge detection rung | FIXED: Known limits rewritten to state this plainly, including why (OIDC trust pinned to main) and what carries pre-merge refusal (the offline tier) |
| F12 | MINOR | README provenance listed `Microsoft.PowerPlatform.Dataverse.Client` as consumed; it is referenced nowhere in the tree (upstream-map already honest: pin TBD at first use) | FIXED: provenance now says declared-not-yet-consumed |
| F13 | MINOR | "Every rule cross-referenced" claim did not hold for `ce-security.md`'s spec-only reference material (which labels itself honestly) | FIXED: claim scoped to the SKILL.md rules tables, reference docs noted as carrying labeled spec-only material |
| F14 | NOTE | ROADMAP cited "blind eval 20/20" without the eval doc's own blinding caveat | FIXED: caveat now rides with both ROADMAP mentions |
| F15 | NOTE | G12 narrows but does not close the datafieldname gap (a from-birth miscasing has no baseline); stated limit accurate, could be clearer | ACCEPTED as stated; the Known limits entry already calls the guard spec-only |

**Public-readiness sweep: clean.** No secrets, tokens, connection strings, TODO markers, editor droppings, oversized binaries, or leaks beyond the deliberate disclosures in `docs/provisioning-record.md`. Personal identifiers confined to the deliberate git author identity.

## Convergence

All findings dispositioned in this document's commit; no disputes. **Auditor's verdict: nothing blocks the public flip**, with F11 folded into stated limits before it (done). Round CONVERGED, 2026-08-14.

## Process notes

First L3 round for this engagement; the rotation requirement (a DIFFERENT auditor identity next round) applies from round 2. The finding profile (one MAJOR limits gap, zero claim fabrications, zero leaks) is consistent with the house's truth-pass discipline working, and with its known weakness: self-authored limits describe what the author feared, not what a stranger notices.
