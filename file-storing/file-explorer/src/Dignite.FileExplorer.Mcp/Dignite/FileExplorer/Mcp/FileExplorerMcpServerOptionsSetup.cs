using Dignite.Abp.AspNetCore.Mcp;
using Microsoft.Extensions.Options;

namespace Dignite.FileExplorer.Mcp;

/// <summary>
/// Raises the MCP endpoint's request-body limit (<see cref="AbpMcpServerOptions.MaxRequestBodySize"/>) just
/// enough for <c>file_explorer_upload_file</c> to carry a file of
/// <see cref="FileExplorerMcpOptions.MaxUploadSize"/>: its bytes arrive base64-encoded, a third larger, inside
/// a JSON-RPC message. Never lowers it, and leaves a host's "no endpoint limit" (<c>null</c>) alone.
/// </summary>
public class FileExplorerMcpServerOptionsSetup : IPostConfigureOptions<AbpMcpServerOptions>
{
    /// <summary>Room for the JSON-RPC envelope and the tool's other arguments around the file.</summary>
    public const long EnvelopeAllowance = 64 * 1024;

    protected FileExplorerMcpOptions Options { get; }

    public FileExplorerMcpServerOptionsSetup(IOptions<FileExplorerMcpOptions> options)
    {
        Options = options.Value;
    }

    public virtual void PostConfigure(string? name, AbpMcpServerOptions options)
    {
        if (options.MaxRequestBodySize == null)
        {
            return;
        }

        var required = 4 * ((Options.MaxUploadSize + 2) / 3) + EnvelopeAllowance;
        if (options.MaxRequestBodySize < required)
        {
            options.MaxRequestBodySize = required;
        }
    }
}
