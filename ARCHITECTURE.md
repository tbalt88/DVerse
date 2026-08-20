# DVerse Architecture

The governing document. Spec is law: deviations are surfaced to the owner, never silently substituted, and settled decisions are not re-litigated.

Authored 2026-07-27. Decision provenance lives in Memory Bridge (`7b5ffbe2`, `8f93bc6a`, `250d58ee`); this document is the design those decisions produced.

## Mission

Make governance of AI-built Power Platform work structurally enforceable rather than procedural.

Claude models design and implement real Dataverse solutions, primarily Dataverse-backed with a deliberate SharePoint document-management footprint. A verification harness gates their declarative output and mechanically refuses what violates a rule, at generation and again in CI, with every refusal written to one committed ledger.

The claim is narrow and defensible: plenty of tooling helps you build on this platform. **None of it refuses.**

## Why this is possible here and not elsewhere

The Power Platform expresses its systems as declarative artifacts. Declarative means diffable. Diffable means gateable.

That property is doing real work. On an imperative platform, "did this change break a rule" is undecidable in general. On Dataverse, the solution is a tree of XML and YAML describing tables, forms, relationships, registrations and apps. A rule like "a relationship to `SharePointDocumentLocation` must be one-to-many" is a structural predicate over that tree, and a machine can settle it in milliseconds with no heuristics and no judgment.

## The gap being filled

Microsoft's `power-platform-skills` marketplace, 572 stars and shipping daily, covers the app layer: canvas apps, model-driven apps, Power Pages, Power Automate, mobile, code apps, MCP apps. Those plugins help you **build**.

Nothing there **refuses**. There is no mechanical enforcement layer, no refusal ledger, and no Dataverse backend pro-code plugin at all.

DVerse layers on top. It does not vendor, fork, or compete with the build tooling. See [`docs/upstream-map.md`](docs/upstream-map.md) for exactly what is consumed and what was declined.

## System shape

Three components, from `0d15a322`, all of which grew concrete during the spec interview.

```
                     ┌─────────────────────────────┐
   Claude model ───► │  C1  evolved architect skill │  encodes judgment
                     └──────────────┬───────────────┘
                                    │ authors
                                    ▼
                     ┌──────────────────────────────┐
                     │  C3  demo solution           │  declarative artifacts
                     │      Dataverse + canvas app  │
                     └──────────────┬───────────────┘
                                    │ inspected by
                                    ▼
                     ┌──────────────────────────────┐
                     │  C2  verification harness    │
                     │   C2a recorder + ledger      │  ◄── the frozen contract
                     │   C2b gate suite             │
                     │   C2c CLI entry point        │
                     └──────────────┬───────────────┘
                                    │ appends
                                    ▼
                          loop/gates.jsonl
                       one append-only ledger
```

**C1 is the source of the rules. C2 is their mechanical form.** That coupling is the cleanest statement of the thesis: rules that live as prose in an architect skill ("`FilteringAttributes` must be set on Update steps", "never call the Web API inside a plug-in") become gates that refuse. Advice becomes enforcement.

## The seam

Everything hinges on one small interface. A gate reads artifacts and returns verdicts. It never writes, never records, never decides what happens next.

```csharp
public interface IGate
{
    string Id { get; }             // "G4"
    string Name { get; }           // "document-location-cardinality"
    bool RequiresTenant { get; }   // online gates skip without credentials
    IEnumerable<GateVerdict> Evaluate(GateContext context);
}
```

That is the entire contract a gate author sees. Depth behind a narrow interface: a gate can be a three-line filesystem check or a full FormXML structural diff, and nothing downstream changes.

**Recording is deliberately not a gate's job.** `GateRunner` owns it, which makes one specific failure impossible rather than merely discouraged: a gate cannot refuse without the refusal reaching the ledger, because the gate does not hold the pen.

## Invariants enforced in code

A preventive action that depends on diligence alone will decay. Each of these is a constructor check or a runner guarantee, not a comment.

| Invariant | Where | Why |
|---|---|---|
| `Evidence` mandatory on every verdict, including Pass | `GateVerdict.Validate()` | a gate that cannot say what it checked has not checked anything |
| `Reason` mandatory on Refuse and Skip | `GateVerdict.Validate()` | an unexplained refusal is indistinguishable from a defect; a silent skip reads as a pass |
| Refusal and ledger append are one action | `GateRunner.Run` | an unrecorded refusal cannot occur |
| A gate that throws **refuses** | `GateRunner.EvaluateSafely` | a harness that passes when its checker crashes issues a clean receipt for an artifact nothing inspected |
| Lazy iterators materialise inside the guard | `GateRunner.EvaluateSafely` | a deferred throw would otherwise escape the fail-closed path entirely |
| No verdict carries an absolute path | integration validation | leaked local paths break reproducibility and leak the author's filesystem into a public repo |
| Ledger append never rewrites | `JsonlRefusalLedger` | JSONL, one verdict per line; a crash truncates a line rather than corrupting a document |

