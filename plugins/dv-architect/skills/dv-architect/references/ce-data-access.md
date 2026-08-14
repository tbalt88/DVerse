# Data Access Reference

> Inherited from the seed `ce-data-access.md`, substantively unchanged: this is
> standard Dataverse SDK/Web API behavior, not something this repo's harness
> checks. Naming examples corrected to this repo's real `dv_` prefix (was the
> seed's `dexx_` placeholder). One addition at the end, `datafieldname`
> casing, is gate-adjacent (found via a live render, not yet mechanically
> checked).

## Web API vs Organization Service — Decision Table

| Scenario | Use | Why |
|---|---|---|
| Inside a plugin or custom workflow activity | **Organization Service** | Web API not supported inside plugins (docs explicit) |
| External .NET app or Azure Function | **ServiceClient** (Org Service SDK) | Same NuGet, full message support, type safety |
| Node.js, Python, non-.NET external | **Web API** (OData v4) | Language-agnostic, open standard |
| Browser/client-side JavaScript | **Web API via Xrm.WebApi** | Secure, session-auth, no token management |
| Power Platform canvas app connector | Dataverse connector | No-code path |

> "Don't try to use the Web API [inside plug-ins] as it isn't supported. Also,
> don't authenticate the user before accessing the web services as the user is
> preauthenticated before plug-in execution." (Write a plug-in)

## Organization Service — Inside Plugin

```csharp
var record = orgService.Retrieve("dv_matter", matterId, new ColumnSet("dv_name", "dv_mattername"));

var newMatter = new Entity("dv_matter");
newMatter["dv_mattername"] = "New Matter";
var newId = orgService.Create(newMatter);

var update = new Entity("dv_matter", matterId);
update["dv_mattername"] = "Updated";
orgService.Update(update);

orgService.Delete("dv_matter", matterId);

var qe = new QueryExpression("dv_matter")
{
    ColumnSet = new ColumnSet("dv_mattername", "dv_openedon"),
    Criteria = new FilterExpression()
};
qe.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
qe.TopCount = 50;
var results = orgService.RetrieveMultiple(qe);
```

## ServiceClient — External .NET

```csharp
using Microsoft.PowerPlatform.Dataverse.Client;

var connectionString =
    "AuthType=ClientSecret;" +
    "Url=https://yourorg.crm.dynamics.com;" +
    "ClientId=YOUR_APP_REGISTRATION_ID;" +
    "ClientSecret=YOUR_SECRET;";

using var client = new ServiceClient(connectionString);
var record = client.Retrieve("dv_matter", matterId, new ColumnSet("dv_mattername"));
```

## Web API (OData v4)

```bash
# GET single record with field selection
GET /api/data/v9.2/dv_matters(GUID)?$select=dv_mattername,dv_openedon

# GET with filter and ordering
GET /api/data/v9.2/dv_matters?$select=dv_mattername&$filter=statecode eq 0&$orderby=dv_mattername asc&$top=50

# POST create
POST /api/data/v9.2/dv_matters
{"dv_mattername":"New Matter"}

# PATCH update (merge; only supplied fields updated)
PATCH /api/data/v9.2/dv_matters(GUID)
{"dv_mattername":"Updated"}

# DELETE
DELETE /api/data/v9.2/dv_matters(GUID)
```

## FetchXML

```xml
<fetch version="1.0" output-format="xml-platform" mapping="logical" distinct="false" top="50">
  <entity name="dv_matter">
    <attribute name="dv_matterid" />
    <attribute name="dv_mattername" />
    <attribute name="dv_openedon" />
    <filter type="and">
      <condition attribute="statecode" operator="eq" value="0" />
    </filter>
    <order attribute="dv_openedon" descending="true" />
  </entity>
</fetch>
```

### Common operators

```
eq, ne, lt, le, gt, ge   — comparison
like, not-like           — wildcard (%value%)
in, not-in                — list membership
null, not-null            — null checks
on-or-after, on-or-before — date comparisons
```

## Client Scripting (Client API)

```javascript
function onFormLoad(executionContext) {
    const formContext = executionContext.getFormContext();
    const status = formContext.getAttribute("dv_status").getValue();
    formContext.getControl("dv_creditlimit").setVisible(status === 1);
}

async function fetchRelatedRecords(primaryId) {
    const result = await Xrm.WebApi.retrieveMultipleRecords(
        "dv_matter",
        `?$select=dv_mattername&$filter=_dv_ownerid_value eq ${primaryId}&$top=10`
    );
    return result.entities;
}
```

## Custom Table Naming Conventions [G2]

| Element | Pattern | Example |
|---|---|---|
| Table schema name | `dv_entityname` | `dv_matter` |
| Column schema name | `dv_columnname` | `dv_mattername` |
| Publisher prefix (`CustomizationPrefix`) | `dv` | no trailing underscore |
| Plugin class | `DVerse.Plugins.OnVerbEntity` | `DVerse.Plugins.MatterNumberValidator` |

Always the `dv_` prefix; never modify an out-of-box table under a foreign
prefix. G2 checks both the publisher's `CustomizationPrefix` value and every
`entities/` directory name.

## FormXml `datafieldname` casing [L14]

Not yet gate-checked, but proven to cost real time: a form control's
`datafieldname` binds by the attribute's LOWERCASE logical name. Any other
casing (PascalCase, the attribute's display-style name) drops the control
SILENTLY at render: pack succeeds, import succeeds, publish succeeds, the
form editor even shows the row. The running app is the only rung that shows
the miss. When authoring FormXml, `datafieldname` must equal the attribute's
`LogicalName` exactly, and any UI-bearing change ends with driving the
rendered form, not just checking the gates.
