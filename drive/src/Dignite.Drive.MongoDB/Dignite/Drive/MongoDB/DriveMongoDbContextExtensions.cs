using MongoDB.Bson;
using MongoDB.Driver;
using Volo.Abp;
using Volo.Abp.MongoDB;

namespace Dignite.Drive.MongoDB;

public static class DriveMongoDbContextExtensions
{
    public static void ConfigureDrive(
        this IMongoModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<FileDescriptor>(x =>
        {
            x.CollectionName = DriveDbProperties.DbTablePrefix + "FileDescriptors";
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

                // A system container's address: one live descriptor per tenant, container and name (the same rule as
                // the EF Core unique index). User-drive files and deleted rows carry no key and are not indexed.
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
            });
        });

    }
}
