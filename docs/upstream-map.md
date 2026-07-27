# Upstream map

What DVerse v2 consumes from Microsoft, at what version, and what it deliberately declined.

Assessed 2026-07-27. This document exists because the alternative, vendoring upstream source into this repo, was considered and rejected.

## Why not vendor

Combining Microsoft's Power Platform repos into this one was assessed and rejected for three reasons.

1. **It would not work.** `PowerPlatform-DataverseServiceClient` states in its own README, in bold: "The Dataverse ServiceClient cannot be built outside of Microsoft." It is a labelled Code Replica, published for transparency. Vendoring it yields source that provably will not compile.
2. **It would break the thesis.** This project's claim is receipts a stranger can reproduce. Vendored copies are frozen snapshots that drift from upstream immediately and never receive security patches.
3. **They are not one kind of thing.** These are four incompatible consumption channels plus a dormant index page. Merging them produces an artifact consumable as none of them.

Combined size had they been vendored: 82 MB.

## Consumed

| Upstream | Channel | Pin | Notes |
|---|---|---|---|
| `microsoft/powerplatform-actions` | GitHub Actions git ref | `v1.9.2` | Provides Power Apps Checker as a CI gate. This is the closest existing thing to our harness. Study where it stops. |
| `Microsoft.PowerPlatform.Dataverse.Client` | NuGet | TBD at first use | Source repo is a read-only mirror, unbuildable externally |
| `Microsoft.CrmSdk.CoreAssemblies` | NuGet | `9.0.2.49` | Version inherited from the seed repo's plugin projects |

## Declined

| Upstream | Reason |
|---|---|
| `microsoft/powerplatform-build-tools` | Azure DevOps marketplace extension. Gates run in GitHub Actions, so this is not a dependency. Read only for ADO parity if that is ever wanted. |
| `microsoft/PowerPlatformConnectors` | Out of scope by owner decision 2026-07-27. Connector definitions are an app-integration surface with a different validation model, and pull toward the citizen-developer layer this project argues against. |
| `microsoft/powerplatform` | 0 MB, dormant since 2025-01-27. An index page. |

## Standing obligation

`microsoft/power-platform-skills` pushes daily. A `dataverse-backend` plugin landing upstream would directly overlap this project's territory. Re-check on each planning cycle.
