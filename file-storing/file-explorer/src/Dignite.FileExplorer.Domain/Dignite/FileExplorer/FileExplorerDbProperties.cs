namespace Dignite.FileExplorer;

public static class FileExplorerDbProperties
{
    /// <summary>
    /// Default value: "Fe".
    /// </summary>
    public static string DbTablePrefix { get; set; } = "Fe";

    /// <summary>
    /// Default value: "null".
    /// </summary>
    public static string DbSchema { get; set; } = null;

    /// <summary>
    /// "FileExplorer".
    /// </summary>
    public const string ConnectionStringName = "FileExplorer";
}