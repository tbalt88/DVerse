# DVerse v2 Roadmap

Waves past wave 1. Authored 2026-07-27, after wave 1 closed at commit `fd76100` with 64 tests green.

Design authority is [`ARCHITECTURE.md`](ARCHITECTURE.md). Wave 1 scope and the risk register are in [`docs/plan.md`](docs/plan.md). This document is sequencing only: what happens in what order, why that order, and what each wave is not allowed to skip.

## Status

| Wave | Content | State |
|---|---|---|
| 0 | recorder, refusal ledger, frozen contract | **done**, `a61f0e5` |
| 1 | G2, G4, G9, G10 with red fixtures | **done**, `fd76100` |
| 2 | CLI, CI wiring, tenant, golden import receipt | next |
| 3 | remaining offline gates | planned |
| 4 | demo solution, Dataverse slice | planned |
| 5 | canvas app and document management | planned |
| 6 | evolved architect skill | planned |
| 7 | structural diff | planned |
| 8 | public flip and receipts | planned |

## The ordering constraint that shapes everything

**The trial Dataverse environment has a 30 day life and it starts the moment it is created.** It does not exist yet, by deliberate decision (D16).

Waves 0, 1 and 3 need no tenant. Wave 2 needs one. So the trial is created at the start of wave 2 and the 30 day window is spent on work that genuinely requires it, rather than burning down while offline gates are written.

Everything tenant-dependent is therefore pulled forward into waves 2, 4 and 5, and everything tenant-free is pushed to wave 3 or later where the clock does not matter.

## Wave 2, CI and the golden import receipt

**Owner action required at wave start: create the trial environment.**

A gap found while writing this roadmap: **there is no entry point.** The harness is a class library with zero `Program.cs`. Nothing can invoke a gate. CI cannot call a library. C2c below closes that, and it is a prerequisite for every later wave, not a wave 2 detail.

| Slice | Deliverable | Tenant? |
|---|---|---|
| 2.1 | **C2c CLI**, `dverse gate run --solution <path> --ledger <path>`, exit non-zero on refusal | no |
| 2.2 | GitHub Actions offline workflow, runs on every push and every fork PR | no |
| 2.3 | Entra app registration and OIDC federated credential | **owner** |
| 2.4 | G7 Power Apps Checker gate, online tier | yes |
| 2.5 | Online workflow, trusted branches only | yes |
| 2.6 | **Schema confirmation**, run `pac solution clone` against the real tenant, diff actual YAML node names against every inferred parser, correct what differs | yes |
| 2.7 | **Golden import receipt**, artifacts really imported, solution really worked, dated and committed | yes |

**2.6 is the one that cannot slip.** Every gate built so far parses a shape guessed from documentation. Until real clone output confirms those node names, the gates are correct in rule and unverified in mechanism. This is the first moment that can be settled, and it must be settled while the trial is alive.

**Definition of done:** offline gates run green on a fork PR with no credentials. G7 runs at least once against the real tenant with output committed. Every inferred parser confirmed or corrected against real clone output. Golden import receipt dated and in git.

## Wave 3, the remaining offline gates

No tenant, so this can run before, during, or after the trial window without cost.

| Slice | Gate | Notes |
|---|---|---|
| 3.1 | G1 well-formedness | table stakes, cheap |
| 3.2 | G3 dependency integrity | `missingdependencies.yml` honesty |
| 3.3 | G8 rootcomponent source presence | the exit-code-0 case Microsoft names explicitly; **outstanding gap from wave 1** |
| 3.4 | G5 plugin registration sanity | correlates registration YAML against C# source, the hardest of the four |
| 3.5 | G6 build and unit tests | shells to `dotnet`, parses results |

G5 is a candidate for slicing further. It is the only gate that reads two sources and reconciles them, and if it cannot be honestly rated Easy it does not go fire-and-forget.

**Definition of done:** every gate in the catalogue except G7 is built, each with a red fixture, each discovered by the integration sweep.

## Wave 4, the Dataverse slice of the demo solution

The thin slice from D13: one table, one form, one plugin, one document-location relationship, gated by the gates that already exist.

| Slice | Deliverable |
|---|---|
| 4.1 | `dv_` publisher and solution manifests |
| 4.2 | One custom table with attributes and a main form |
| 4.3 | Document management enabled, 1:N relationship to `SharePointDocumentLocation` |
| 4.4 | One plugin assembly, registered, with unit tests |
| 4.5 | Full gate run over the real solution, ledger committed |

**4.3 is the demonstration.** Build the relationship correctly, commit the green ledger entry. Then deliberately invert it, run the gates, and commit the refusal. Two ledger entries, minutes apart, showing the harness catching a defect the platform would have accepted in silence. That pair is the most convincing artifact this project can produce, and it costs almost nothing to create.

