# Fixtures Manager

An abstraction layer for handling database fixtures for automated testing purposes, providing a standardized interface across different database systems.

## Installation

```sh
dotnet add [path/to/your/csproj/file] package DbFixtures
```

## Features

* Test runner agnostic
* No dependencies
* Standardized interface across multiple database systems
* Easily set your database for each test's needs

## Drivers

This package will use drivers to handle the database operations.
Each driver will be dedicated to 1 databse system (ex: MongoDb, Postgres).  
You can set as many drivers as needed and the fixtures will be sent to each one.

### Driver interface

The drivers are expected to use the following interface

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

### Current official drivers

* [MongoDB](https://github.com/PedroHenriques/dotnet-dbfixtures/blob/main/src/DbFixtures.Mongodb/README.md)

## Usage

This package exposes the following interface

```c#
public interface IDbFixtures
{
  public Task InsertFixtures<T>(string[] tableNames, Dictionary<string, T[]> fixtures);

  public Task CloseDrivers();
}
```

Where

* `InsertFixtures<T>(string[] tableNames, Dictionary<string, T[]> fixtures)`: call this function with the fixtures to be sent to each registered driver.  
**Note:** the fixtures will be inserted in the order they are provided.

* `CloseDrivers()`: call this function to run any necessary cleanup operations on all registered drivers.

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

## How It Works

Each registered driver will be called to:

* clear the "tables" that will be used in the current fixture insertion operation from any content.

* insert the fixtures in the order they were provided.

* terminate the connection to their database.
