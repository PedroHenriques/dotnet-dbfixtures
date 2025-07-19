namespace SharedLibs.Types;

public interface IDriver
{
  public Task Truncate(string[] tableNames);

  public Task InsertFixtures(string tableName, object[] fixtures);

  public Task Close();
}