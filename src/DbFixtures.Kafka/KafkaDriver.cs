using SharedLibs.Types;

namespace DbFixtures.Kafka;

public class KafkaDriver : IDriver
{
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