# Provisioning record

State, not procedure. The procedure is [azure-prerequisites.md](azure-prerequisites.md); this file records what actually exists. All values here are non-secret identifiers by design. No client secret exists for this app registration, and none may be created.

Recorded 2026-08-13 from owner handback.

## Dataverse environment

| Item | Value |
|---|---|
| Environment ID | `5700d87e-f783-e347-b2da-46659e769f00` |
| Environment URL | `https://dexevo.crm.dynamics.com/` |
| Region | United States |
| Created | 2026-07-28 15:18 PT |
| **Estimated expiry** | **2026-08-27 15:18 PT** (created + 30 days) |
| Expiry confirmed in PPAC | **NO, owner to read the Details page and confirm** |

**Schedule impact, on the record:** D16 planned environment creation at wave 2 start so the full 30 days would back the tenant work. The environment was actually created 2026-07-28, one day after that decision, so the clock ran unnoticed for two weeks. About 14 days remain as of this record. All tenant-dependent work (G7, schema confirmation 2.6, golden import receipt 2.7) must land inside that window. The mitigation ordering stands, compressed: receipt work goes first once auth is live, not last.

## Entra app registration

| Item | Value |
|---|---|
| Application (client) ID | `31e37971-d28a-f111-8077-70a8a59a66f9` |
| Directory (tenant) ID | `a18bf5e0-62a5-4b3b-bb96-8b0cc7d02989` |
| API permissions granted | **pending, owner in progress (C5/C6)** |
| Federated credential (section D) | **pending** |
| Client secret | none, by design |

## Dataverse application user (section E)

**Pending.** Environment exists, so this can be done immediately after C5/C6 consent.

## SharePoint (section F)

**Not provisioned yet.** Owner deferred. Needed by wave 4 (document-management footprint), not by G7 or the import receipt. The G4 gate is offline and does not depend on it.

## GitHub repository variables

Set 2026-08-13 via `gh variable set` (variables, not secrets; nothing here is sensitive):

| Variable | Value |
|---|---|
| `DV_CLIENT_ID` | `31e37971-d28a-f111-8077-70a8a59a66f9` |
| `DV_TENANT_ID` | `a18bf5e0-62a5-4b3b-bb96-8b0cc7d02989` |
| `DV_ENVIRONMENT_URL` | `https://dexevo.crm.dynamics.com/` |

## Remaining before the online lane can run

1. C5/C6: API permissions + admin consent (owner, in progress)
2. D: federated credential, branch-scoped to `main` (owner)
3. E: application user with System Administrator (owner)
4. I: device-code `pac auth` verification (owner signs in, seat verifies)
5. PPAC expiry date confirmed and corrected above if different
