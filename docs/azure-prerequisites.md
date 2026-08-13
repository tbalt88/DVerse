# Azure and Microsoft 365 prerequisites

Owner checklist for wave 2. Everything here requires credentials or admin consent, so all of it is yours. None of it can be done by the engineering seat.

**Reality note (2026-08-13):** the environment actually provisioned on 2026-07-28 is **pay-as-you-go**, not the Trial (standard) this checklist describes in section B. Section B's trial mechanics (30 day expiry, no backup/restore, zero cost) do not apply to it; see [provisioning-record.md](provisioning-record.md) for the state that exists. The rest of this checklist (A, C, D, E, F, G) applies unchanged.

Verified 2026-07-31 against Microsoft Learn: the Power Platform OIDC/FIC tutorial, the GitHub Actions for Power Platform tutorial, and the application user documentation. Sources linked per section.

**Do not paste any secret, client secret, password, or token into chat.** The whole point of the federated credential path below is that no secret exists to leak. The only values the seat needs are non-secret identifiers.

## Before you start: the 30 day clock

Creating the Dataverse environment starts a 30 day trial life (D16). Do section B **last**, immediately before we begin wave 2, not while working through sections C to G. Sections C, D and G can all be done first without touching the clock.

## A. Accounts and roles you need

| # | Item | Notes |
|---|---|---|
| A1 | Microsoft Entra tenant | you already have one, tbalt88 identity is separate from this |
| A2 | **Power Platform Administrator** or **Global Administrator** role | required to create the environment and the application user |
| A3 | Permission to create Entra app registrations | some tenants restrict this to admins; check before you get to C |
| A4 | Microsoft 365 subscription with **SharePoint Online** | required for the document management footprint, section F |
| A5 | GitHub repo admin on `tbalt88/DVerse-v2` | you have this |

If A3 is blocked by tenant policy, everything in section C stops until it is lifted. Worth checking first because it is the least visible blocker on this list.

## B. Dataverse environment (do this LAST)

| # | Step | Record this |
|---|---|---|
| B1 | Power Platform admin center, `admin.powerplatform.microsoft.com`, then Manage, Environments, New | |
| B2 | Type: **Trial (standard)**, NOT Trial (subscription-based). The subscription-based type requires an active Dynamics 365 trial subscription and is a different product. For the standard type, answer **Yes** to "Create a database for this environment" | |
| B3 | Region: pick one and record it. `pac solution check` takes a `--geo` argument at run time; keeping them matched avoids cross-geo confusion | region name |
| B4 | **Dynamics 365 apps cannot be enabled on trial-type environments** (Microsoft limitation). Fine for us; DVerse targets Dataverse, not first-party apps | |
| B5 | After creation, open the environment, Details | **Environment ID** (GUID) |
| B6 | Same panel | **Environment URL** (`https://<org>.crm<n>.dynamics.com`) |
| B7 | Note the creation date | **expiry = creation + 30 days** |

B7 is the one people forget. The expiry date drives the golden import receipt deadline in wave 2.7.

Two more standard-trial facts worth knowing before you commit to one:

- A standard trial **can only be created and deleted**. No backup, no restore, no copy, no reset. There is no safety net inside the environment, which is exactly why every artifact lives in git and the environment is treated as disposable.
- After 30 days the environment is disabled and then deleted. Anything not exported to the repo by then is gone.

## C. Entra app registration

