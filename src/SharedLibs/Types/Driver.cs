namespace DbFixtures.SharedLibs.Types;

public interface IDriver
{
  public Task Truncate(string[] tableNames);

  public Task InsertFixtures<T>(string tableName, T[] fixtures);

  public Task Close();
}