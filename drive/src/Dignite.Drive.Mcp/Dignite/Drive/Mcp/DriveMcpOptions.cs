namespace Dignite.Drive.Mcp;

/// <summary>Options of the Drive's MCP tools.</summary>
public class DriveMcpOptions
{
    /// <summary>
    /// The address the Drive is reached at, which the links of <c>drive_create_upload_url</c> start with. <c>null</c>:
    /// the address the MCP request came in on - right when the MCP server runs in the Drive's own host. Set it when the
    /// Drive is a service of its own, reached through <c>Dignite.Drive.HttpApi.Client</c>.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// The largest file <c>drive_upload_file</c> takes, in bytes; the container's own cap applies too. Its bytes arrive
    /// base64-encoded, a third larger, in one JSON-RPC message: the default, 2 MB, fits the MCP endpoint's default body
    /// limit (<c>AbpMcpServerOptions.MaxRequestBodySize</c>, 4 MB). Raise that limit as well to raise this one.
    /// </summary>
    public long MaxInlineUploadSize { get; set; } = 2 * 1024 * 1024;
}
