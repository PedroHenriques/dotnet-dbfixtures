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
    this._driverOne.Setup(s => s.Truncate(It.IsAny<string[]>()))
      .Returns(Task.CompletedTask);
    this._driverOne.Setup(s => s.InsertFixtures(It.IsAny<string>(), It.IsAny<object[]>()))
      .Returns(Task.CompletedTask);

    this._driverTwo.Setup(s => s.Close())
      .Returns(Task.CompletedTask);
    this._driverTwo.Setup(s => s.Truncate(It.IsAny<string[]>()))
      .Returns(Task.CompletedTask);
    this._driverTwo.Setup(s => s.InsertFixtures(It.IsAny<string>(), It.IsAny<object[]>()))
      .Returns(Task.CompletedTask);
  }

  public void Dispose()
  {
    this._driverOne.Reset();
    this._driverTwo.Reset();
  }

  [Fact]
  public async Task CloseDrivers_ItShouldCallCloseOnFirstRegisteredDriverOnce()
  {
    var sut = new DbFixtures([this._driverOne.Object, this._driverTwo.Object]);

    await sut.CloseDrivers();

    this._driverOne.Verify(m => m.Close(), Times.Once());
  }

  [Fact]
  public async Task CloseDrivers_ItShouldCallCloseOnSecondRegisteredDriverOnce()
  {
    var sut = new DbFixtures([this._driverOne.Object, this._driverTwo.Object]);

    await sut.CloseDrivers();

    this._driverTwo.Verify(m => m.Close(), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_ItShouldCallTruncateOnTheFirstRegisteredDriverOnce()
  {
    var sut = new DbFixtures([this._driverOne.Object, this._driverTwo.Object]);

    string[] tables = ["gh"];
    Dictionary<string, object[]> fixtures = new Dictionary<string, object[]>
    {
      { "gh", [ ] },
    };
    await sut.InsertFixtures(tables, fixtures);

    this._driverOne.Verify(m => m.Truncate(tables), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_ItShouldCallTruncateOnTheSecondRegisteredDriverOnce()
  {
    var sut = new DbFixtures([this._driverOne.Object, this._driverTwo.Object]);

    string[] tables = ["gh"];
    Dictionary<string, object[]> fixtures = new Dictionary<string, object[]>
    {
      { "gh", [ ] },
    };
    await sut.InsertFixtures(tables, fixtures);

    this._driverTwo.Verify(m => m.Truncate(tables), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_ItShouldCallInsertFixturesOnTheFirstRegisteredDriverOnceWithTheExpectedArguments()
  {
    var sut = new DbFixtures([this._driverOne.Object, this._driverTwo.Object]);

    string[] tables = ["gh"];
    Dictionary<string, object[]> fixtures = new Dictionary<string, object[]>
    {
      { "gh", [ new TestFixture{} ] },
    };
    await sut.InsertFixtures(tables, fixtures);

    this._driverOne.Verify(m => m.InsertFixtures("gh", fixtures["gh"]), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_ItShouldCallInsertFixturesOnTheSecondRegisteredDriverOnceWithTheExpectedArguments()
  {
    var sut = new DbFixtures([this._driverOne.Object, this._driverTwo.Object]);

    string[] tables = ["gh"];
    Dictionary<string, object[]> fixtures = new Dictionary<string, object[]>
    {
      { "gh", [ new TestFixture{} ] },
    };
    await sut.InsertFixtures(tables, fixtures);

    this._driverTwo.Verify(m => m.InsertFixtures("gh", fixtures["gh"]), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfThereAreTwoTableNamesProvided_ItShouldCallInsertFixturesOnTheFirstRegisteredDriverOnceWithTheExpectedArgumentsForTheFirstTable()
  {
    var sut = new DbFixtures([this._driverOne.Object, this._driverTwo.Object]);

    string[] tables = ["gh", "ab"];
    Dictionary<string, object[]> fixtures = new Dictionary<string, object[]>
    {
      { "gh", [ new TestFixture{} ] },
      { "ab", [ new TestFixture{}, new TestFixture{} ] },
    };
    await sut.InsertFixtures(tables, fixtures);

    this._driverOne.Verify(m => m.InsertFixtures("gh", fixtures["gh"]), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfThereAreTwoTableNamesProvided_ItShouldCallInsertFixturesOnTheFirstRegisteredDriverOnceWithTheExpectedArgumentsForTheSecondTable()
  {
    var sut = new DbFixtures([this._driverOne.Object, this._driverTwo.Object]);

    string[] tables = ["gh", "ab"];
    Dictionary<string, object[]> fixtures = new Dictionary<string, object[]>
    {
      { "gh", [ new TestFixture{} ] },
      { "ab", [ new TestFixture{}, new TestFixture{} ] },
    };
    await sut.InsertFixtures(tables, fixtures);

    this._driverOne.Verify(m => m.InsertFixtures("ab", fixtures["ab"]), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfThereAreTwoTableNamesProvided_ItShouldCallInsertFixturesOnTheSecondRegisteredDriverOnceWithTheExpectedArgumentsForTheFirstTable()
  {
    var sut = new DbFixtures([this._driverOne.Object, this._driverTwo.Object]);

    string[] tables = ["gh", "ab"];
    Dictionary<string, object[]> fixtures = new Dictionary<string, object[]>
    {
      { "gh", [ new TestFixture{} ] },
      { "ab", [ new TestFixture{}, new TestFixture{} ] },
    };
    await sut.InsertFixtures(tables, fixtures);

    this._driverTwo.Verify(m => m.InsertFixtures("gh", fixtures["gh"]), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfThereAreTwoTableNamesProvided_ItShouldCallInsertFixturesOnTheSecondRegisteredDriverOnceWithTheExpectedArgumentsForTheSecondTable()
  {
    var sut = new DbFixtures([this._driverOne.Object, this._driverTwo.Object]);

    string[] tables = ["gh", "ab"];
    Dictionary<string, object[]> fixtures = new Dictionary<string, object[]>
    {
      { "gh", [ new TestFixture{} ] },
      { "ab", [ new TestFixture{}, new TestFixture{} ] },
    };
    await sut.InsertFixtures(tables, fixtures);

    this._driverTwo.Verify(m => m.InsertFixtures("ab", fixtures["ab"]), Times.Once());
  }
}

public class TestFixture
{
  public TestFixture() { }
}