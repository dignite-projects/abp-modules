namespace Dignite.Abp.BlobManagement.Mcp;

/// <summary>Options of Blob Management's MCP tools.</summary>
public class BlobManagementMcpOptions
{
    /// <summary>
    /// The address Blob Management is reached at, which the links of <c>blob_create_upload_url</c> start with. <c>null</c>:
    /// the address the MCP request came in on - right when the MCP server runs in Blob Management's own host. Set it when the
    /// Blob Management is a service of its own, reached through <c>Dignite.Abp.BlobManagement.HttpApi.Client</c>.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// The largest file <c>blob_upload_file</c> takes, in bytes; the container's own cap applies too. Its bytes arrive
    /// base64-encoded, a third larger, in one JSON-RPC message: the default, 2 MB, fits the MCP endpoint's default body
    /// limit (<c>AbpMcpServerOptions.MaxRequestBodySize</c>, 4 MB). Raise that limit as well to raise this one.
    /// </summary>
    public long MaxInlineUploadSize { get; set; } = 2 * 1024 * 1024;
}
