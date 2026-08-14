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
| API permissions | Dynamics CRM `user_impersonation`, Microsoft Graph `User.Read`, PowerApps-Advisor `Analysis.All`. PowerApps Runtime Service was added per the stale Microsoft tutorial, then REMOVED: it resolves to "Previous version CDS OBSOLETE" (`82f77645-...`) and blocks consent with AADSTS650052 |
| Admin consent | **GRANTED for DMD LLC on all three, screenshot-verified 2026-08-13.** Portal button failed (tenant lacked first-party service principals); succeeded via the adminconsent URL endpoint after removing the obsolete permission and adding a temporary `https://localhost` redirect URI |
| Federated credential (section D) | **DONE AND CI-PROVEN 2026-08-13** (run 31751910173 rerun + run 31753552568): `pac auth create --githubFederated` succeeded on the ubuntu runner with zero stored secrets. Subject `repo:tbalt88@20543139/DVerse-v2@1314351397:ref:refs/heads/main` (immutable IDs; repo postdates GitHub's 2026-07-15 cutoff). First attempt failed AADSTS70025 because the credential was never saved: the required Name field was empty and the Add silently did not commit. Name: `dverse-v2-main` |
| Client secret | none, by design |

## Dataverse application user (section E)

**DONE, verified by FetchXML query 2026-08-13:** application user `# DexEvoApp` bound to `1f71d5bb-...`, Enabled, role **System Administrator** (role id `c9ad0449-...`). Interactive chain also verified: `pac auth` profile `dverse-ci` connects, `pac org who` returns environment `5700d87e-...` matching this record, `pac solution list` returns 5 solutions. The federated credential remains the one unproven link; only a real GitHub Actions OIDC run can exercise it (wave 2.5). Org unique name `unq530cda77ce8af1119969000d3a5cb`, friendly name `crmdev`.

## SharePoint (section F)

SharePoint Online confirmed, 2026-08-13. Tenant `dmdllc08.sharepoint.com`, admin center `https://dmdllc08-admin.sharepoint.com/`. **Site collection (owner-designated): `https://dmdllc08.sharepoint.com/sites/DMDLLC`** (existence probe: unauthenticated HEAD returns 302 to login, site resolves). F COMPLETE 2026-08-13: owner enabled server-based integration; verified from inside Dataverse (sharepointsite record "Default Site", absoluteurl matching, isdefault Yes, validationstatus Valid). Per-table document management stays OFF (F6); wave 4.3 enables it declaratively as the gated change. Needed by wave 5 runtime behavior; G4 and the 4.3 declarative work do not depend on it.

Note: the owner's pasted URL carried invisible bidirectional text marks (U+200E) around "DMDLLC"; the recorded URL above is the cleaned form.

## GitHub repository variables

Set 2026-08-13 via `gh variable set` (variables, not secrets; nothing here is sensitive):

| Variable | Value |
|---|---|
| `DV_CLIENT_ID` | `1f71d5bb-5f44-4e72-bffa-b382eff9cad7` (updated 2026-08-13 after app ID re-issue) |
| `DV_TENANT_ID` | `a18bf5e0-62a5-4b3b-bb96-8b0cc7d02989` |
| `DV_ENVIRONMENT_URL` | `https://dexevo.crm.dynamics.com/` |

## Remaining before the online lane can run

1. C5/C6: API permissions + admin consent (owner, in progress)
2. D: federated credential, branch-scoped to `main` (owner)
3. E: application user with System Administrator (owner)
4. I: device-code `pac auth` verification (owner signs in, seat verifies)
5. ~~PPAC expiry date confirmation~~ resolved: pay-as-you-go, no expiry

## Wave 5.3 addendum (2026-08-14)

The original document management configuration predated dv_matter, so the
site had libraries only for Account/Contact. The seat created the
`dv_matter` document library on https://dmdllc08.sharepoint.com/sites/DMDLLC
via the SharePoint REST API (list id 8f17643d-504b-4ac1-9ea0-c3b23d808fb5,
BaseTemplate 101), after which the Matter App's Documents tab uploaded
M-0001-engagement-letter.txt and Dataverse auto-created the per-record
folder and SharePointDocumentLocation (d68188be-9e97-f111-b8de-70a8a59a66f9,
relativeurl "First DVerse Matter_06B40B388F97F111B8DC70A8A59A66F9"),
regarding the First DVerse Matter record: the live 1:N behavior gate G4
exists to protect. Receipt: docs/receipts/wave5-3-documents-tab-live-upload.png
