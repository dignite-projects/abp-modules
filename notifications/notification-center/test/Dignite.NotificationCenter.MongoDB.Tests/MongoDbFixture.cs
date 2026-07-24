using System;
using MongoDB.Driver;
using MongoSandbox;

namespace Dignite.NotificationCenter.MongoDB;

/// <summary>
/// Boots one embedded MongoDB server for the whole test session via MongoSandbox (the maintained
/// successor of EphemeralMongo; the runtime package bundles the mongod binary, so no local MongoDB
/// install is required). Each test run gets its own randomly-named database for isolation.
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
        return GetConnectionString(MongoDbRunner, databaseName);
    }

    public static string GetConnectionString(IMongoRunner runner, string databaseName)
    {
        return new MongoUrlBuilder(runner.ConnectionString)
        {
            DatabaseName = databaseName
        }.ToString();
    }

    public void Dispose()
    {
        MongoDbRunner?.Dispose();
    }
}
