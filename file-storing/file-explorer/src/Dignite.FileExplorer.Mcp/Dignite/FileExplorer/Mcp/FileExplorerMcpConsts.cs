namespace Dignite.FileExplorer.Mcp;

public static class FileExplorerMcpConsts
{
    /// <summary>The MCP namespace: every tool is named <c>file_explorer_…</c>.</summary>
    public const string ModuleName = "file_explorer";

    /// <summary>
    /// This module's section of the server instructions. It describes only these tools: another
    /// module's tools may or may not be on the same server.
    /// </summary>
    public const string Instructions =
        "File storage (file_explorer_* tools): files live in containers the host has chosen to expose. " +
        "Call `file_explorer_list_containers` first - it returns the containers you may use, with each one's " +
        "purpose, upload size limit, allowed file types and cells. Every other file_explorer tool takes a " +
        "`containerName` from that list; do not guess one.\n\n" +
        "Uploading: `file_explorer_upload_file` takes the file's bytes as base64, so it suits small files only. " +
        "The result carries the file's `url`, which is how to reference the file anywhere else.\n\n" +
        "Files and directories are addressed by the `id` the list tools return.";
}

public static class FileExplorerMcpErrorCodes
{
    /// <summary>
    /// Outside the <c>FileExplorer:</c> range on purpose - a transport-surface failure (a container that
    /// is not exposed to MCP, or a file in one), not a domain rule. Its message is written for a model and
    /// is not localized.
    /// </summary>
    public const string NotFound = "FileExplorer.Mcp:001";
}
