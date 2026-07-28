# DVerse v2 Plan

Authored 2026-07-27 by the engineering-lead seat. Awaiting the plan gate.

Decision record: Memory Bridge `7b5ffbe2` (D1 to D5) and `8f93bc6a` (D6 to D12). This document is the plan; those entries are the decision provenance.

## Mission (owner-approved 2026-07-27, supersedes `f6d75df6`)

Build a system that makes governance of AI-built Power Platform work structurally enforceable rather than procedural. Claude models design and implement real Dataverse solutions, primarily Dataverse-backed with a deliberate SharePoint document-management footprint, and a verification harness gates their declarative output (solution XML, FormXML, entity metadata, relationship cardinality, plugin registrations, canvas `.pa.yaml`), mechanically refusing what violates a rule, at generation and again in CI, with every refusal written to one committed ledger.

The claim is narrow and defensible: plenty of tooling helps you build on this platform. None of it refuses.

The proof is a working solution built entirely under those gates, with receipts a stranger can re-run without credentials.

## Component inventory

| ID | Component | Path | Depends on | Owner |
|---|---|---|---|---|
| C1 | Evolved architect skill | `skills/` | none | seat |
| C2a | Recorder and refusal ledger | `harness/recorder/` | none | dev agent |
| C2b | Gate suite | `harness/gates/` | C2a contract | dev agent |
| C3 | Demo solution | `demo-solution/` | none for authoring | dev agent |
| C4 | CI wiring | `.github/workflows/` | C2a, C2b | seat |
| C5 | Docs and upstream map | `docs/` | all | seat |

C2a is the contract everything else consumes. It is built first and frozen before C2b or C4 begin.

## Gate ladder, first cut

Every gate ships with a fixture it refuses. A gate with no red case is an assertion, not a gate.

| Gate | Asserts | Tier | Novel? |
|---|---|---|---|
| G1 well-formedness | artifacts parse, match schema | offline | no, table stakes |
| G2 publisher prefix | all custom components carry `dv_` | offline | no, trivial |
| G3 dependency integrity | no dangling refs; `MissingDependencies` honest | offline | yes, nothing checks this pre-import |
| G4 document-location cardinality | entity to `SharePointDocumentLocation` is 1:N, never N:1 or N:N | offline | **yes, flagship** |
| G5 plugin registration sanity | stage, mode, `FilteringAttributes` match the code | offline | yes |
| G6 build and unit tests | plugins compile, xUnit suite green | offline | no, inherited |
| G7 Power Apps Checker | Microsoft's ruleset | **online** | no, composed |

G4 is the flagship because its violation fails **silently**. Microsoft documents that a non-1:N relationship to a document-location entity causes documents to simply not appear. No error, no import failure, no checker warning. That is precisely the failure class governance exists for, and nothing upstream catches it.

Deferred to wave 2: structural FormXML diff. Element identity in FormXML is not positional, and a diff engine that gets that wrong produces confident wrong answers, which is worse than no diff.

## Offline and online split

Offline gates (G1 to G6) run everywhere including fork pull requests, with zero credentials. Online (G7) runs on trusted branches only, via OIDC federation.

This is a platform constraint, not a design choice: GitHub issues neither secrets nor OIDC tokens to fork pull requests. Stated as a feature because it is one. Every gate DVerse authors is credential-free and reproducible by a stranger. The single rung needing a tenant is Microsoft's own, which DVerse composes rather than invents.

## Provisioning inventory

Secure before dev starts, never piecemeal mid-wave.

| Item | Required | Needed by | Who |
|---|---|---|---|
| Dataverse trial environment URL | yes | G7, golden import receipt | owner (exists in Azure trial) |
| Trial expiry date | yes | schedules the import receipt | **owner, unknown to the seat** |
| Entra app registration (client ID, tenant ID) | yes | OIDC auth | owner only |
| Dataverse application user plus security role | yes | G7 in CI | owner only |
| GitHub OIDC federated credential | yes | G7 in CI | seat prepares, owner approves |
| SharePoint Online site | yes | document-management footprint | owner (needs M365) |
| Server-based SharePoint integration enabled | yes | doc management works at all | owner, admin center |
| GitHub repository | done | all | complete, private |
| API keys | none | n/a | the harness needs no third-party keys |

No secrets enter chat, commits, or docs. OIDC federation means no stored client secret at all.

## Risk register

| ID | Risk | Severity | Mitigation |
|---|---|---|---|
| R1 | `pac canvas pack/unpack/validate` are Preview; the canvas gate foundation may shift | high | pin pac version; keep canvas gates in a separate module so churn is contained |
| R2 | Trial expires and G7 goes dark | high | spend the window on a golden import receipt, dated and committed; offline gates continue unaffected |
| R3 | Microsoft ships a dataverse-backend plugin upstream | medium | re-check `microsoft/power-platform-skills` each planning cycle; the gating layer stays differentiated even if building converges |
| R4 | Offline-authored artifacts pass our gates but fail a real import | medium | golden import receipt proves the artifacts were real, not merely well-formed |
| R5 | Fork PRs cannot run G7 | low | documented in README; offline tier covers everything DVerse authors |
| R6 | No Dataverse auth profile verified on this machine | medium | owner asserts prior connection at 80%; resolved by first `pac auth create` |
| R7 | Clean recorder rewrite re-learns loop.py's hard-won fixes | medium | port `_norm` loose matching and the recorder-honesty fix deliberately, with comments naming why |
| R8 | Thesis breadth (D11) weakens the differentiator | accepted | owner ruled with costs stated; claim restated as gating rather than building |

## Definition of done, wave 1

1. Every gate G1 to G6 runs offline with zero credentials, and each has a fixture it refuses.
2. A refusal at generation and the same refusal in CI both append to one committed ledger, emitted in the same action as the refusal.
3. The demo solution packs, unpacks, and round-trips without loss.
4. A golden import receipt exists: dated proof the artifacts imported into a real environment and worked.
5. G7 has run at least once against the trial, output committed.
6. README claims match what the code actually enforces, with the enforced versus written-rule split honest.
7. `docs/upstream-map.md` current, including anything newly consumed.

## Execution shape

**Estimate: 6 to 8 dev-days.** Per calibration (n=1 to 8), gated agent workflows compress calendar time roughly 4 to 5 times, so expect 1.5 to 2 wall-clock days. Both numbers are quoted deliberately: the dev-day figure is scope honesty, the compression is schedule reality.

This exceeds the 1 to 2 dev-day trigger and splits into cleanly separable workstreams, so the multi-agent pattern is recommended.

| Wave | Workstream | Parallel? | Model |
|---|---|---|---|
| 0 | C2a recorder and ledger, contract frozen | no, serial, seat-owned scaffold | seat |
| 1 | C2b gates | yes | Sonnet |
| 1 | C3 demo solution artifacts | yes | Sonnet |
| 1 | C1 evolved skill | yes | Sonnet |
| 2 | C4 CI wiring and integration | no, seat owns the seam | seat |

Model assignment is pinned, never inherited. Dev agents are executors; the manifests carry the thinking. Claude models only.

Complexity ratings: C2b and C3 are Medium (iterative, seat's call on hosting). C2a is Hard because everything depends on its contract, so it stays seat-owned rather than delegated.

## Open items carried into dev

- Trial expiry date unknown. This schedules R2's mitigation and is the one provisioning fact the seat cannot look up.
- Canvas app scope within the thin slice is not yet fixed: single-screen or multi-screen, and which CRUD operations the first slice covers.
- Structural diff design, deferred to wave 2.
