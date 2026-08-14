# Security Model Reference

> Inherited from the seed `ce-security.md`, substantively unchanged: this is
> the standard Dataverse three-layer security model, not something this
> repo's harness checks yet. No gate exists over security roles, BU hierarchy,
> or field-level security; every rule below is spec-only. Naming examples
> corrected to `dv_`.

## Three-Layer Security Model

| Layer | Scope | Mechanism |
|---|---|---|
| **Role-Based** | Entity-level CRUD + privileges | Security Roles -> Users or Teams |
| **Record-Based** | Row-level ownership + sharing | Record Owner; Share; Access Teams |
| **Field-Level (FLS)** | Column-level read/write | Field Security Profiles -> Users or Teams |

## Role-Based Security

| Access Level | Scope |
|---|---|
| User | Records owned by the user |
| Business Unit | Records owned by users in the same BU |
| Parent: Child BUs | Records owned by users in the BU and all child BUs |
| Organization | All records in the org |

Assign roles to Teams, not individual users: AAD Group -> Dataverse Team ->
Security Role(s). Owner Teams inherit roles; Access Teams are per-record
sharing with no inherited roles.

BU structure is very hard to change after records exist: every record is
owned by a user in a BU, so moving records requires manual bulk reassignment.
Design and sign off the BU hierarchy before go-live.

## Record-Based Security

1. **Ownership**: every record has an Owner (user or team); the Owner always
   has full access regardless of role access level.
2. **Sharing**: `GrantAccessRequest` with a `PrincipalAccess` mask.
3. **Access Teams**: per-record, dynamic; no role inheritance.

## Field-Level Security (FLS)

Restricts read/write on specific columns regardless of role-based access.
Enable on the column, create a Field Security Profile, set Read/Create/Update
permissions per field, add Users or Teams. FLS is platform-enforced; plugins
cannot bypass it and run as the executing UserId, so a service account
missing an FSP assignment reads secured fields as null, not as an error.

## Hierarchical Security

Optional add-on: Position hierarchy or Manager hierarchy
(`SystemUser.manager_id` chain), max depth 3. Use only when the org has deep
management reporting needs; most implementations use regional BU + teams
instead.

## Service Principal / Application User Security

```bash
az ad app create --display-name "DVerse-Pipeline-SP"
az ad sp create --id <appId>
az ad app credential reset --id <appId> --append
# Dataverse: Settings > Security > Application Users > New Application User
```

System Administrator only during initial deployment setup; swap to a custom
"Deployment" role (solution import/export, plugin assembly read/write, web
resource read/write) after bootstrap. This repo's live tenant path is OIDC
federation instead of a stored client secret; see `ce-bootstrap.md`.

## Anti-Patterns

| Anti-pattern | Problem | Fix |
|---|---|---|
| Security Roles assigned directly to users | Unmanageable at scale | Assign to Teams; sync AAD groups |
| System Administrator for service accounts | Maximum blast radius on breach | Custom least-privilege role |
| Single monolithic Security Role | Hard to audit | Persona-based roles |
| Modifying OOB Security Roles | Overwritten on solution update | Copy OOB role, modify the copy |
| FLS on plugin-read fields without FSP on the app user | Plugin reads null, fails silently | Add app user to the FSP |
| Designing BU hierarchy post-go-live | Requires mass reassignment | Freeze BU hierarchy before go-live |
