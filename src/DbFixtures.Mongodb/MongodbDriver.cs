using MongoDB.Driver;
using SharedLibs.Types;

namespace DbFixtures.Mongodb;

public class MongodbDriver : IDriver
{
  private readonly IMongoClient _client;
  private readonly IMongoDatabase _db;

  public MongodbDriver(
    IMongoClient client, string dbName, MongoDatabaseSettings? dbSettings = null
  )
  {
    this._client = client;
    this._db = client.GetDatabase(dbName, dbSettings);
  }

  public Task Close()
  {
    this._client.Dispose();
    return Task.CompletedTask;
  }

  public async Task InsertFixtures<T>(string tableName, T[] fixtures)
  {
    if (fixtures.Length == 0) { return; }

    var coll = this._db.GetCollection<T>(tableName);
    await coll.InsertManyAsync(fixtures);
  }

  public async Task Truncate(string[] tableNames)
  {
    List<Task> tasks = new List<Task>();

    foreach (var tableName in tableNames)
    {
      tasks.Add(Task.Run(() => this._db.DropCollectionAsync(tableName)));
    }

    await Task.WhenAll(tasks);
  }
}