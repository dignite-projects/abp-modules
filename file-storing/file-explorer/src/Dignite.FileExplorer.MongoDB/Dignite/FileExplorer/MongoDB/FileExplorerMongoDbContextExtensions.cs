using System.Linq;
using Dignite.FileExplorer.Directories;
using Dignite.FileExplorer.Files;
using MongoDB.Bson;
using MongoDB.Driver;
using Volo.Abp;
using Volo.Abp.MongoDB;

namespace Dignite.FileExplorer.MongoDB;

public static class FileExplorerMongoDbContextExtensions
{
    public static void ConfigureFileExplorer(
        this IMongoModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<DirectoryDescriptor>(x =>
        {
            x.CollectionName = FileExplorerDbProperties.DbTablePrefix + "DirectoryDescriptors";
        });

        builder.Entity<FileDescriptor>(x =>
        {
            x.CollectionName = FileExplorerDbProperties.DbTablePrefix + "FileDescriptors";
            x.ConfigureIndexes(indexes =>
            {
                var keys = Builders<BsonDocument>.IndexKeys;

                // #16 created this lookup index without uniqueness. Remove that
                // legacy definition before creating the unique hash index; MongoDB
                // does not allow equivalent indexes with different options.
                RemoveLegacyMd5Index(indexes);

                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    keys.Ascending(nameof(FileDescriptor.TenantId))
                        .Ascending(nameof(FileDescriptor.ContainerName))
                        .Ascending(nameof(FileDescriptor.BlobName)),
                    new CreateIndexOptions { Unique = true }));
                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    keys.Ascending(nameof(FileDescriptor.TenantId))
                        .Ascending(nameof(FileDescriptor.ContainerName))
                        .Ascending(nameof(FileDescriptor.Md5)),
                    new CreateIndexOptions<BsonDocument>
                    {
                        Unique = true,
                        PartialFilterExpression = Builders<BsonDocument>.Filter.Gt(
                            nameof(FileDescriptor.Md5),
                            string.Empty)
                    }));
                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    keys.Ascending(nameof(FileDescriptor.TenantId))
                        .Ascending(nameof(FileDescriptor.ContainerName))
                        .Ascending(nameof(FileDescriptor.ReferBlobName))));
                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    keys.Ascending(nameof(FileDescriptor.TenantId))
                        .Ascending(nameof(FileDescriptor.ContainerName))
                        .Ascending(nameof(FileDescriptor.EntityId))));
            });
        });
    }

    private static void RemoveLegacyMd5Index(IMongoIndexManager<BsonDocument> indexes)
    {
        try
        {
            using var cursor = indexes.List();
            foreach (var index in cursor.ToList())
            {
                if (!index.TryGetValue("name", out var name) ||
                    !name.IsString ||
                    !name.AsString.Equals(
                        "TenantId_1_ContainerName_1_Md5_1",
                        System.StringComparison.Ordinal))
                {
                    continue;
                }

                var isUnique = index.TryGetValue("unique", out var unique) && unique.IsBoolean && unique.AsBoolean;
                var hasPartialFilter = index.Contains("partialFilterExpression");
                if (!isUnique || !hasPartialFilter)
                {
                    indexes.DropOne(name.AsString);
                }
            }
        }
        catch (MongoCommandException exception) when (exception.Code == 26)
        {
            // The collection does not exist yet; CreateOne will create it as needed.
        }
    }
}
