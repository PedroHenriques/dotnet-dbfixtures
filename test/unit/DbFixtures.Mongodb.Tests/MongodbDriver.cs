using MongoDB.Driver;
using Moq;

namespace DbFixtures.Mongodb.Tests.Unit;

[Trait("Type", "Unit")]
public class MongodbDriverTests : IDisposable
{
  private readonly Mock<IMongoClient> _clientMock;
  private readonly Mock<IMongoDatabase> _dbMock;
  private readonly Mock<IMongoCollection<object>> _collMock;

  public MongodbDriverTests()
  {
    this._clientMock = new Mock<IMongoClient>(MockBehavior.Strict);
    this._dbMock = new Mock<IMongoDatabase>(MockBehavior.Strict);
    this._collMock = new Mock<IMongoCollection<object>>(MockBehavior.Strict);

    this._clientMock.Setup(s => s.Dispose());
    this._clientMock.Setup(s => s.GetDatabase(It.IsAny<string>(), It.IsAny<MongoDatabaseSettings>()))
      .Returns(this._dbMock.Object);

    this._dbMock.Setup(s => s.GetCollection<object>(It.IsAny<string>(), It.IsAny<MongoCollectionSettings>()))
      .Returns(this._collMock.Object);
    this._dbMock.Setup(s => s.DropCollectionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    this._collMock.Setup(s => s.InsertManyAsync(It.IsAny<IEnumerable<object>>(), It.IsAny<InsertManyOptions>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);
  }

  public void Dispose()
  {
    this._clientMock.Reset();
    this._dbMock.Reset();
    this._collMock.Reset();
  }

  [Fact]
  public void Constructor_ItShouldCallGetDatabaseOnTheMongoClientOnceWithTheExpectedArguments()
  {
    var dbOpts = new MongoDatabaseSettings { };
    var sut = new MongodbDriver(this._clientMock.Object, "testDb", dbOpts);

    this._clientMock.Verify(m => m.GetDatabase("testDb", dbOpts), Times.Once());
  }

  [Fact]
  public async Task Close_ItShouldCallDisposeOnTheMongoClientOnce()
  {
    var sut = new MongodbDriver(this._clientMock.Object, "testDb");

    await sut.Close();

    this._clientMock.Verify(m => m.Dispose(), Times.Once());
  }

  [Fact]
  public async Task Close_IfCallingDisposeOnTheMongoClientThrowsAnException_ItShouldLetItBubbleUp()
  {
    var testEx = new Exception("test msg.");
    this._clientMock.Setup(s => s.Dispose())
      .Throws(testEx);

    var sut = new MongodbDriver(this._clientMock.Object, "testDb");

    var ex = await Assert.ThrowsAsync<Exception>(async () => await sut.Close());
    Assert.Equal(testEx, ex);
  }

  [Fact]
  public async Task InsertFixtures_ItShouldCallGetCollectionOnTheDatabaseInstanceOnceWithTheExpectedArguments()
  {
    var sut = new MongodbDriver(this._clientMock.Object, "testDb");

    await sut.InsertFixtures<object>("testColl", [""]);

    this._dbMock.Verify(m => m.GetCollection<object>("testColl", null), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_ItShouldCallInsertManyAsyncOnTheCollectionInstanceOnceWithTheExpectedArguments()
  {
    var sut = new MongodbDriver(this._clientMock.Object, "testDb");

    object[] fixtures = [""];
    await sut.InsertFixtures("testColl", fixtures);

    this._collMock.Verify(m => m.InsertManyAsync(fixtures, null, default), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfTheProvidedFixturesIsEmpty_ItShouldNotCallInsertManyAsyncOnTheCollectionInstance()
  {
    var sut = new MongodbDriver(this._clientMock.Object, "testDb");

    await sut.InsertFixtures<object>("testColl", []);

    this._collMock.Verify(m => m.InsertManyAsync(It.IsAny<IEnumerable<object>>(), It.IsAny<InsertManyOptions>(), It.IsAny<CancellationToken>()), Times.Never());
  }

  [Fact]
  public async Task InsertFixtures_IfCallingGetCollectionOnTheDatabaseInstanceThrowsAnException_ItShouldLetItBubbleUp()
  {
    var testEx = new Exception("something random");
    this._dbMock.Setup(s => s.GetCollection<object>(It.IsAny<string>(), It.IsAny<MongoCollectionSettings>()))
      .Throws(testEx);

    var sut = new MongodbDriver(this._clientMock.Object, "testDb");

    var ex = await Assert.ThrowsAsync<Exception>(async () => await sut.InsertFixtures<object>("testColl", [""]));
    Assert.Equal(testEx, ex);
  }

  [Fact]
  public async Task InsertFixtures_IfCallingInsertManyAsyncOnTheCollectionInstanceThrowsAnException_ItShouldLetItBubbleUp()
  {
    var testEx = new Exception("something random");
    this._collMock.Setup(s => s.InsertManyAsync(It.IsAny<IEnumerable<object>>(), It.IsAny<InsertManyOptions>(), It.IsAny<CancellationToken>()))
      .Throws(testEx);

    var sut = new MongodbDriver(this._clientMock.Object, "testDb");

    var ex = await Assert.ThrowsAsync<Exception>(async () => await sut.InsertFixtures<object>("testColl", [""]));
    Assert.Equal(testEx, ex);
  }

  [Fact]
  public async Task Truncate_IfTheProvidedArrayOfCollectionsHasTwoEntries_ItShouldCallDropCollectionAsyncOnTheDatabaseInstanceOnceWithTheExpectedArgumentsForTheFirstEntry()
  {
    var sut = new MongodbDriver(this._clientMock.Object, "testDb");

    await sut.Truncate(["some coll", "another collection"]);

    this._dbMock.Verify(m => m.DropCollectionAsync("some coll", default), Times.Once());
  }

  [Fact]
  public async Task Truncate_IfTheProvidedArrayOfCollectionsHasTwoEntries_ItShouldCallDropCollectionAsyncOnTheDatabaseInstanceOnceWithTheExpectedArgumentsForTheSecondEntry()
  {
    var sut = new MongodbDriver(this._clientMock.Object, "testDb");

    await sut.Truncate(["some coll", "another collection"]);

    this._dbMock.Verify(m => m.DropCollectionAsync("another collection", default), Times.Once());
  }

  [Fact]
  public async Task Truncate_IfTheProvidedArrayOfCollectionsHasTwoEntries_IFCallingDropCollectionAsyncOnTheDatabaseInstanceThrowsAnException_ItShouldLetItBubbleUp()
  {
    var testEx = new Exception("something");
    this._dbMock.Setup(s => s.DropCollectionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Throws(testEx);

    var sut = new MongodbDriver(this._clientMock.Object, "testDb");

    var ex = await Assert.ThrowsAsync<Exception>(async () => await sut.Truncate(["some coll", "another collection"]));
    Assert.Equal(testEx, ex);
  }
}