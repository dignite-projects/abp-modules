namespace Dignite.Drive;

public static class DriveDbProperties
{
    public static string DbTablePrefix { get; set; } = "Drive";

    public static string? DbSchema { get; set; } = null;

    public const string ConnectionStringName = "Drive";
}
