```json
//[doc-seo]
{
    "Description": "How the ABP MCP server turns exceptions into structured tool errors, and runs every tool call, resource read and prompt in its own unit of work."
}
```

# Errors and Unit of Work

## Errors

Any exception a tool throws becomes a `CallToolResult` with `isError: true` and a structured payload, built with ABP's own `IExceptionToErrorInfoConverter`, so the message is the one an HTTP client would get:

````json
{
  "error": {
    "kind": "validation",
    "code": null,
    "message": "Your request is not valid!",
    "validationErrors": [ { "message": "The Name field is required.", "fields": [ "name" ] } ]
  }
}
````

`kind` tells the model what it can do about the error:

| Kind | Exception | Meaning |
|---|---|---|
| `validation` | `AbpValidationException`, or arguments that do not fit the tool's parameters (see below) | The arguments are not valid; fix them and call again. |
| `notFound` | `EntityNotFoundException` | The entity does not exist. |
| `conflict` | A business exception (`IBusinessException`) | A business rule refused the call. |
| `forbidden` | `AbpAuthorizationException` | The user lacks a permission; do not retry. |
| `unknown` | Anything else | An internal error. |

### Arguments That Do Not Fit the Tool

A call whose arguments cannot be bound to the tool method's parameters (a required argument is missing, a value has the wrong type) is a `validation` error too, and `validationErrors` names the arguments. The [MCP specification](https://modelcontextprotocol.io/specification/2026-07-28/server/tools#error-handling) (since `2025-11-25`) makes input validation errors tool execution errors, so that the model sees them and calls again, and keeps protocol errors for requests that are malformed as a whole. The SDK itself answers a failed binding with "An error occurred invoking '*tool*'.", which names nothing.

````json
{
  "error": {
    "kind": "validation",
    "message": "Your request is not valid!",
    "validationErrors": [
      { "message": "The 'templateName' argument is required.", "fields": [ "templateName" ] },
      { "message": "The 'template' argument is not a parameter of this tool.", "fields": [ "template" ] }
    ]
  }
}
````

The request is compared with the tool's input schema, and what is found is named in `fields` (a member of an argument by its path, such as `booking.guests[1].age`):

| Problem | Message |
|---|---|
| A required argument is absent | The 'x' argument is required. |
| An argument is not a parameter of the tool | The 'x' argument is not a parameter of this tool. |
| A value has the wrong JSON type | The 'x' argument must be an integer. |
| A value is outside the enum | The 'x' argument must be one of: Draft, Published. |
| A string is not in the declared format (`uuid`, `date-time`, `date`, `time`, `duration`, `uri`) | The 'x' argument must be a GUID. |

At most ten are named. Neither the value that was sent nor the text of the exception is repeated, since the former may be large or private and the latter carries .NET type names. When the comparison finds nothing to name (a constraint the schema check does not model), the call is still a `validation` error, with no field and the message "The arguments do not match the tool's input schema.".

The comparison runs only after the binder has failed, and is never stricter than the binder: a number written as a string is accepted for a number, as the SDK's serializer options accept it, so such a call runs. The failure is recognized as the binder's by the exception (`JsonException`, or an `ArgumentException` with the parameter name `arguments`) and by where it was thrown, `Microsoft.Extensions.AI`, not by the type alone: a tool that throws either exception itself, such as one that reads a response that is not JSON, has a bug of its own and stays `unknown`.

An exception whose type would be classified wrongly, typically a `UserFriendlyException` that means "not found", implements `IHasMcpToolErrorKind` to say which kind it is. The kinds are constants of the `McpToolErrorKinds` class.

The SDK's own exceptions keep the SDK's meaning: the message of an `McpException` is passed through as written, while `McpProtocolException` (a protocol-level failure, such as an unknown tool) and `InputRequiredException` (a tool asking the client for more input) are not converted at all.

Every exception reported this way is also logged, and passed to `IExceptionNotifier`, as ABP's exception handling middleware does for the HTTP API, honoring `AbpExceptionHandlingOptions.ExcludeExceptionFromLoggerSelectors`. The client of an unexpected failure is only told that an internal error occurred, so the log is where its cause is found. See [Exception Handling](https://abp.io/docs/latest/framework/fundamentals/exception-handling).

## Unit of Work

Each tool call, resource read and prompt runs in a [unit of work](https://abp.io/docs/latest/framework/architecture/domain-driven-design/unit-of-work) of its own, completed - the changes saved and the transaction committed - **before the result is returned**, and rolled back if the call fails.

Without it, the changes would only be saved once the whole pipeline had returned, after the SDK had already written the result, so a save that fails on a unique index or a concurrency conflict would cut off the client's connection instead of coming back as the call's error.

The commit is not left to ABP's unit-of-work middleware at the end of the request, as it is for an MVC action. The result is one event of a Streamable HTTP response, and a client may close the stream as soon as it has read it; the request is then aborted, and a commit still to come would be cancelled and the changes rolled back after the client was told the call succeeded. A call the client cancels before its result is written is rolled back.

For the same reason the middleware is kept off the MCP endpoints, as it keeps itself off a SignalR hub request: the module adds each endpoint's route to `AbpAspNetCoreUnitOfWorkOptions.IgnoredUrls`. That list matches by prefix, so a route is added up to its first parameter (`/t/{tenant}/mcp` adds `/t`), and a route that starts with a parameter is not added; calls at such a route still commit before their result is written, but the middleware's own unit of work - begun by the first repository call of the request, such as the permission check - fails to commit when the client leaves early, and logs an error.

* Tool calls are **transactional** by default, as a POST is.
* Resource reads and prompts are not, as a GET is not.

`AbpUnitOfWorkDefaultOptions` decides either way.

## See Also

* [Contributing Tools](contributing-tools.md)
* [Hosting the Server](hosting.md)
