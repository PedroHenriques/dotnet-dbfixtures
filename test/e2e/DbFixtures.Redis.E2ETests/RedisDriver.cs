using DbFixtures.SharedLibs.Types;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace DbFixtures.Redis.Tests.E2E;

[Trait("Type", "E2E")]
public class RedisDriverTests : IAsyncDisposable
{
  private readonly IConnectionMultiplexer _client;
  private readonly IDatabase _db;
  private readonly IDriver _driver;
  private readonly DbFixtures _sut;

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
    this._driver = new RedisDriver(
      this._client, this._db,
      new Dictionary<string, Types.KeyTypes>
      {
        { "testHash", Types.KeyTypes.Hash },
        { "testList", Types.KeyTypes.List },
        { "testSet", Types.KeyTypes.Set },
        { "testStream", Types.KeyTypes.Stream },
        { "testString", Types.KeyTypes.String },
        { "anotherTestString", Types.KeyTypes.String },
      }
    );

    this._sut = new DbFixtures([this._driver]);

    this._db.KeyDelete("testHash");
    this._db.KeyDelete("testList");
    this._db.KeyDelete("testSet");
    this._db.KeyDelete("testStream");
    this._db.KeyDelete("testString");
    this._db.KeyDelete("anotherTestString");
  }

  public async ValueTask DisposeAsync()
  {
    await this._sut.CloseDrivers();
  }

  [Fact]
  public async Task InsertFixtures_ItShouldInsertTheProvidedValuesIntoTheSpecifiedKeys()
  {
    await this._db.StringSetAsync("anotherTestString", "delete me");

    Assert.Equal("delete me", await this._db.StringGetAsync("anotherTestString"));
    Assert.Equal(RedisValue.Null, await this._db.StringGetAsync("testString"));
    Assert.Equal(0, await this._db.ListLengthAsync("testList"));
    Assert.Equal(0, await this._db.SetLengthAsync("testSet"));
    Assert.Equal(0, await this._db.StreamLengthAsync("testStream"));
    Assert.Equal(0, await this._db.HashLengthAsync("testHash"));

    string[] testListContent = ["list v1", "list v2", "list v3"];
    string[] testSetContent = ["set v1", "set v2"];
    await this._sut.InsertFixtures<string>(
      ["testString", "testList", "testSet", "anotherTestString"],
      new Dictionary<string, string[]>
      {
        { "testString", ["new str value"] },
        { "anotherTestString", [] },
        { "testList", testListContent },
        { "testSet", testSetContent },
      }
    );

    Dictionary<string, string>[] testStreamContent = [
      new Dictionary<string, string> {
        { "stream f1 k1", "stream f1 v1" },
        { "stream f1 k2", "stream f1 v2" },
      },
      new Dictionary<string, string> {
        { "stream f2 k1", "stream f2 v1" },
      },
    ];
    Dictionary<string, string> testHashContent = new Dictionary<string, string> {
      { "hash f1 k1", "hash f1 v1" },
      { "hash f2 k1", "hash f2 v1" },
      { "hash f2 k2", "hash f2 v2" },
    };
    await this._sut.InsertFixtures<Dictionary<string, string>>(
      ["testStream", "testHash"],
      new Dictionary<string, Dictionary<string, string>[]>
      {
        { "testStream", testStreamContent },
        { "testHash", [testHashContent] },
      }
    );

    Assert.Equal(RedisValue.Null, await this._db.StringGetAsync("anotherTestString"));
    Assert.Equal("new str value", await this._db.StringGetAsync("testString"));
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
      testHashContent,
      (await this._db.HashGetAllAsync("testHash")).ToDictionary(entry => entry.Name.ToString(), entry => entry.Value.ToString())
    );
  }
}