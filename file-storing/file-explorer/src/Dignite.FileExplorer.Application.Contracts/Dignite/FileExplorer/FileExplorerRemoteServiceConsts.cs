namespace Dignite.FileExplorer;

public class FileExplorerRemoteServiceConsts
{
    public const string RemoteServiceName = "FileExplorer";

    public const string ModuleName = "file-explorer";

    /// <summary>
    /// The route the HTTP API serves files under, <c>{FilesRoutePrefix}/{containerName}/{blobName}</c>.
    /// Here rather than on the controller so a file's URL is built from one definition by every surface
    /// that hands one out - the HTTP API and the MCP tools.
    /// </summary>
    public const string FilesRoutePrefix = "api/file-explorer/files";
}