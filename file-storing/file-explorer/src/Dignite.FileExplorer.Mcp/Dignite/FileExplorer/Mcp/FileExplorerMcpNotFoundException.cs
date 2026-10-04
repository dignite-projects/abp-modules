using Dignite.Abp.AspNetCore.Mcp.Errors;
using Volo.Abp;

namespace Dignite.FileExplorer.Mcp;

/// <summary>
/// A container that is not exposed to MCP, or a file or directory that lives in one.
/// <para>
/// A <see cref="UserFriendlyException"/> because ABP's error converter passes the message through only for
/// those - and the message, which names the containers that do exist, is the one thing a model can act
/// on. <see cref="IHasMcpToolErrorKind"/> because it means "not found", not the business-rule "conflict"
/// its base type would otherwise be classified as.
/// </para>
/// </summary>
public class FileExplorerMcpNotFoundException : UserFriendlyException, IHasMcpToolErrorKind
{
    public FileExplorerMcpNotFoundException(string message)
        : base(message, code: FileExplorerMcpErrorCodes.NotFound)
    {
    }

    public string McpToolErrorKind => McpToolErrorKinds.NotFound;
}
