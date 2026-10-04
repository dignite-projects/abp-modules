using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Dignite.Abp.AspNetCore.Mcp.Errors;

/// <summary>
/// The structured payload every failed tool call on the server returns, whichever module the tool
/// belongs to.
/// <para>
/// This exists so an AI client can <i>correct itself</i>. Flattening a validation failure into one
/// English sentence throws away the only part a model can act on - which field, and why - and turns a
/// one-turn fix into a guessing game. ABP's validation already produces that detail; this carries it out
/// intact. It is one shape for the whole server because a request filter is server-wide: two modules
/// each shaping errors their own way would mean whichever filter ran innermost won.
/// </para>
/// </summary>
public class McpToolErrorEnvelope
{
    [JsonPropertyName("error")]
    public McpToolError Error { get; set; } = new();
}

public class McpToolError
{
    /// <summary>
    /// What kind of failure this is, so a client can decide what to do without parsing the message -
    /// one of <see cref="McpToolErrorKinds"/>.
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = McpToolErrorKinds.Unknown;

    /// <summary>
    /// The domain error code where there is one, e.g. <c>Site:040001</c> for a duplicate slug. Stable
    /// across languages, unlike <see cref="Message"/>.
    /// </summary>
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public string? Details { get; set; }

    /// <summary>Per-field failures. Empty for anything other than a validation failure.</summary>
    [JsonPropertyName("validationErrors")]
    public List<McpToolValidationError> ValidationErrors { get; set; } = new();
}

public class McpToolValidationError
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The member names this message is about - <c>ValidationResult.MemberNames</c>, carried through
    /// unchanged, so the client knows precisely which argument to change.
    /// </summary>
    [JsonPropertyName("fields")]
    public List<string> Fields { get; set; } = new();
}

/// <summary>The values of <see cref="McpToolError.Kind"/>.</summary>
public static class McpToolErrorKinds
{
    /// <summary>Fix the fields named in <c>validationErrors</c> and retry.</summary>
    public const string Validation = "validation";

    /// <summary>Something the call named does not exist; re-read whatever lists the valid names.</summary>
    public const string NotFound = "notFound";

    /// <summary>A business rule refused the call, such as a duplicate slug.</summary>
    public const string Conflict = "conflict";

    /// <summary>The caller lacks a permission; do not retry.</summary>
    public const string Forbidden = "forbidden";

    public const string Unknown = "unknown";
}

/// <summary>
/// Lets an exception state its own <see cref="McpToolError.Kind"/> where its type alone would classify it
/// wrongly - typically a <c>UserFriendlyException</c> (needed so its message reaches the model) that
/// actually means <see cref="McpToolErrorKinds.NotFound"/> rather than a business-rule
/// <see cref="McpToolErrorKinds.Conflict"/>.
/// </summary>
public interface IHasMcpToolErrorKind
{
    string McpToolErrorKind { get; }
}
