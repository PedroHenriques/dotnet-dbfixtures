using SharedLibs.Types;

namespace DbFixtures.Redis;

public class RedisDriver : IDriver
{
  public RedisDriver() { }

  public Task Close()
  {
    throw new NotImplementedException();
  }

  public Task InsertFixtures<T>(string tableName, T[] fixtures)
  {
    throw new NotImplementedException();
  }

  public Task Truncate(string[] tableNames)
  {
    throw new NotImplementedException();
  }
}