**Definition of done:** the solution packs, unpacks, and round-trips without loss. Every gate passes over it. The deliberate-violation refusal is on the record.

## Wave 5, canvas app and the SharePoint footprint

| Slice | Deliverable |
|---|---|
| 5.1 | Canvas app over the Dataverse table, CRUD |
| 5.2 | Single-screen and multi-screen variants |
| 5.3 | SharePoint document management, server-based integration |
| 5.4 | Canvas `.pa.yaml` gating module, isolated from the Dataverse gates |
| 5.5 | Search, edit, create, delete verification |

**Risk R1 concentrates here.** `pac canvas pack/unpack/validate` are Preview. 5.4 stays in its own module so that when the tooling shifts, the blast radius is one directory.

## Wave 6, the evolved architect skill

C1, deliberately late. The skill encodes the rules; the gates enforce them. Writing the skill *after* the gates means every rule it states has already been proven mechanically checkable, rather than the skill asserting rules that turn out to be unenforceable.

| Slice | Deliverable |
|---|---|
| 6.1 | Port `d365-architect` v3, 81 lines plus six reference docs, from the archived seed |
| 6.2 | Encode gate-backed rules, cross-referenced to gate IDs |
| 6.3 | Lay out to Microsoft's `plugins/<name>/` marketplace convention |
| 6.4 | Evals against the demo solution |

6.3 keeps the deferred D2 option (a) alive: contributing upstream from a position of demonstrated work rather than to establish it.

## Wave 7, structural diff

Deferred from D9 on purpose. **A diff engine that gets FormXML element identity wrong produces confident wrong answers, which is worse than no diff.**

| Slice | Deliverable |
|---|---|
| 7.1 | Element identity model, FormXML identity is not positional |
| 7.2 | Semantic diff over declarative artifacts |
| 7.3 | Diff-aware gates, what changed rather than what exists |
| 7.4 | Diff receipts in the ledger |

This is the most impressive engineering in the project and the least urgent. It goes last because the honest version takes real time and the project has value without it.

## Wave 8, public flip and receipts

**Owner runs the visibility flip.**

| Slice | Deliverable | Who |
|---|---|---|
| 8.1 | Public-repo scrub checklist from the vault | **owner**, vault is Mac-only |
| 8.2 | README refresh so claims match what the code enforces | seat |
| 8.3 | Enforced versus written-rule split verified honest | seat |
| 8.4 | Visibility flip | **owner** |
| 8.5 | Archived seed gains its live forward link | seat |
| 8.6 | Series content from the build | owner, `/dex-twin` |

8.5 is a standing obligation from D4: the archived seed currently names DVerse v2 without linking it, because a link to a private repo 404s for everyone. It gets its link the moment the repo is public.

## What is deliberately not on this roadmap

- **Custom connectors.** Cut in D3.
- **Competing on generation.** Their marketplace builds, DVerse gates.
- **Multi-vendor models.** Claude only, D12.
- **GitHub Pro.** Declined; refusal lives in the harness, not in branch protection.

## Cross-wave obligations

Carried forward until discharged, so they cannot be lost between waves.

| # | Obligation | Due |
|---|---|---|
| O1 | Confirm inferred YAML node names against real clone output | wave 2.6 |
| O2 | G8 rootcomponents gate, gap left open by wave 1 | wave 3.3 |
| O3 | Isolated build outputs per slice, concurrency contention bit wave 1 | wave 2 |
| O4 | Re-check `microsoft/power-platform-skills` for a dataverse-backend plugin | every wave |
| O5 | Archived seed gains its forward link | wave 8.5 |
| O6 | Mission statement may be tweaked if an interesting use case warrants | owner, any time |
| O7 | **DONE, wave 2.1 (commit e630ac6).** `Artifact` is repository-root-relative, forward slashes, never absolute, across all gates. Verified by strengthened per-gate tests (mutation-checked: reverting the fix turns 9 tests red) and a live CLI run showing one path base. | done |
| O8 | **CLI must enforce SolutionRoot under RepositoryRoot** (exit 2 otherwise). Repo-relative artifacts are only meaningful when the solution root sits under the repo root; with an outside root they climb out via `..` and reproduce the outside path in relative form. Found by the first Linux CI run failing a wave 1 test that passed on Windows only via a path-separator accident. Add CLI validation plus a CliTest. | next executor slice |
| O9 | GitHub deprecation notice: `actions/checkout@v4` and `setup-dotnet@v4` target Node 20, forced onto Node 24. Bump to current majors in a later slice; non-blocking warning today. | wave 3 window |
