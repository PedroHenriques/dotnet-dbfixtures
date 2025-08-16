using Moq;
using StackExchange.Redis;

namespace DbFixtures.Redis.Tests.Unit;

[Trait("Type", "Unit")]
public class RedisDriverTests : IDisposable
{
  private readonly Mock<IConnectionMultiplexer> _clientMock;
  private readonly Mock<IDatabase> _dbMock;

  public RedisDriverTests()
  {
    this._clientMock = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
    this._dbMock = new Mock<IDatabase>(MockBehavior.Strict);

    this._clientMock.Setup(s => s.Close(It.IsAny<bool>()));

    this._dbMock.Setup(s => s.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<long>(0));
    this._dbMock.Setup(s => s.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(true));
    this._dbMock.Setup(s => s.HashSetAsync(It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()))
      .Returns(Task.CompletedTask);
    this._dbMock.Setup(s => s.ListLeftPushAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue[]>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<long>(0));
    this._dbMock.Setup(s => s.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<long>(0));
    this._dbMock.Setup(s => s.StreamAddAsync(It.IsAny<RedisKey>(), It.IsAny<NameValueEntry[]>(), It.IsAny<RedisValue?>(), It.IsAny<long?>(), It.IsAny<bool>(), It.IsAny<long?>(), It.IsAny<StreamTrimMode>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<RedisValue>(""));
  }

  public void Dispose()
  {
    this._clientMock.Reset();
    this._dbMock.Reset();
  }

  [Fact]
  public async Task Close_ItShouldCallCloseOnTheClientInstanceOnce()
  {
    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { });
    await sut.Close();

    this._clientMock.Verify(m => m.Close(true), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedKeyIsSetToAString_IfTheProvidedFixturesIsAnArrayOfOneString_ItShouldCallStringSetAsyncOnTheDatabaseInstanceOnce()
  {
    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { { "str key", Types.KeyTypes.String } });
    await sut.InsertFixtures<string>("str key", [""]);

    this._dbMock.Verify(m => m.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedKeyIsSetToAString_IfTheProvidedFixturesIsAnArrayOfOneString_ItShouldCallStringSetAsyncOnTheDatabaseInstanceWithTheProvidedString()
  {
    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { { "some key", Types.KeyTypes.String } });
    await sut.InsertFixtures<string>("some key", ["some value"]);

    this._dbMock.Verify(m => m.StringSetAsync(new RedisKey("some key"), new RedisValue("some value"), null, false, When.Always, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedKeyIsSetToAString_IfTheProvidedFixturesIsAnArrayOfOneString_IfCallingStringSetAsyncOnTheDatabaseInstanceReturnFalse_ItShouldThrowAnExceptionWithTheExpectedMessage()
  {
    this._dbMock.Setup(s => s.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult(false));

    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { { "some key", Types.KeyTypes.String } });

    Exception ex = await Assert.ThrowsAsync<Exception>(async () => await sut.InsertFixtures<string>("some key", ["some value"]));
    Assert.Equal("Failed to insert string key 'some key'", ex.Message);
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedKeyIsSetToAString_IfTheProvidedFixturesIsAnEmptyArray_ItShouldNotCallStringSetAsyncOnTheDatabaseInstance()
  {
    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { { "some key", Types.KeyTypes.String } });
    await sut.InsertFixtures<string>("some key", []);

    this._dbMock.Verify(m => m.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Never());
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedKeyIsSetToAString_IfTheProvidedFixturesIsAnArrayOfMultipleStrings_ItShouldCallStringSetAsyncOnTheDatabaseInstanceWithTheFirstOfTheProvidedStrings()
  {
    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { { "some key", Types.KeyTypes.String } });
    await sut.InsertFixtures<string>("some key", ["some value", "another value"]);

    this._dbMock.Verify(m => m.StringSetAsync(new RedisKey("some key"), new RedisValue("some value"), null, false, When.Always, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedKeyIsSetToAHash_ItShouldCallHashSetAsyncOnTheDatabaseInstanceOnce()
  {
    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { { "some key", Types.KeyTypes.Hash } });
    await sut.InsertFixtures<Dictionary<string, string>>("some key", [new Dictionary<string, string> { }]);

    this._dbMock.Verify(m => m.HashSetAsync(It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedKeyIsSetToAHash_ItShouldCallHashSetAsyncOnTheDatabaseInstanceWithTheExpectedArguments()
  {
    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { { "some key", Types.KeyTypes.Hash } });
    await sut.InsertFixtures<Dictionary<string, string>>("some key", [new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }]);

    HashEntry[] expectedValues = [
      new HashEntry("key1", "value1"),
      new HashEntry("key2", "value2"),
    ];
    this._dbMock.Verify(m => m.HashSetAsync(new RedisKey("some key"), expectedValues, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedKeyIsSetToAHash_If2FixturesAreProvided_ItShouldCallHashSetAsyncOnTheDatabaseInstanceOnceWithTheExpectedArgumentsForThe1stFixture()
  {
    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { { "some hash key", Types.KeyTypes.Hash } });
    await sut.InsertFixtures<Dictionary<string, string>>("some hash key", [new Dictionary<string, string> { { "hello", "world" }, { "from", "unit test" } }, new Dictionary<string, string> { { "some key", "some value" } }]);

    HashEntry[] expectedValues = [
      new HashEntry("hello", "world"),
      new HashEntry("from", "unit test"),
    ];
    this._dbMock.Verify(m => m.HashSetAsync(new RedisKey("some hash key"), expectedValues, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedKeyIsSetToAHash_If2FixturesAreProvided_ItShouldCallHashSetAsyncOnTheDatabaseInstanceOnceWithTheExpectedArgumentsForThe2ndFixture()
  {
    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { { "some hash key", Types.KeyTypes.Hash } });
    await sut.InsertFixtures<Dictionary<string, string>>("some hash key", [new Dictionary<string, string> { { "hello", "world" }, { "from", "unit test" } }, new Dictionary<string, string> { { "some key", "some value" } }]);

    HashEntry[] expectedValues = [
      new HashEntry("some key", "some value"),
    ];
    this._dbMock.Verify(m => m.HashSetAsync(new RedisKey("some hash key"), expectedValues, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedKeyIsSetToAHash_IfTheProvidedFixturesIsAnEmptyArray_ItShouldNotCallHashSetAsyncOnTheDatabaseInstance()
  {
    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { { "some key", Types.KeyTypes.String } });
    await sut.InsertFixtures<Dictionary<string, string>>("some key", []);

    this._dbMock.Verify(m => m.HashSetAsync(It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()), Times.Never());
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedKeyIsSetToAList_ItShouldCallListLeftPushAsyncOnTheDatabaseInstanceOnce()
  {
    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { { "some list key", Types.KeyTypes.List } });
    await sut.InsertFixtures<string>("some list key", [""]);

    this._dbMock.Verify(m => m.ListLeftPushAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue[]>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedKeyIsSetToAList_ItShouldCallListLeftPushAsyncOnTheDatabaseInstanceOnceWithTheExpectedArguments()
  {
    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { { "some list key", Types.KeyTypes.List } });
    await sut.InsertFixtures<string>("some list key", ["value 1", "value 8", "prop 0"]);

    RedisValue[] expectedValues = [
      new RedisValue("value 1"), new RedisValue("value 8"), new RedisValue("prop 0"),
    ];
    this._dbMock.Verify(m => m.ListLeftPushAsync("some list key", expectedValues, When.Always, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedKeyIsSetToAList_IfTheProvidedFixturesIsAnEmptyArray_ItShouldNotCallListLeftPushAsyncOnTheDatabaseInstance()
  {
    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { { "some list key", Types.KeyTypes.List } });
    await sut.InsertFixtures<string>("some list key", []);

    this._dbMock.Verify(m => m.ListLeftPushAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue[]>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Never());
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedKeyIsSetToASet_ItShouldCallSetAddAsyncOnTheDatabaseInstanceOnce()
  {
    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { { "some set key", Types.KeyTypes.Set } });
    await sut.InsertFixtures<string>("some set key", [""]);

    this._dbMock.Verify(m => m.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedKeyIsSetToASet_ItShouldCallSetAddAsyncOnTheDatabaseInstanceOnceWithTheExpectedArguments()
  {
    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { { "some set key", Types.KeyTypes.Set } });
    await sut.InsertFixtures<string>("some set key", ["some value 1", "another value 8", "yet another prop 0"]);

    RedisValue[] expectedValues = [
      new RedisValue("some value 1"), new RedisValue("another value 8"), new RedisValue("yet another prop 0"),
    ];
    this._dbMock.Verify(m => m.SetAddAsync("some set key", expectedValues, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedKeyIsSetToASet_IfTheProvidedFixturesIsAnEmptyArray_ItShouldNotCallSetAddAsyncOnTheDatabaseInstance()
  {
    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { { "some set key", Types.KeyTypes.Set } });
    await sut.InsertFixtures<string>("some set key", []);

    this._dbMock.Verify(m => m.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()), Times.Never());
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedKeyIsSetToAStream_ItShouldCallStreamAddAsyncOnTheDatabaseInstanceOnce()
  {
    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { { "some stream key", Types.KeyTypes.Stream } });
    await sut.InsertFixtures<Dictionary<string, string>>("some stream key", [new Dictionary<string, string> { }, new Dictionary<string, string> { }]);

    this._dbMock.Verify(m => m.StreamAddAsync(It.IsAny<RedisKey>(), It.IsAny<NameValueEntry[]>(), It.IsAny<RedisValue?>(), It.IsAny<long?>(), It.IsAny<bool>(), It.IsAny<long?>(), It.IsAny<StreamTrimMode>(), It.IsAny<CommandFlags>()), Times.Exactly(2));
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedKeyIsSetToAStream_ItShouldCallStreamAddAsyncOnTheDatabaseInstanceOnceWithTheExpectedArguments()
  {
    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { { "some stream key", Types.KeyTypes.Stream } });
    await sut.InsertFixtures<Dictionary<string, string>>("some stream key", [new Dictionary<string, string> { { "hello", "world" }, { "from", "unit test" } }]);

    NameValueEntry[] expectedValues = [
      new NameValueEntry("hello", "world"),
      new NameValueEntry("from", "unit test"),
    ];
    this._dbMock.Verify(m => m.StreamAddAsync("some stream key", expectedValues, null, null, false, null, StreamTrimMode.KeepReferences, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedKeyIsSetToAStream_If2FixturesAreProvided_ItShouldCallStreamAddAsyncOnTheDatabaseInstanceOnceWithTheExpectedArgumentsForThe1stFixture()
  {
    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { { "some stream key", Types.KeyTypes.Stream } });
    await sut.InsertFixtures<Dictionary<string, string>>("some stream key", [new Dictionary<string, string> { { "hello", "world" }, { "from", "unit test" } }, new Dictionary<string, string> { { "some key", "some value" } }]);

    NameValueEntry[] expectedValues = [
      new NameValueEntry("hello", "world"),
      new NameValueEntry("from", "unit test"),
    ];
    this._dbMock.Verify(m => m.StreamAddAsync("some stream key", expectedValues, null, null, false, null, StreamTrimMode.KeepReferences, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedKeyIsSetToAStream_If2FixturesAreProvided_ItShouldCallStreamAddAsyncOnTheDatabaseInstanceOnceWithTheExpectedArgumentsForThe2ndFixture()
  {
    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { { "some stream key", Types.KeyTypes.Stream } });
    await sut.InsertFixtures<Dictionary<string, string>>("some stream key", [new Dictionary<string, string> { { "hello", "world" }, { "from", "unit test" } }, new Dictionary<string, string> { { "some key", "some value" } }]);

    NameValueEntry[] expectedValues = [
      new NameValueEntry("some key", "some value"),
    ];
    this._dbMock.Verify(m => m.StreamAddAsync("some stream key", expectedValues, null, null, false, null, StreamTrimMode.KeepReferences, CommandFlags.None), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedKeyIsSetToAStream_IfTheProvidedFixturesIsAnEmptyArray_ItShouldNotCallStreamAddAsyncOnTheDatabaseInstance()
  {
    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { { "some stream key", Types.KeyTypes.Stream } });
    await sut.InsertFixtures<Dictionary<string, string>>("some stream key", []);

    this._dbMock.Verify(m => m.StreamAddAsync(It.IsAny<RedisKey>(), It.IsAny<NameValueEntry[]>(), It.IsAny<RedisValue?>(), It.IsAny<long?>(), It.IsAny<bool>(), It.IsAny<long?>(), It.IsAny<StreamTrimMode>(), It.IsAny<CommandFlags>()), Times.Never());
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedKeyDoesNotExistInTheProvidedDictionaryOfKeyTypes_ItShouldThrowAnExceptionWithTheExpectedMessage()
  {
    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { { "some list key", Types.KeyTypes.List } });

    Exception ex = await Assert.ThrowsAsync<Exception>(async () => await sut.InsertFixtures<string>("some unknown key", [""]));
    Assert.Equal("The key 'some unknown key' doesn't have a declared type, in the dictionary provided in the constructor of this driver", ex.Message);
  }

  [Fact]
  public async Task Truncate_ItShouldCallKeyDeleteAsyncOnTheDatabaseInstanceOnce()
  {
    this._dbMock.Setup(s => s.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<long>(3));

    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { });
    await sut.Truncate(["key1", "key2", "key3"]);

    this._dbMock.Verify(m => m.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()), Times.Once());
  }

  [Fact]
  public async Task Truncate_ItShouldCallKeyDeleteAsyncOnTheDatabaseInstanceWithTheExpectedKeys()
  {
    this._dbMock.Setup(s => s.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
      .Returns(Task.FromResult<long>(3));

    var sut = new RedisDriver(this._clientMock.Object, this._dbMock.Object, new Dictionary<string, Types.KeyTypes> { });
    await sut.Truncate(["key1", "key2", "key3"]);

    RedisKey[] expectedKeys = ["key1", "key2", "key3"];
    this._dbMock.Verify(m => m.KeyDeleteAsync(expectedKeys, CommandFlags.None), Times.Once());
  }
}