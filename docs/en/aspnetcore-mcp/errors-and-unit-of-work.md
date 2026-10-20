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
| `validation` | `AbpValidationException` | The arguments are not valid; fix them and call again. |
| `notFound` | `EntityNotFoundException` | The entity does not exist. |
| `conflict` | A business exception (`IBusinessException`) | A business rule refused the call. |
| `forbidden` | `AbpAuthorizationException` | The user lacks a permission; do not retry. |
| `unknown` | Anything else | An internal error. |

An exception whose type would be classified wrongly, typically a `UserFriendlyException` that means "not found", implements `IHasMcpToolErrorKind` to say which kind it is. The kinds are constants of the `McpToolErrorKinds` class.

The SDK's own exceptions keep the SDK's meaning: the message of an `McpException` is passed through as written, while `McpProtocolException` (a protocol-level failure, such as an unknown tool) and `InputRequiredException` (a tool asking the client for more input) are not converted at all.

Every exception reported this way is also logged, and passed to `IExceptionNotifier`, as ABP's exception handling middleware does for the HTTP API, honoring `AbpExceptionHandlingOptions.ExcludeExceptionFromLoggerSelectors`. The client of an unexpected failure is only told that an internal error occurred, so the log is where its cause is found. See [Exception Handling](https://abp.io/docs/latest/framework/fundamentals/exception-handling).

## Unit of Work

Each tool call, resource read and prompt runs in the [unit of work](https://abp.io/docs/latest/framework/architecture/domain-driven-design/unit-of-work) an MVC action would get: it takes over the unit of work reserved for the request (or begins its own, in an application without that middleware), saves the changes **before the result is returned**, and rolls back if the call fails.

Without it, the changes would only be saved once the whole pipeline had returned, after the SDK had already written the result, so a save that fails on a unique index or a concurrency conflict would cut off the client's connection instead of coming back as the call's error.

* Tool calls are **transactional** by default, as a POST is.
* Resource reads and prompts are not, as a GET is not.

`AbpUnitOfWorkDefaultOptions` decides either way. As for an MVC action, committing the transaction itself stays with the middleware at the end of the request.

## See Also

* [Contributing Tools](contributing-tools.md)
* [Hosting the Server](hosting.md)
