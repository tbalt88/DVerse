# Integration Reference

> Inherited from the seed `ce-integration.md`, substantively unchanged: this is
> standard Dataverse platform behavior (Service Bus, webhooks, virtual
> entities, async service), not something this repo's harness checks yet. No
> gate exists over integration components; every rule below is spec-only.
> Naming examples corrected to `dv_`.

## Integration Decision Tree

```
Need to react to a Dataverse event externally?
├── Near-real-time + guaranteed delivery -> Azure Service Bus (topic/queue)
├── Near-real-time + lightweight HTTP endpoint -> Webhook
├── Fire-and-forget side effect, no external -> Async PostOp Plugin
└── External data surfaced inside Dataverse -> Virtual Entity

Need to push data INTO Dataverse from external?
├── .NET app/service -> ServiceClient (Org Service SDK)
├── Non-.NET / language-agnostic -> Web API (OData v4)
└── High-volume batch -> Web API + $batch or ExecuteMultiple (external only)
```

## Azure Service Bus Integration

Reliable async integration to external systems with guaranteed delivery and
retry. Register a Service Endpoint via the Plug-in Registration Tool or
platform-mirror the shape the same way plugin steps are mirrored [L4]:
designation OneWay/TwoWay/Topic/EventHub, SAS key from Azure, message format
XML/JSON/DotNetBinary. `RemoteExecutionContext` carries the same properties as
`IPluginExecutionContext`.

## Webhooks

Simpler, best-effort alternative to Service Bus: Dataverse POSTs the
execution context as JSON to an HTTPS endpoint. No delivery guarantee, lower
complexity, receiver uptime required.

| | Webhook | Service Bus |
|---|---|---|
| Delivery guarantee | No (best-effort) | Yes (retry + dead-letter) |
| Receiver uptime required | Yes | No |
| Complexity | Low | Medium |

## Virtual Entities

Surface external data inside Dataverse without replicating it: no storage
cost, no sync. Read-only by default; writable with a custom provider
implementing `IPlugin` on `Retrieve`/`RetrieveMultiple`, registered at
`MainOperation` (30), the only valid stage for a virtual entity provider.

Caveats: hard 30-second timeout on external calls, no offline support, not
supported in workflows or business rules (read-only in automation),
availability tied to the external system.

## Asynchronous Service

PostOperation async plugins and workflows run via the Dataverse Async
Service, outside the database transaction; a more lenient time limit than
sync plugins, but still bounded. Use for: side effects that don't need to be
atomic with the main operation, work touching records created in a separate
pipeline, external calls that must happen post-commit.

## Access External Web Resources from Plugins

Only in async PostOp, never in sync PreVal/PreOp/sync PostOp:

```csharp
using (var client = new HttpClient())
{
    client.Timeout = TimeSpan.FromSeconds(15);   // explicit timeout required
    var request = new HttpRequestMessage(HttpMethod.Post, "https://api.external.com/data");
    request.Headers.ConnectionClose = true;        // KeepAlive = false
    var response = await client.SendAsync(request);
    response.EnsureSuccessStatusCode();
}
```
