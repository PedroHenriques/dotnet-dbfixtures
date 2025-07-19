using DbFixtures.Types;
using SharedLibs.Types;

namespace DbFixtures;

public class DbFixtures : IDbFixtures
{
  private readonly IDriver[] _drivers;

  public DbFixtures(IDriver[] drivers)
  {
    this._drivers = drivers;
  }

  public async Task CloseDrivers()
  {
    List<Task> tasks = new List<Task>();

    foreach (var driver in this._drivers)
    {
      tasks.Add(Task.Run(async () =>
      {
        await driver.Close();
      }));
    }

    await Task.WhenAll(tasks);
  }

  public Task InsertFixtures(string[] tableNames, Dictionary<string, object[]> fixtures)
  {
    throw new NotImplementedException();
  }
}