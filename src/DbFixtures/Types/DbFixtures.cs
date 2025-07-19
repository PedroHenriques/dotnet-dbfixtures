namespace DbFixtures.Types;

public interface IDbFixtures
{
  public Task InsertFixtures(string[] tableNames, Dictionary<string, object[]> fixtures);

  public Task CloseDrivers();
}