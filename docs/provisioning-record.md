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
| Billing model | **Pay-as-you-go** (Azure subscription linked), owner correction 2026-08-13 |
| Expiry | **None.** Not a trial; no 30 day clock |

**Correction on the record (2026-08-13):** the first version of this file recorded the environment as a 30 day trial expiring 2026-08-27 and reordered tenant work around that deadline. The owner corrected it: this is a pay-as-you-go environment, not a trial. Consequences: no expiry deadline exists; risk R2 (trial expiry kills G7) is retired; backup, restore and copy operations are available, unlike a standard trial; and the running cost is usage-billed to the owner's Azure subscription rather than free, so the checklist's "zero incremental spend" line does not apply to the path actually taken. The golden import receipt (2.7) remains required by the receipts doctrine, but is no longer deadline-driven. D16's deferred-creation logic is moot.

## Entra app registration

| Item | Value |
|---|---|
| Application (client) ID | `1f71d5bb-5f44-4e72-bffa-b382eff9cad7` (supersedes `31e37971-...` recorded earlier on 2026-08-13; owner re-issued the app ID same day) |
| Directory (tenant) ID | `a18bf5e0-62a5-4b3b-bb96-8b0cc7d02989` |
| API permissions | added: Dynamics CRM `user_impersonation`, PowerApps Runtime Service `user_impersonation`, PowerApps-Advisor `Analysis.All` (screenshot-verified 2026-08-13) |
| Admin consent | **pending, Status column blank; owner to click Grant admin consent** |
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
5. ~~PPAC expiry date confirmation~~ resolved: pay-as-you-go, no expiry