## Two stages, one ledger

D5. Refusal fires at **generation**, when the harness wraps the agent's write path and a failing artifact never lands on disk, and again at **integration**, when CI re-runs the same gate logic over what was committed.

Generation is where the action happens, so that is where the guard belongs. CI is where the proof lives, because a stranger can verify a red check and cannot verify a claim about what an agent declined to write.

Both write to the same `loop/gates.jsonl`, distinguished by `GateStage`. Two ledgers would eventually disagree and a reader would have no way to tell which one lied.

## Offline and online tiers

| Tier | Gates | Runs | Credentials |
|---|---|---|---|
| Offline | G1 to G6, G8 to G10 | everywhere, including fork pull requests | none |
| Online | G7, Power Apps Checker | trusted branches only | OIDC federation |

GitHub issues neither secrets nor OIDC tokens to fork pull requests. That is a platform constraint, not a design choice, and it is stated as a feature because it is one: **every gate DVerse authors is credential-free and reproducible by a stranger.** The single rung needing a tenant is Microsoft's own, which DVerse composes rather than invents.

## Solution format

**YAML source control format, not the legacy XML format.** This is forced, not preferred: canvas app `.msapp` files and modern flows are supported *only* in the YAML format, and D11 put canvas apps in scope.

```
demo-solution/
├── solutions/<SolutionUniqueName>/
│     solution.yml · solutioncomponents.yml
│     rootcomponents.yml · missingdependencies.yml
├── publishers/<PublisherUniqueName>/publisher.yml
├── entities/<dv_entity>/attributes|formxml|savedqueries
├── entityrelationships/
├── canvasapps/<name>/<name>.msapp
└── webresources/ · workflows/ · modernflows/
```

Publisher prefix is **`dv_`**. Not `ms_`, which would assert Microsoft authorship of components that are not Microsoft's, in a repo one link from five `microsoft/*` repos.

## The gate catalogue

Gates are numbered stably. A gate's number never changes once assigned, so a ledger entry from any point in history stays readable.

| Gate | Rule | Failure mode it catches | Status |
|---|---|---|---|
| G1 | artifacts parse and match schema | loud | planned |
| G2 | `CustomizationPrefix: dv`, entity dirs `dv_` | loud | **built** |
| G3 | no dangling component references | loud at import | planned |
| G4 | entity to `SharePointDocumentLocation` is 1:N | **silent** | **built, flagship** |
| G5 | plugin stage, mode, `FilteringAttributes` match code | silent | planned |
| G6 | plugins compile, unit suite green | loud | planned |
| G7 | Power Apps Checker ruleset | loud | wave 2, online |
| G8 | `rootcomponents.yml` declarations have source on disk | **silent, exit 0** | planned |
| G9 | `solutioncomponents.yml` paths resolve | **silent, exit 0** | **built** |
| G10 | manifests under `solutions/<name>/`, not root | **misleading error** | **built** |

**The silent ones are the point.** A gate that catches something the compiler would have caught is a convenience. A gate that catches something the platform accepts, publishes, and then quietly fails to honour is governance.

G4 is the flagship because Microsoft documents that a non-1:N relationship to a document table makes documents simply not appear: no error, no import failure, no checker warning, no signal of any kind. The only symptom is an empty Documents tab several layers from the cause.

## Extension points

Adding a gate touches exactly three paths and no shared file:

```
harness/DVerse.Harness/Gates/<Name>Gate.cs
harness/DVerse.Harness.Tests/Gates/<Name>GateTests.cs
harness/fixtures/<gid>/{pass,refuse-*}/
```

**Every gate ships with a fixture it refuses.** A gate with no red case is an assertion, not a gate. `WaveOneIntegrationTests` discovers `refuse-*` directories from disk, so a new red fixture is covered automatically and a deleted one shows up as a drop in the executed count.

## Known limits

Stated here rather than discovered by a reader.

**The YAML node names are inferred, not observed.** Every gate parses a shape derived from Microsoft's format documentation. No Dataverse tenant exists yet, so no genuine `pac solution clone` output has ever been seen. The *rules* are quoted from Microsoft's docs and are sound. The *node names* are educated guesses. Confirming them against real clone output is a wave 2 obligation; if the shapes differ, the parsers change and the rules stand.

**Canvas gating rests on Preview tooling.** `pac canvas pack`, `unpack` and `validate` are all marked Preview. The foundation may shift. Canvas gates are kept in their own module so churn stays contained.

**The differentiator is narrower than the original framing.** Not "nobody builds agentic Power Platform tooling" (Microsoft does, publicly, with a funded team) but "nobody has shipped mechanical governance gates over Power Platform declarative artifacts."

## Non-goals

- Competing with Microsoft on solution *generation*. DVerse gates; their marketplace builds.
- Custom connector authoring. Cut in D3.
- Vendoring upstream Microsoft source. Consumed at pinned versions, never copied.
- Subjective quality scoring. Every gate is a structural predicate with a deterministic answer, or it is not a gate.
