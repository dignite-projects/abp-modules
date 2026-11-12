using MongoDB.Bson;
using MongoDB.Driver;
using Volo.Abp;
using Volo.Abp.MongoDB;

namespace Dignite.Abp.BlobManagement.MongoDB;

public static class BlobManagementMongoDbContextExtensions
{
    public static void ConfigureBlobManagement(
        this IMongoModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<FileDescriptor>(x =>
        {
            x.CollectionName = BlobManagementDbProperties.DbTablePrefix + "FileDescriptors";
            x.ConfigureIndexes(indexes =>
            {
                var keys = Builders<BsonDocument>.IndexKeys;

                // Content dedup's arbiter: one live writer per tenant, container and hash (the same rule as the EF
                // Core unique index). Rows without a key - every row but the live writer - are not indexed.
                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    keys.Ascending(nameof(FileDescriptor.DedupKey)),
                    new CreateIndexOptions<BsonDocument>
                    {
                        Unique = true,
                        PartialFilterExpression = Builders<BsonDocument>.Filter.Type(nameof(FileDescriptor.DedupKey), BsonType.String)
                    }));

                // The address of a file an application saved: one live descriptor per tenant, container and name (the
                // same rule as the EF Core unique index). Uploaded files and deleted rows carry no key and are not
                // indexed.
                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    keys.Ascending(nameof(FileDescriptor.NameKey)),
                    new CreateIndexOptions<BsonDocument>
                    {
                        Unique = true,
                        PartialFilterExpression = Builders<BsonDocument>.Filter.Type(nameof(FileDescriptor.NameKey), BsonType.String)
                    }));

                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    keys.Ascending(nameof(FileDescriptor.TenantId))
                        .Ascending(nameof(FileDescriptor.ContainerName))
                        .Ascending(nameof(FileDescriptor.Name))));

                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    keys.Ascending(nameof(FileDescriptor.TenantId))
                        .Ascending(nameof(FileDescriptor.ContainerName))
                        .Ascending(nameof(FileDescriptor.Hash))));

                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    keys.Ascending(nameof(FileDescriptor.TenantId))
                        .Ascending(nameof(FileDescriptor.ContainerName))
                        .Ascending(nameof(FileDescriptor.BlobName))));

                // The listing of one place - a container's top, or a directory - sorted by name.
                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    keys.Ascending(nameof(FileDescriptor.TenantId))
                        .Ascending(nameof(FileDescriptor.ContainerName))
                        .Ascending(nameof(FileDescriptor.DirectoryId))
                        .Ascending(nameof(FileDescriptor.Name))));

                // "Is anything filed here" - asked on every directory delete, by directory alone.
                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    keys.Ascending(nameof(FileDescriptor.DirectoryId))));
            });
        });

        builder.Entity<DirectoryDescriptor>(x =>
        {
            x.CollectionName = BlobManagementDbProperties.DbTablePrefix + "DirectoryDescriptors";
            x.ConfigureIndexes(indexes =>
            {
                var keys = Builders<BsonDocument>.IndexKeys;

                // Sibling names, as an arbiter: one live directory per tenant, container, parent and name (the same
                // rule as the EF Core unique index). A deleted directory carries no key and is not indexed: MongoDB
                // would otherwise count every null as the same value.
                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    keys.Ascending(nameof(DirectoryDescriptor.NameKey)),
                    new CreateIndexOptions<BsonDocument>
                    {
                        Unique = true,
                        PartialFilterExpression = Builders<BsonDocument>.Filter.Type(nameof(DirectoryDescriptor.NameKey), BsonType.String)
                    }));

                // One level of a container's tree.
                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    keys.Ascending(nameof(DirectoryDescriptor.TenantId))
                        .Ascending(nameof(DirectoryDescriptor.ContainerName))
                        .Ascending(nameof(DirectoryDescriptor.ParentId))));

                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    keys.Ascending(nameof(DirectoryDescriptor.ParentId))));

                // A subtree is a prefix of Path (a move rewrites it).
                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    keys.Ascending(nameof(DirectoryDescriptor.Path))));
            });
        });

    }
}
