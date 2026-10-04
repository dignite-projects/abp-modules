using System;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Volo.Abp;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.ExceptionHandling;
using Volo.Abp.Validation;

namespace Dignite.Abp.AspNetCore.Mcp.Errors;

/// <summary>
/// Turns an exception thrown by any tool on the server into a result an AI client can act on.
/// <para>
/// <b>It reuses ABP's own <see cref="IExceptionToErrorInfoConverter"/></b> - the same converter the HTTP
/// API uses - rather than reading each exception type by hand. That converter already knows which
/// exceptions are safe to surface, already resolves error codes through their localization resources,
/// and already lifts <c>ValidationResult.MemberNames</c> into a structured list. Re-deriving any of that
/// here would be a second, drifting copy of the error contract.
/// </para>
/// <para>
/// <b>Failures come back as <see cref="CallToolResult"/> with <c>IsError</c>, not as a thrown
/// <c>McpException</c>.</b> A protocol error tells the client's transport something went wrong; an error
/// <i>result</i> is handed to the model as the outcome of the call it made, which is what lets it fix the
/// argument and try again within the same turn.
/// </para>
/// </summary>
public static class McpToolErrorFilter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Wraps every tool invocation. Registered once, by <see cref="AbpAspNetCoreMcpModule"/>, so no tool
    /// carries error-shaping code of its own.
    /// <para>
    /// <b>Note what this deliberately does not attempt.</b> A failed tool call can still leave a partially
    /// applied write behind: ABP's <c>AuditingInterceptor</c> calls <c>SaveChangesAsync</c> from a
    /// <c>finally</c> on the way out of a failed application-service call, so the flush has already
    /// happened by the time this runs. That is framework behaviour shared with the HTTP API - an MVC action
    /// failing persists exactly as much - so rolling back here would be theatre.
    /// </para>
    /// </summary>
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> CallToolFilter(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next)
    {
        return async (request, cancellationToken) =>
        {
            try
            {
                return await next(request, cancellationToken);
            }
            // Rethrown, not reported, exactly as the SDK's own tool pipeline rethrows them:
            // - OperationCanceledException: reporting a disconnected client as a "successful" response
            //   would return cleanly to the unit-of-work middleware, which then tries to complete on an
            //   already-cancelled token.
            // - McpProtocolException: a protocol-level failure (an unknown tool, malformed arguments) that
            //   the client's transport is meant to see as a JSON-RPC error, not a tool outcome.
            // - InputRequiredException: not a failure at all - it is how a tool asks the client for more
            //   input mid-call (MRTR), and converting it would break that flow.
            catch (Exception exception) when (exception is not (OperationCanceledException or McpProtocolException or InputRequiredException))
            {
                return Create(exception, request.Services);
            }
        };
    }

    public static CallToolResult Create(Exception exception, IServiceProvider? services)
    {
        var error = Describe(exception, services);
        return new CallToolResult
        {
            IsError = true,
            StructuredContent = JsonSerializer.SerializeToElement(
                new McpToolErrorEnvelope { Error = error }, SerializerOptions),
            // The same information again as plain text. Not a fallback to stringifying - the structured
            // payload above is authoritative - but clients are not obliged to surface structuredContent
            // to the model, and a model that cannot see the error cannot correct it.
            Content = { new TextContentBlock { Text = Describe(error) } }
        };
    }

    private static McpToolError Describe(Exception exception, IServiceProvider? services)
    {
        var error = new McpToolError { Kind = KindOf(exception) };

        // The SDK's own way for a tool to address the client: its message is written for the caller and
        // the SDK passes it through. ABP's converter knows nothing of it and would replace it with "An
        // internal error occurred", so it is taken as-is.
        if (exception is McpException)
        {
            error.Message = exception.Message;
            return error;
        }

        // Resolved from the request scope, and only there. A caller that invokes a tool outside a
        // request (a unit test) simply gets the exception's own message instead of a localized one.
        var converter = services?.GetService<IExceptionToErrorInfoConverter>();
        if (converter != null)
        {
            var info = converter.Convert(exception, options => options.SendExceptionsDetailsToClients = false);
            error.Code = info.Code;
            error.Message = info.Message ?? exception.Message;
            error.Details = info.Details;

            if (info.ValidationErrors != null)
            {
                error.ValidationErrors = info.ValidationErrors
                    .Select(validationError => new McpToolValidationError
                    {
                        Message = validationError.Message ?? string.Empty,
                        Fields = validationError.Members?.ToList() ?? new()
                    })
                    .ToList();
            }

            return error;
        }

        error.Message = exception.Message;
        error.Code = (exception as IHasErrorCode)?.Code;

        if (exception is AbpValidationException validationException)
        {
            error.ValidationErrors = validationException.ValidationErrors
                .Select(validationError => new McpToolValidationError
                {
                    Message = validationError.ErrorMessage ?? string.Empty,
                    Fields = validationError.MemberNames.ToList()
                })
                .ToList();
        }

        return error;
    }

    /// <summary>
    /// Classifies the failure into the handful of things a client can do differently. Order matters:
    /// the specific types are tested before <see cref="IBusinessException"/>, which several of them also
    /// implement, and an exception's own say (<see cref="IHasMcpToolErrorKind"/>) comes first of all.
    /// </summary>
    private static string KindOf(Exception exception)
    {
        return exception switch
        {
            IHasMcpToolErrorKind hasKind => hasKind.McpToolErrorKind,
            AbpValidationException => McpToolErrorKinds.Validation,
            EntityNotFoundException => McpToolErrorKinds.NotFound,
            AbpAuthorizationException => McpToolErrorKinds.Forbidden,
            IBusinessException => McpToolErrorKinds.Conflict,
            _ => McpToolErrorKinds.Unknown
        };
    }

    private static string Describe(McpToolError error)
    {
        if (error.ValidationErrors.Count == 0)
        {
            return error.Code == null ? error.Message : $"{error.Message} [{error.Code}]";
        }

        var fields = string.Join("; ", error.ValidationErrors.Select(
            validationError => validationError.Fields.Count == 0
                ? validationError.Message
                : $"{string.Join(", ", validationError.Fields)}: {validationError.Message}"));

        return $"{error.Message} ({fields})";
    }
}
