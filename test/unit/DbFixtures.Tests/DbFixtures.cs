using Moq;
using SharedLibs.Types;

namespace DbFixtures.Tests;

[Trait("Type", "Unit")]
public class DbFixturesTests : IDisposable
{
  private readonly Mock<IDriver> _driverOne;
  private readonly Mock<IDriver> _driverTwo;

  public DbFixturesTests()
  {
    this._driverOne = new Mock<IDriver>(MockBehavior.Strict);
    this._driverTwo = new Mock<IDriver>(MockBehavior.Strict);

    this._driverOne.Setup(s => s.Close())
      .Returns(Task.CompletedTask);

    this._driverTwo.Setup(s => s.Close())
      .Returns(Task.CompletedTask);
  }

  public void Dispose()
  {
    this._driverOne.Reset();
    this._driverTwo.Reset();
  }

  [Fact]
  public async Task CloseDrivers_ItShouldCallCloseOnEachRegisteredDriverOnce()
  {
    var sut = new DbFixtures([this._driverOne.Object, this._driverTwo.Object]);

    await sut.CloseDrivers();

    this._driverOne.Verify(m => m.Close(), Times.Once());
    this._driverTwo.Verify(m => m.Close(), Times.Once());
  }
}