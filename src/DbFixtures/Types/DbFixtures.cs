namespace DbFixtures.Types;

public interface IDbFixtures
{
  public Task InsertFixtures<T>(string[] tableNames, Dictionary<string, T[]> fixtures);

  public Task CloseDrivers();
}