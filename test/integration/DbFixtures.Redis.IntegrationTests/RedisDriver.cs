using Newtonsoft.Json;
using SharedLibs.Types;
using StackExchange.Redis;

namespace DbFixtures.Redis.Tests.Integration;

[Trait("Type", "Integration")]
public class RedisDriverTests : IDisposable
{
  private readonly IConnectionMultiplexer _client;
  private readonly IDatabase _db;
  private readonly IDriver _sut;

  public RedisDriverTests()
  {
    this._client = ConnectionMultiplexer.Connect(
      new ConfigurationOptions
      {
        EndPoints = { "api_redis:6379" },
        AbortOnConnectFail = false,
      }
    );
    this._db = this._client.GetDatabase(0);

    this._sut = new RedisDriver(
      this._client, this._db,
      new Dictionary<string, Types.KeyTypes>
      {
        { "testHash", Types.KeyTypes.Hash },
        { "testList", Types.KeyTypes.List },
        { "testSet", Types.KeyTypes.Set },
        { "testStream", Types.KeyTypes.Stream },
        { "testString", Types.KeyTypes.String },
      }
    );

    this._db.KeyDelete("testHash");
    this._db.KeyDelete("testList");
    this._db.KeyDelete("testSet");
    this._db.KeyDelete("testStream");
    this._db.KeyDelete("testString");
  }

  public void Dispose()
  {
    this._sut.Close();
  }

  [Fact]
  public async Task Truncate_ItShouldDeleteAllTheProvidedKeys()
  {
    await this._db.StringSetAsync("safeString", "some safe str value");
    await this._db.HashSetAsync("safeHash", [new HashEntry("some safe hash key", "some safe hash value"), new HashEntry("some safe other hash key", "some safe other hash value")]);

    await this._db.StringSetAsync("testString", "some str value");
    await this._db.ListLeftPushAsync("testList", ["some list value", "some other list value"]);
    await this._db.SetAddAsync("testSet", ["some set value", "some other set value"]);
    await this._db.StreamAddAsync("testStream", [new NameValueEntry("some stream key", "some stream value")]);
    await this._db.StreamAddAsync("testStream", [new NameValueEntry("some other stream key", "some other stream value")]);
    await this._db.HashSetAsync("testHash", [new HashEntry("some hash key", "some hash value"), new HashEntry("some other hash key", "some other hash value")]);

    Assert.Equal("some safe str value", await this._db.StringGetAsync("safeString"));
    Assert.Equal(2, await this._db.HashLengthAsync("safeHash"));
    Assert.Equal("some str value", await this._db.StringGetAsync("testString"));
    Assert.Equal(2, await this._db.ListLengthAsync("testList"));
    Assert.Equal(2, await this._db.SetLengthAsync("testSet"));
    Assert.Equal(2, await this._db.StreamLengthAsync("testStream"));
    Assert.Equal(2, await this._db.HashLengthAsync("testHash"));

    await this._sut.Truncate(["testString", "testList", "testSet", "testStream", "testHash"]);

    Assert.Equal("some safe str value", await this._db.StringGetAsync("safeString"));
    Assert.Equal(2, await this._db.HashLengthAsync("safeHash"));
    Assert.Equal(RedisValue.Null, await this._db.StringGetAsync("testString"));
    Assert.Equal(0, await this._db.ListLengthAsync("testList"));
    Assert.Equal(0, await this._db.SetLengthAsync("testSet"));
    Assert.Equal(0, await this._db.StreamLengthAsync("testStream"));
    Assert.Equal(0, await this._db.HashLengthAsync("testHash"));
  }

  [Fact]
  public async Task InsertFixtures_ItShouldInsertTheProvidedValuesIntoTheSpecifiedKeys()
  {
    await this._db.StringSetAsync("safeString", "some safe str value");
    HashEntry[] safeHashContent = [new HashEntry("some safe hash key", "some safe hash value"), new HashEntry("some safe other hash key", "some safe other hash value")];
    await this._db.HashSetAsync("safeHash", safeHashContent);

    Assert.Equal("some safe str value", await this._db.StringGetAsync("safeString"));
    Assert.Equal(2, await this._db.HashLengthAsync("safeHash"));
    Assert.Equal(RedisValue.Null, await this._db.StringGetAsync("testString"));
    Assert.Equal(0, await this._db.ListLengthAsync("testList"));
    Assert.Equal(0, await this._db.SetLengthAsync("testSet"));
    Assert.Equal(0, await this._db.StreamLengthAsync("testStream"));
    Assert.Equal(0, await this._db.HashLengthAsync("testHash"));

    await this._sut.InsertFixtures<string>("testString", ["new value"]);
    string[] testListContent = ["list v1", "list v2", "list v3"];
    await this._sut.InsertFixtures<string>("testList", testListContent);
    string[] testSetContent = ["set v1", "set v2"];
    await this._sut.InsertFixtures<string>("testSet", testSetContent);
    Dictionary<string, string>[] testStreamContent = [
      new Dictionary<string, string> {
        { "stream f1 k1", "stream f1 v1" },
        { "stream f1 k2", "stream f1 v2" },
      },
      new Dictionary<string, string> {
        { "stream f2 k1", "stream f2 v1" },
      },
    ];
    await this._sut.InsertFixtures<Dictionary<string, string>>("testStream", testStreamContent);
    await this._sut.InsertFixtures<Dictionary<string, string>>(
      "testHash",
      [
        new Dictionary<string, string> {
          { "hash f1 k1", "hash f1 v1" },
        },
        new Dictionary<string, string> {
          { "hash f2 k1", "hash f2 v1" },
          { "hash f2 k2", "hash f2 v2" },
        },
      ]
    );

    Assert.Equal("some safe str value", await this._db.StringGetAsync("safeString"));
    Assert.Equal(2, await this._db.HashLengthAsync("safeHash"));
    Assert.Equal(safeHashContent, await this._db.HashGetAllAsync("safeHash"));
    Assert.Equal("new value", await this._db.StringGetAsync("testString"));
    Assert.Equal(3, await this._db.ListLengthAsync("testList"));
    var actualTestListContent = (await this._db.ListRangeAsync("testList", 0, 100)).ToStringArray();
    Array.Sort(actualTestListContent);
    Assert.Equal(testListContent, actualTestListContent);
    Assert.Equal(2, await this._db.SetLengthAsync("testSet"));
    Assert.Equal(testSetContent, (await this._db.SetMembersAsync("testSet")).ToStringArray());
    Assert.Equal(2, await this._db.StreamLengthAsync("testStream"));
    Assert.Equal(
      JsonConvert.SerializeObject(testStreamContent),
      JsonConvert.SerializeObject(
        (await this._db.StreamRangeAsync("testStream", "-", "+"))
        .Select(entry => entry.Values.ToDictionary(nv => nv.Name.ToString(), nv => nv.Value.ToString()))
        .ToArray()
      )
    );
    Assert.Equal(3, await this._db.HashLengthAsync("testHash"));
    Assert.Equal(
      new Dictionary<string, string> {
        { "hash f1 k1", "hash f1 v1" },
        { "hash f2 k1", "hash f2 v1" },
        { "hash f2 k2", "hash f2 v2" },
      },
      (await this._db.HashGetAllAsync("testHash")).ToDictionary(entry => entry.Name.ToString(), entry => entry.Value.ToString())
    );
  }
}