Source: [GitHub Actions for Power Platform tutorial](https://learn.microsoft.com/power-platform/alm/tutorials/github-actions-start).

| # | Step | Record this |
|---|---|---|
| C1 | Entra admin center, App registrations, New registration | |
| C2 | Name: `DVerse-v2-ci`. Single tenant. **No redirect URI** | |
| C3 | Overview page | **Application (client) ID** |
| C4 | Overview page | **Directory (tenant) ID** |

### C5. API permissions, all three are required

Add these under API permissions, then grant admin consent.

| API | Where to find it | Type | Permission |
|---|---|---|---|
| **Dynamics CRM** | Microsoft APIs tab | Delegated | `user_impersonation` |
| ~~PowerApps Runtime Service~~ | **DO NOT ADD, see below** | | |
| **PowerApps-Advisor** | **APIs my organization uses**, search for it | Delegated | `Analysis.All` |

**PowerApps Runtime Service is dead, despite Microsoft's tutorial still listing it.** Discovered empirically 2026-08-13: it resolves to service `82f77645-8a66-4745-bcdf-9706824f9ad0`, which Microsoft has renamed "Previous version CDS OBSOLETE - DO NOT USE", and tenant consent fails with AADSTS650052 trying to provision it. Nothing in this project needs it: interactive `pac auth` does not use this app registration, service-principal access to Dataverse is authorized by the application user in section E, and Solution Checker uses the Advisor permission. If it was added, remove it before granting consent.

**If the portal consent button fails with "your organization does not have a subscription (or service principal)":** the tenant lacks local service principals for the listed first-party APIs, which the portal button cannot create. Use the admin consent URL endpoint instead, which provisions them as part of consent: `https://login.microsoftonline.com/<TENANT_ID>/adminconsent?client_id=<CLIENT_ID>`, signed in as Global Administrator. A post-accept error page about a missing reply address is cosmetic; verify the green Status in the portal.

**PowerApps-Advisor / `Analysis.All` is the Solution Checker permission.** Without it, gate G7 cannot run at all. It is the one people miss because it is not on the Microsoft APIs tab; you have to search the organization tab for it.

C6: select **Grant admin consent for \<tenant\>** and confirm all three show green.

**Do not create a client secret.** Section D replaces it. If you accidentally create one, delete it; an unused secret in a repo about governance is a bad look and an expiry waiting to happen.

## D. Federated credential, the no-secret path

Source: [OIDC/FIC tutorial for Power Platform](https://learn.microsoft.com/power-platform/alm/tutorials/github-actions-oidc-fic).

| # | Step |
|---|---|
| D1 | In the app registration: Certificates and secrets, **Federated credentials**, Add credential |
| D2 | Scenario: **GitHub Actions deploying Azure resources** |
| D3 | Organization: `tbalt88`  Repository: `DVerse-v2` |
| D4 | Entity type: **Branch**  Branch: `main` |
| D5 | Name: `dverse-v2-main` |
| D6 | Confirm audience is `api://AzureADTokenExchange` |

### Why branch-scoped rather than workflow-scoped

Microsoft's tutorial uses a `repository, workflow` subject claim, producing `repo:tbalt88/DVerse-v2:workflow:<Name>`. That bakes the workflow *name* into the credential, so renaming a workflow silently breaks auth.

**Branch-scoped** (`repo:tbalt88/DVerse-v2:ref:refs/heads/main`) is better for us for a structural reason: it matches the D8b tier split exactly. Only `main` can obtain a token, so a fork or a feature branch **cannot** reach the tenant even if a workflow is modified to try. The offline tier needs no credential at all, so nothing legitimate is blocked.

Environment-scoped credentials with required reviewers would be stronger still, but GitHub environment protection rules are restricted on free private repositories, the same family of limitation as the branch-protection 403 we already hit. Revisit at the public flip in wave 8.

## E. Application user in Dataverse

Source: [Manage application users](https://learn.microsoft.com/power-platform/admin/manage-application-users).

The app registration alone grants nothing. Dataverse needs a matching **application user**, an unlicensed account bound to the app's service principal.

| # | Step |
|---|---|
| E1 | PPAC, Manage, Environments, select your environment, Settings |
| E2 | Users + permissions, **Application users**, **+ New app user** |
| E3 | **+ Add an app**, pick `DVerse-v2-ci` |
| E4 | Business unit: the environment's root business unit |
| E5 | Security roles: **System Administrator** |
| E6 | Create |

**On System Administrator.** Microsoft's docs are explicit that Solution Checker needs a role carrying the `prvAppendmsdyn_analysisjob` privilege, and System Administrator has it by default. A tighter custom role is the correct production answer, and I would normally push for least privilege here. For a disposable 30 day trial running gates in a private repo it is not worth the setup time, and I would rather spend that time on the golden import receipt. Flagging it so the choice is deliberate rather than accidental.

### Faster alternative for C and E

`pac admin create-service-principal --environment <environment-id>` creates the Entra app **and** the application user in one command.

The catch: it also generates a **client secret** and prints it in clear text, which is the thing section D exists to avoid. If you use it, add the federated credential from section D afterwards and delete the secret. Your call whether the time saved is worth the extra cleanup step.

## F. SharePoint document management

Source: [Manage SharePoint documents](https://learn.microsoft.com/power-pages/configure/manage-sharepoint-documents).

Required for the D15 footprint and for gate G4 to have anything real to gate.

| # | Step |
|---|---|
| F1 | Confirm **SharePoint Online**. Document management does not work with SharePoint on-premises |
| F2 | PPAC, your environment, Settings, Integration, **Document management settings** |
| F3 | Enable **server-based SharePoint integration**. This is the only supported mode |
| F4 | Point it at a SharePoint site collection, a dedicated one is cleaner than reusing an existing site |
| F5 | Record the **SharePoint site URL** |
| F6 | Leave per-table document management **off** for now. Wave 4 enables it on the `dv_` table as a gated change, which is the demonstration |

F6 matters. Enabling it by hand in the portal would mean the harness gates a change it did not observe being made. Wave 4 makes it a declarative, gated change instead.

## G. GitHub side, no tenant needed

| # | Step |
|---|---|
| G1 | Repo Settings, Actions, General: confirm Actions are **enabled** |
| G2 | Workflow permissions: **Read repository contents**, plus **id-token: write**, which the workflow requests per job |
| G3 | Add three **repository variables**, not secrets, since none of these is sensitive |

| Variable | Value |
|---|---|
| `DV_CLIENT_ID` | Application (client) ID from C3 |
| `DV_TENANT_ID` | Directory (tenant) ID from C4 |
| `DV_ENVIRONMENT_URL` | Environment URL from B6 |

**Repository variables, not secrets.** A client ID and tenant ID are identifiers, not credentials, and storing them as secrets makes CI logs unreadable for no security gain. With federated identity there is nothing secret left to store, which is the entire point.

## H. What to hand back to the seat

Non-secret values only. Everything here is safe in chat.

- [ ] Environment ID (GUID)
- [ ] Environment URL
- [ ] Environment creation date, so expiry is on the record
- [ ] Application (client) ID
- [ ] Directory (tenant) ID
- [ ] Power Apps Checker geo, from B3
- [ ] SharePoint site URL
- [ ] Confirmation that all three API permissions show admin consent granted

Never send: client secrets, passwords, tokens, certificate files, or a `pac auth` profile.

## I. How we verify it, together

Once H is delivered, I can run these and confirm the chain end to end. The interactive sign-in is yours; I never handle credentials.

```bash
pac auth create --name dverse-ci --deviceCode --environment <ENVIRONMENT_URL>
```

You complete the device-code prompt in a browser. Then I verify:

```bash
pac auth list && pac org who && pac solution list
```

`pac org who` returning your org confirms A through E. `pac solution list` confirms the application user actually has read access rather than merely existing.

The federated credential from section D cannot be tested locally at all; it only exercises inside a GitHub Actions run. Wave 2.5 is the first thing that proves it, and if D3 to D5 are wrong it fails there with an audience or subject mismatch, which is a clear and fast error rather than a subtle one.

## Cost

Trial environment: free for 30 days. Entra app registration: free. Federated credentials: free. GitHub Actions on a private repo: free tier minutes, and our offline gates are seconds per run. SharePoint: requires an existing M365 subscription, no incremental cost.

**Total incremental spend: nothing**, provided the trial is not converted and A4 already exists.

## Known limits carried in

- Branch protection and rulesets are unavailable on this free private repo. Refusal lives in the harness, not in GitHub, so this constrains nothing. GitHub Pro was declined and is not needed.
- Fork pull requests receive neither secrets nor OIDC tokens. Gate G7 therefore cannot run on them, by design, and the offline tier covers everything DVerse authors. Note this is anticipatory: forking is off by default on private repos, so the fork-PR scenario only becomes live at the wave 8 public flip.
- Everything in section B expires 30 days after creation. The golden import receipt exists so the offline gates keep their evidentiary value after that.
