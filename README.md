# DVerse v2

Governed, pro-code agentic development for Microsoft Dataverse.

Part of the DVerse series. Successor to [DVerseClaudeSkills](https://github.com/tbalt88/DVerseClaudeSkills) (d365-architect-v3).

> **Status: day zero.** This repo was scaffolded 2026-07-27. Nothing here is built yet. It is private until the harness can gate its own demo solution. No badges appear below because there is nothing true to badge yet.

## What this is

The Power Platform expresses its systems as declarative artifacts: Dataverse solution XML, FormXML, sitemap, ribbon, FetchXML, workflow XAML, and canvas app sources unpacked to `.pa.yaml`. Declarative means diffable. Diffable means gateable.

DVerse v2 tests whether that property can carry real governance: whether AI DLC gates can run natively against those artifacts and mechanically refuse ungated output, rather than governance being asserted by human review.

Everything here is built by Claude models. No other vendor's models are used.

Three components, all currently empty:

| Path | Component | State |
|---|---|---|
| `skills/` | Evolved d365-architect skill, encoding architect judgment | not started |
| `harness/` | Verification gates over declarative artifacts, refuses ungated output | not started |
| `demo-solution/` | Solution built BY the agent UNDER the harness, with receipts | not started |

## Where this sits in the Microsoft ecosystem

DVerse layers on top of Microsoft's official tooling. It does not replace or vendor it.

[`microsoft/power-platform-skills`](https://github.com/microsoft/power-platform-skills) covers the app layer: canvas apps, model-driven apps, Power Pages, Power Automate, mobile, code apps, MCP apps. That territory is theirs and they ship it daily.

Their plugins help you **build**. DVerse **gates**. That is the distinction, and it holds across the whole surface: nothing in their marketplace mechanically refuses output that violates a rule, and nothing there produces an auditable refusal ledger.

DVerse covers two things their marketplace does not: **governance gates** over Power Platform declarative artifacts, and **Dataverse backend pro-code** (plugin assemblies, custom APIs, workflow activities), which has no plugin of theirs at all.

Scope note, stated honestly: DVerse gates canvas app sources as well as Dataverse solutions. That overlaps their `canvas-apps` territory on the build side. We are not competing on generation; we are adding the layer above it.

See [`docs/upstream-map.md`](docs/upstream-map.md) for exactly what is consumed, at what pinned version, and what was deliberately declined.

## Provenance: inherited vs built

Honesty about what is ours matters more here than anywhere, because this project's claim is auditability.

**Inherited (consumed, not written by us):**
- Power Apps Checker service, via `microsoft/powerplatform-actions`. The Solution Checker gate is Microsoft's, not ours. We compose it.
- `Microsoft.PowerPlatform.Dataverse.Client`, via NuGet. Note that Microsoft states this cannot be built outside Microsoft.
- Dataverse SDK core assemblies, via NuGet.

**To be built here (nothing yet exists):**
- Declarative XML diff validation
- Dependency gates
- Refusal semantics (the mechanical "no" that makes governance structural)
- Ledger and receipt chain

## What this repo does not claim

The differentiator is narrow and specific: **nobody has shipped mechanical governance gates over Power Platform declarative artifacts.** Plenty of tooling helps you build. None of it refuses.

It is not a claim that nobody builds agentic Power Platform tooling. Microsoft does, publicly, with a funded team and a 572-star marketplace. Overstating that would not survive thirty seconds of scrutiny.

Two further limits worth stating before someone finds them:

- Canvas app gating depends on `pac canvas pack/unpack/validate`, which Microsoft currently marks **Preview**. That foundation may shift.
- The Power Apps Checker rung requires a live Dataverse environment and cannot run on fork pull requests. Every gate authored here runs offline with no credentials; that one does not.

## Engineering notes

Practices are marked by how they are enforced, because a written rule and a gate are not the same thing.

| Practice | Enforcement |
|---|---|
| AI DLC gates over declarative artifacts | code-enforced (planned, not yet built) |
| Upstream deps pinned, never vendored | written rule |
| Honest provenance in docs | written rule |
| Public scrub before visibility flip | written rule, checklist in vault |

## Testing

Nothing is tested, because nothing is built. This section will state real numbers or say nothing.

## For hiring managers

This repo argues that agentic development on enterprise platforms can be governed structurally rather than procedurally, using a property specific to Dataverse: its artifacts are declarative. It is built pro-code, by an agent, under gates that refuse ungated output. The receipts are the point, so the machinery is public and the gate logs are real.
