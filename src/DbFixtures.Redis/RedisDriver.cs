using DbFixtures.Redis.Types;
using DbFixtures.SharedLibs.Types;
using StackExchange.Redis;

namespace DbFixtures.Redis;

public class RedisDriver : IDriver
{
  private readonly IConnectionMultiplexer _client;
  private readonly IDatabase _db;
  private readonly Dictionary<string, KeyTypes> _keyTypes;

  public RedisDriver(
    IConnectionMultiplexer client, IDatabase db,
    Dictionary<string, KeyTypes> keyTypes
  )
  {
    this._client = client;
    this._db = db;
    this._keyTypes = keyTypes;
  }

  public Task Close()
  {
    this._client.Close();
    return Task.CompletedTask;
  }

  public async Task InsertFixtures<T>(string tableName, T[] fixtures)
  {
    if (fixtures.Length == 0) { return; }

    KeyTypes keyType;
    if (this._keyTypes.TryGetValue(tableName, out keyType) == false)
    {
      throw new Exception($"The key '{tableName}' doesn't have a declared type, in the dictionary provided in the constructor of this driver");
    }

    switch (keyType)
    {
      case KeyTypes.String:
        var result = await this._db.StringSetAsync(
          tableName, new RedisValue(fixtures[0].ToString())
        );
        if (result == false)
        {
          throw new Exception($"Failed to insert string key '{tableName}'");
        }
        return;

      case KeyTypes.List:
        RedisValue[] listValues = Array.ConvertAll(
          fixtures, fixture => new RedisValue(fixture.ToString())
        );
        await this._db.ListLeftPushAsync(tableName, listValues);
        return;

      case KeyTypes.Set:
        RedisValue[] setValues = Array.ConvertAll(
          fixtures, fixture => new RedisValue(fixture.ToString())
        );
        await this._db.SetAddAsync(tableName, setValues);
        return;

      case KeyTypes.Stream:
        var streamFixture = fixtures as Dictionary<string, string>[];
        foreach (var fixture in streamFixture)
        {
          NameValueEntry[] streamValues = fixture.Select(
            pair => new NameValueEntry(pair.Key, pair.Value)
          ).ToArray();

          await this._db.StreamAddAsync(tableName, streamValues);
        }
        return;

      case KeyTypes.Hash:
        var hashFixture = fixtures as Dictionary<string, string>[];
        foreach (var fixture in hashFixture)
        {
          HashEntry[] hashValues = fixture.Select(
            pair => new HashEntry(pair.Key, pair.Value)
          ).ToArray();

          await this._db.HashSetAsync(new RedisKey(tableName), hashValues);
        }
        return;
    }
  }

  public async Task Truncate(string[] tableNames)
  {
    RedisKey[] keys = Array.ConvertAll(tableNames, name => (RedisKey)name);
    await this._db.KeyDeleteAsync(keys);
  }
}
