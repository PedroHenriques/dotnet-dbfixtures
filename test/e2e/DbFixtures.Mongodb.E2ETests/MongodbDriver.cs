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
  private readonly IMongoClient _client;
  private readonly IMongoDatabase _db;
  private readonly IDriver _driver;

  public MongodbDriverTests()
  {
    this._client = new MongoClient("mongodb://admin:pw@api_db:27017/admin?authMechanism=SCRAM-SHA-256");
    this._db = this._client.GetDatabase(DB_NAME);
    this._driver = new MongodbDriver(this._client, DB_NAME);

    this._client.DropDatabase(DB_NAME);
  }

  public void Dispose()
  {
    this._client.Dispose();
    this._driver.Close();
  }

  [Fact]
  public async Task InsertFixtures_ItShouldInsertTheProvidedFixturesInTheSpecifiedCollections()
  {
    var collOne = this._db.GetCollection<TestModel>("coll");
    collOne.InsertMany([
      new TestModel{ Prop1 = "model 1", Prop2 = 1 },
      new TestModel{ Prop1 = "model 2" },
    ]);

    Assert.Equal(2, collOne.Find(FilterDefinition<TestModel>.Empty).ToList().Count);

    var collTwo = this._db.GetCollection<TestModel>("anotherColl");
    collTwo.InsertMany([
      new TestModel{ Prop1 = "model 3", Prop2 = 39 },
      new TestModel{ Prop1 = "model 4", Prop2 = 92 },
      new TestModel{ Prop1 = "model 5" },
    ]);

    Assert.Equal(3, collTwo.Find(FilterDefinition<TestModel>.Empty).ToList().Count);

    TestModel[] expectedDocs = [
      new TestModel{ Id = ObjectId.GenerateNewId().ToString(), Prop1 = "final doc 1" },
      new TestModel{ Id = ObjectId.GenerateNewId().ToString(), Prop1 = "final doc 2", Prop2 = 123 },
    ];

    var sut = new DbFixtures([this._driver]);
    await sut.InsertFixtures(
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