# Environment Bootstrap Reference

> Corrected from the seed `ce-bootstrap.md`: publisher prefix example, the
> `.gitignore` template (dropped `*.snk`), and the scaffold layout (sibling
> test project, YAML solution format) are updated to this repo's proven
> reality. The AAD/PRT/security bootstrap steps are unchanged Dataverse
> platform behavior.

## Phase 1 — AAD App Registration (service principal)

```bash
az login
az ad app create --display-name "DVerse-SP"
APP_ID=$(az ad app list --display-name "DVerse-SP" --query "[0].appId" -o tsv)
az ad sp create --id $APP_ID
az ad app credential reset --id $APP_ID --append
# In Dataverse: Settings -> Security -> Application Users -> New Application User
# Assign System Administrator ONLY for initial setup; swap to a minimum-privilege
# custom "Deployment" role after bootstrap.
```

This repo's live tenant path is OIDC federation, not a stored client secret:
zero secrets in CI, GitHub's own immutable subject as the trust anchor. See
`docs/azure-prerequisites.md` and `docs/provisioning-record.md` for the actual
recorded configuration; the client-secret flow above is the general-purpose
fallback, not what this repo runs online.

## Phase 2 — Publisher and Solution Setup [G2]

```bash
pac auth create --url https://yourorg.crm.dynamics.com \
  --applicationId $APP_ID --clientSecret $CLIENT_SECRET --tenant $TENANT_ID --name dev
pac org who
```

**Publisher prefix, this repo's actual value, not an illustrative placeholder:**

| Setting | Value here | Rule |
|---|---|---|
| Prefix | `dv` | 2-8 lowercase letters; used for `CustomizationPrefix` |
| Derived schema prefix | `dv_` | prefix + underscore; every custom table/column/relationship name |

Once the prefix is in use on live records, it cannot be changed. G2 checks
both the `CustomizationPrefix: dv` value in `publisher.yml` and the `dv_`
directory-name prefix under `entities/`; a wrong prefix imports cleanly and
only surfaces later as a naming collision.

## Phase 3 — Plugin Project Scaffold [L9, L15]

```bash
mkdir -p demo-solution/plugins/<AssemblyName> && cd demo-solution/plugins/<AssemblyName>
dotnet new classlib -n <AssemblyName> --framework net462
dotnet add package Microsoft.CrmSdk.CoreAssemblies

# Strong-name sign; commit the key, do not gitignore it (Sandbox requires a
# public key token; the seed's own defect was hiding this file from git, not
# signing itself)
sn -k <AssemblyName>.snk
# <AssemblyOriginatorKeyFile><AssemblyName>.snk</AssemblyOriginatorKeyFile>
# <SignAssembly>true</SignAssembly>

# Test project: a SIBLING directory, never nested inside the plugin project
mkdir -p demo-solution/plugins/<AssemblyName>.Tests && cd ../<AssemblyName>.Tests
dotnet new xunit -n <AssemblyName>.Tests --framework net462
dotnet add reference ../<AssemblyName>/<AssemblyName>.csproj
```

G6 discovers every `*.csproj` under the solution root independently, classifies
each as test or non-test via `IsTestProject`, and builds or tests it
accordingly; the sibling layout is what lets it find both halves as separate
projects rather than one project's tests going unrun inside another's build.

## Phase 4 — First Solution Export to Source Control

```bash
mkdir -p .github/workflows demo-solution/{solutions,publishers,entities}
```

`.gitignore` (corrected: no `*.snk`, no `*.zip` blanket rule):

```
bin/
obj/
*.dll
export/
dist/
*.user
.vs/
```

```bash
pac solution export --name DVerseCore --path ./export/DVerseCore.zip --managed false
pac solution unpack --zipfile ./export/DVerseCore.zip \
  --folder ./demo-solution --packagetype Unmanaged
git add demo-solution/
git commit -m "feat: initial solution scaffold"
```

## Phase 5 — GitHub Repository Setup

```bash
gh secret set PP_APP_ID --body "$APP_ID"
gh secret set PP_CLIENT_SECRET --body "$CLIENT_SECRET"
gh secret set PP_TENANT_ID --body "$TENANT_ID"
gh variable set DEV_ENV_URL --body "https://yourorg-dev.crm.dynamics.com"
```

Prefer OIDC federation (no stored secret) over a long-lived client secret for
any new pipeline; that is what this repo's own online CI tier does. See
`docs/azure-prerequisites.md`.

## Phase 6 — Plug-in Registration: platform-mirror, not PRT-then-guess [L4, L15]

```bash
pac tool prt   # interactive; fine for exploration
```

For anything this repo will keep, do not hand-transcribe a shape from PRT or
from documentation. Register once (a seat-only tenant write), then:

```bash
pac solution clone --name DVerseCore   # read-only; canonical XML back
```

Transcribe the platform's own canonical element and attribute set into YAML
source. Four distinct import rejections were absorbed one at a time before
this became standing procedure; see `ce-plugin-dev.md`'s "DVerse v2 additions"
for the specific rungs (FullName schemaName, leading-slash part URI,
mandatory strong-naming).

## Phase 7 — Dev Tooling Verification Checklist [L5]

```bash
pac --version && dotnet --version && git --version && gh --version
```

A runtime is present only when it has actually EXECUTED something; `which`/
`Get-Command` can resolve a Windows Store stub for `python` or `node` that
then fails on first real invocation. Run `--version` (or equivalent) and
require exit 0, never presence-on-PATH alone.

```bash
pac org who
pac solution list
```

## Common First-Day Mistakes (corrected for this repo)

| Mistake | Impact | Prevention |
|---|---|---|
| Wrong publisher prefix | All schema names wrong; cannot rename | `CustomizationPrefix: dv` before any customization; G2 checks it |
| Gitignoring the strong-name key | Sandbox registration import fails, invisible until that rung | Commit `*.snk`; see `ce-alm.md`'s corrected `.gitignore` |
| Nesting the test project inside the plugin project | G6 either misses the tests or misclassifies the project | Sibling directories always |
| Skipping `--activate-plugins` on import | Steps land DISABLED, everything else reports success | Always pass the flag on any solution carrying steps [L17] |
| Trusting a documented YAML manifest shape without decompiling | Pack exits 0 over a component the packer silently dropped | Decompile-before-parse, platform-mirror before authoring [L4] |
| Deploying unmanaged to Prod | Unlocked solution | Always import managed to Test/Prod |
