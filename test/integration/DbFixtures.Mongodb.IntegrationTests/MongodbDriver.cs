using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace DbFixtures.Mongodb.Tests.Integration;

[Trait("Type", "Integration")]
public class MongodbDriverTests : IDisposable
{
  private const string DB_NAME = "testDb";
  private readonly IMongoClient _client;
  private readonly IMongoDatabase _db;

  public MongodbDriverTests()
  {
    this._client = new MongoClient("mongodb://admin:pw@api_db:27017/admin?authMechanism=SCRAM-SHA-256");

    this._client.DropDatabase(DB_NAME);
    this._db = this._client.GetDatabase(DB_NAME);
  }

  public void Dispose()
  {
    this._client.Dispose();
  }

  [Fact]
  public async Task Truncate_ItShouldDeleteAllDocumentsFromTheProvidedCollections()
  {
    var collOne = this._db.GetCollection<TestModel>("collOne");
    collOne.InsertMany([
      new TestModel{ Prop1 = "model 1", Prop2 = true },
      new TestModel{ Prop1 = "model 2" },
    ]);

    Assert.Equal(2, collOne.Find(FilterDefinition<TestModel>.Empty).ToList().Count);

    var collTwo = this._db.GetCollection<TestModel>("collTwo");
    collTwo.InsertMany([
      new TestModel{ Prop1 = "model 3" },
    ]);
    Assert.Single(collTwo.Find(FilterDefinition<TestModel>.Empty).ToList());

    var sut = new MongodbDriver(this._client, DB_NAME);
    await sut.Truncate(["collOne", "collTwo"]);

    Assert.Empty(collOne.Find(FilterDefinition<TestModel>.Empty).ToList());
    Assert.Empty(collTwo.Find(FilterDefinition<TestModel>.Empty).ToList());
  }

  [Fact]
  public async Task InsertFixtures_ItShouldInsertTheProvidedDocumentsIntoTheSpecifiedCollection()
  {
    var coll = this._db.GetCollection<TestModel>("coll");
    Assert.Empty(coll.Find(FilterDefinition<TestModel>.Empty).ToList());

    var doc1 = new TestModel
    {
      Id = ObjectId.GenerateNewId().ToString(),
      Prop1 = "something from doc 1",
    };
    var doc2 = new TestModel
    {
      Id = ObjectId.GenerateNewId().ToString(),
      Prop1 = "string from doc 2",
      Prop2 = false,
    };

    var sut = new MongodbDriver(this._client, DB_NAME);
    await sut.InsertFixtures("coll", [doc1, doc2]);

    var docs = coll.Find(FilterDefinition<TestModel>.Empty).ToList();
    Assert.Equal(2, docs.Count);
    Assert.Equal(doc1.ToJson(), docs[0].ToJson());
    Assert.Equal(doc2.ToJson(), docs[1].ToJson());
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
  public bool Prop2 { get; set; }
}