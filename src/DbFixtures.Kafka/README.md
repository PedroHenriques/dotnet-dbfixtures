# Fixtures Manager Kafka Driver

An abstraction layer for the [Kafka package](https://www.nuget.org/packages/confluent.kafka) to facilitate handling database fixtures for testing purposes, in a Kafka instance.  
This package is ment to be used in conjunction with the [dbfixtures package](https://github.com/PedroHenriques/dotnet-dbfixtures/blob/main/src/DbFixtures/README.md), but can also be used by itself.

## Installation

```sh
dotnet add [path/to/your/csproj/file] package DbFixtures.Kafka
```

## Usage

This package exposes the following interface

```c#
public interface IDriver
{
  // clears the specified "tables" of any content
  public Task Truncate(string[] tableNames);

  // inserts the supplied "rows" into the specified "table"
  public Task InsertFixtures<T>(string tableName, T[] fixtures);

  // cleanup and terminate the connection to the database
  public Task Close();
}
```

### Example

```c#
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using SharedLibs.Types;

namespace DbFixtures.Mongodb.Tests.E2E;

[Trait("Type", "E2E")]
public class MongodbDriverTests : IDisposable
{
  private const string DB_NAME = "testDb";
  private readonly IDbFixtures _dbFixtures;

  public MongodbDriverTests()
  {
    var client = new MongoClient("mongodb://admin:pw@api_db:27017/admin?authMechanism=SCRAM-SHA-256");
    var driver = new MongodbDriver(this._client, DB_NAME);
    this._dbFixtures = new DbFixtures([this._driver]);

    this._client.DropDatabase(DB_NAME);
  }

  public void Dispose()
  {
    this._dbFixtures.CloseDrivers();
  }

  [Fact]
  public async TasK MySutMethod_ItShouldWork()
  {
    var collOne = this._db.GetCollection<TestModel>("coll");
    var collTwo = this._db.GetCollection<TestModel>("anotherColl");

    TestModel[] expectedDocs = [
      new TestModel{ Id = ObjectId.GenerateNewId().ToString(), Prop1 = "final doc 1" },
      new TestModel{ Id = ObjectId.GenerateNewId().ToString(), Prop1 = "final doc 2", Prop2 = 123 },
    ];

    await this._dbFixtures.InsertFixtures(
      ["coll", "anotherColl"],
      new Dictionary<string, TestModel[]>
      {
        { "coll", [] },
        { "anotherColl", expectedDocs },
      }
    );

    var CollOneDocs = collOne.Find(FilterDefinition<TestModel>.Empty).ToList();
    var CollTwoDocs = collTwo.Find(FilterDefinition<TestModel>.Empty).ToList();
    Assert.Empty(CollOneDocs);
    Assert.Equal(expectedDocs.ToJson(), CollTwoDocs.ToJson());
  }
}

public class TestModel
{
  [JsonPropertyName("id")]
  [BsonId]
  [BsonRepresentation(BsonType.ObjectId)]
  [BsonIgnoreIfDefault]
  public string? Id { get; set; }

  [JsonPropertyName("prop1")]
  public required string Prop1 { get; set; }

  [JsonPropertyName("prop2")]
  public int Prop2 { get; set; }
}
```