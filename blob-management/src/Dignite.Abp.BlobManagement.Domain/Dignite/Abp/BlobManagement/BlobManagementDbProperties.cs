namespace Dignite.Abp.BlobManagement;

public static class BlobManagementDbProperties
{
    public static string DbTablePrefix { get; set; } = "Blob";

    public static string? DbSchema { get; set; } = null;

    public const string ConnectionStringName = "BlobManagement";
}
