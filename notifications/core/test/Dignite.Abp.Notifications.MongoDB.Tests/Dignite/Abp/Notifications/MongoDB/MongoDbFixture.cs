using System;
using MongoDB.Driver;
using MongoSandbox;

namespace Dignite.Abp.Notifications.MongoDB;

/// <summary>
/// Boots one embedded MongoDB server for the whole test session via MongoSandbox (the runtime package bundles the
/// mongod binary, so no local MongoDB install is required), as <c>Dignite.NotificationCenter.MongoDB.Tests</c> does.
/// It runs as a single-node replica set: the static saver writes in a transactional unit of work, as ABP's
/// <c>StaticPermissionSaver</c> does, and MongoDB transactions need a replica set.
/// </summary>
public class MongoDbFixture : IDisposable
{
    public static readonly IMongoRunner MongoDbRunner;

    static MongoDbFixture()
    {
        MongoDbRunner = MongoRunner.Run(new MongoRunnerOptions
        {
            UseSingleNodeReplicaSet = true
        });
    }

    public static string GetRandomConnectionString()
    {
        return GetConnectionString("Db_" + Guid.NewGuid().ToString("N"));
    }

    public static string GetConnectionString(string databaseName)
    {
        return new MongoUrlBuilder(MongoDbRunner.ConnectionString)
        {
            DatabaseName = databaseName
        }.ToString();
    }

    public void Dispose()
    {
        MongoDbRunner?.Dispose();
    }
}
