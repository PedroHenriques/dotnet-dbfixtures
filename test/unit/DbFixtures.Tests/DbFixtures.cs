using Moq;
using SharedLibs.Types;

namespace DbFixtures.Tests.Unit;

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
  public async Task CloseDrivers_IfCallingCloseOnFirstRegisteredDriverThrowsAnException_ItShouldLetItBubbleUp()
  {
    var testEx = new Exception("ex msg from unit test.");
    this._driverOne.Setup(s => s.Close())
      .ThrowsAsync(testEx);

    var sut = new DbFixtures([this._driverOne.Object, this._driverTwo.Object]);

    var ex = await Assert.ThrowsAsync<Exception>(async () => await sut.CloseDrivers());
    Assert.Equal(testEx, ex);
  }

  [Fact]
  public async Task CloseDrivers_IfCallingCloseOnSecondRegisteredDriverThrowsAnException_ItShouldLetItBubbleUp()
  {
    var testEx = new Exception("ex msg from unit test.");
    this._driverTwo.Setup(s => s.Close())
      .ThrowsAsync(testEx);

    var sut = new DbFixtures([this._driverOne.Object, this._driverTwo.Object]);

    var ex = await Assert.ThrowsAsync<Exception>(async () => await sut.CloseDrivers());
    Assert.Equal(testEx, ex);
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

  [Fact]
  public async Task InsertFixtures_IFCallingTruncateOnTheFirstRegisteredDriverThrowsAnException_ItShouldLetItBubbleUp()
  {
    var testEx = new Exception("test ex msg");
    this._driverOne.Setup(s => s.Truncate(It.IsAny<string[]>()))
      .ThrowsAsync(testEx);

    var sut = new DbFixtures([this._driverOne.Object, this._driverTwo.Object]);

    string[] tables = ["gh"];
    Dictionary<string, object[]> fixtures = new Dictionary<string, object[]>
    {
      { "gh", [ ] },
    };
    var ex = await Assert.ThrowsAsync<Exception>(async () => await sut.InsertFixtures(tables, fixtures));
    Assert.Equal(testEx, ex);
  }

  [Fact]
  public async Task InsertFixtures_IFCallingTruncateOnTheSecondRegisteredDriverThrowsAnException_ItShouldLetItBubbleUp()
  {
    var testEx = new Exception("test ex msg");
    this._driverTwo.Setup(s => s.Truncate(It.IsAny<string[]>()))
      .ThrowsAsync(testEx);

    var sut = new DbFixtures([this._driverOne.Object, this._driverTwo.Object]);

    string[] tables = ["gh"];
    Dictionary<string, object[]> fixtures = new Dictionary<string, object[]>
    {
      { "gh", [ ] },
    };
    var ex = await Assert.ThrowsAsync<Exception>(async () => await sut.InsertFixtures(tables, fixtures));
    Assert.Equal(testEx, ex);
  }

  [Fact]
  public async Task InsertFixtures_IFCallingInsertFixturesOnTheFirstRegisteredDriverThrowsAnException_ItShouldLetItBubbleUp()
  {
    var testEx = new Exception("test ex msg");
    this._driverOne.Setup(s => s.InsertFixtures(It.IsAny<string>(), It.IsAny<object[]>()))
      .ThrowsAsync(testEx);

    var sut = new DbFixtures([this._driverOne.Object, this._driverTwo.Object]);

    string[] tables = ["gh"];
    Dictionary<string, object[]> fixtures = new Dictionary<string, object[]>
    {
      { "gh", [ ] },
    };
    var ex = await Assert.ThrowsAsync<Exception>(async () => await sut.InsertFixtures(tables, fixtures));
    Assert.Equal(testEx, ex);
  }

  [Fact]
  public async Task InsertFixtures_IFCallingInsertFixturesOnTheSecondRegisteredDriverThrowsAnException_ItShouldLetItBubbleUp()
  {
    var testEx = new Exception("test ex msg");
    this._driverTwo.Setup(s => s.InsertFixtures(It.IsAny<string>(), It.IsAny<object[]>()))
      .ThrowsAsync(testEx);

    var sut = new DbFixtures([this._driverOne.Object, this._driverTwo.Object]);

    string[] tables = ["gh"];
    Dictionary<string, object[]> fixtures = new Dictionary<string, object[]>
    {
      { "gh", [ ] },
    };
    var ex = await Assert.ThrowsAsync<Exception>(async () => await sut.InsertFixtures(tables, fixtures));
    Assert.Equal(testEx, ex);
  }
}

public class TestFixture
{
  public TestFixture() { }
}