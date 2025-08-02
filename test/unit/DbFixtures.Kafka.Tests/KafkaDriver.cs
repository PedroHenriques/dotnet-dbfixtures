using System.Dynamic;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Moq;

namespace DbFixtures.Kafka.Tests.Unit;

[Trait("Type", "Unit")]
public class KafkaDriverTests : IDisposable
{
  private readonly Mock<IAdminClient> _adminMock;
  private readonly Mock<IConsumer<Ignore, Ignore>> _consumerMock;
  private readonly Mock<IProducer<dynamic, dynamic>> _producerMock;

  public KafkaDriverTests()
  {
    this._adminMock = new Mock<IAdminClient>(MockBehavior.Strict);
    this._consumerMock = new Mock<IConsumer<Ignore, Ignore>>(MockBehavior.Strict);
    this._producerMock = new Mock<IProducer<dynamic, dynamic>>(MockBehavior.Strict);

    List<TopicMetadata> topics = new List<TopicMetadata>
    {
      new TopicMetadata(
        "test topic",
        new List<PartitionMetadata>{
          new PartitionMetadata(0, 0, [1], [1], null),
          new PartitionMetadata(1, 0, [1], [1], null),
        },
        null
      ),
    };

    this._adminMock.Setup(s => s.GetMetadata(It.IsAny<string>(), It.IsAny<TimeSpan>()))
      .Returns(new Metadata(new List<BrokerMetadata>(), topics, 1, ""));
    this._adminMock.Setup(s => s.DeleteRecordsAsync(It.IsAny<IEnumerable<TopicPartitionOffset>>(), It.IsAny<DeleteRecordsOptions>()))
      .Returns(Task.FromResult(new List<DeleteRecordsResult> { }));
    this._adminMock.Setup(s => s.Dispose());

    this._consumerMock.Setup(s => s.QueryWatermarkOffsets(It.IsAny<TopicPartition>(), It.IsAny<TimeSpan>()))
      .Returns(new WatermarkOffsets(1, 2));
    this._consumerMock.Setup(s => s.Close());
    this._consumerMock.Setup(s => s.Dispose());

    this._producerMock.Setup(s => s.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<dynamic, dynamic>>(), It.IsAny<CancellationToken>()))
      .Returns(Task.FromResult(new DeliveryResult<dynamic, dynamic> { }));
    this._producerMock.Setup(s => s.Flush(It.IsAny<TimeSpan>()))
      .Returns(1);
    this._producerMock.Setup(s => s.Dispose());
  }

  public void Dispose()
  {
    this._adminMock.Reset();
    this._consumerMock.Reset();
    this._producerMock.Reset();
  }

  [Fact]
  public async Task Truncate_ItShouldCallGetMetadataOnTheAdminClientOnceWithTheExpectedArguments()
  {
    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);
    await sut.Truncate(["test topic"]);

    this._adminMock.Verify(m => m.GetMetadata("test topic", TimeSpan.FromSeconds(10)), Times.Once());
  }

  [Fact]
  public async Task Truncate_ItShouldCallQueryWatermarkOffsetsOnTheConsumerClientOnceWithTheExpectedArguments()
  {
    List<TopicMetadata> topics = new List<TopicMetadata>
    {
      new TopicMetadata(
        "test topic",
        new List<PartitionMetadata>{
          new PartitionMetadata(1, 1, [1], [1], null),
        },
        null
      ),
    };

    this._adminMock.Setup(s => s.GetMetadata(It.IsAny<string>(), It.IsAny<TimeSpan>()))
      .Returns(new Metadata(new List<BrokerMetadata>(), topics, 1, ""));

    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);
    await sut.Truncate(["test topic"]);

    this._consumerMock.Verify(m => m.QueryWatermarkOffsets(new TopicPartition("test topic", new Partition(1)), TimeSpan.FromSeconds(10)), Times.Once());
  }

  [Fact]
  public async Task Truncate_ItShouldCallDeleteRecordsAsyncFromTheAdminClinetOnceWithTheExpectedArguments()
  {
    List<TopicMetadata> topics = new List<TopicMetadata>
    {
      new TopicMetadata(
        "test topic",
        new List<PartitionMetadata>{
          new PartitionMetadata(0, 0, [1], [1], null),
          new PartitionMetadata(1, 0, [1], [1], null),
        },
        null
      ),
    };
    var offsets = new List<TopicPartitionOffset>{
      {
        new TopicPartitionOffset(
          new TopicPartition("test topic", new Partition(0)),
          new Offset(7)
        )
      },
      {
        new TopicPartitionOffset(
          new TopicPartition("test topic", new Partition(1)),
          new Offset(13)
        )
      },
    };

    this._adminMock.Setup(s => s.GetMetadata(It.IsAny<string>(), It.IsAny<TimeSpan>()))
      .Returns(new Metadata(new List<BrokerMetadata>(), topics, 1, ""));

    this._consumerMock.SetupSequence(s => s.QueryWatermarkOffsets(It.IsAny<TopicPartition>(), It.IsAny<TimeSpan>()))
      .Returns(new WatermarkOffsets(1, 7))
      .Returns(new WatermarkOffsets(5, 13));

    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);
    await sut.Truncate(["test topic"]);

    this._adminMock.Verify(m => m.DeleteRecordsAsync(offsets, null), Times.Once());
  }

  [Fact]
  public async Task Truncate_IfTwoValuesArePassedInTheTableNamesArgument_ItShouldCallGetMetadataOnTheAdminClientTwice()
  {
    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);
    await sut.Truncate(["test topic", "other topic"]);

    this._adminMock.Verify(m => m.GetMetadata(It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Exactly(2));
  }

  [Fact]
  public async Task Truncate_IfTwoValuesArePassedInTheTableNamesArgument_ItShouldCallGetMetadataOnTheAdminClientWithTheExpectedValuesForTheFirstElement()
  {
    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);
    await sut.Truncate(["test topic", "other topic"]);

    this._adminMock.Verify(m => m.GetMetadata("test topic", TimeSpan.FromSeconds(10)), Times.Once());
  }

  [Fact]
  public async Task Truncate_IfTwoValuesArePassedInTheTableNamesArgument_ItShouldCallGetMetadataOnTheAdminClientWithTheExpectedValuesForTheSecondElement()
  {
    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);
    await sut.Truncate(["test topic", "other topic"]);

    this._adminMock.Verify(m => m.GetMetadata("other topic", TimeSpan.FromSeconds(10)), Times.Once());
  }

  [Fact]
  public async Task Truncate_IfTwoValuesArePassedInTheTableNamesArgument_IfTheFirstTopicHas1PartitionAndTheSecondHas3Partitions_ItShouldCallQueryWatermarkOffsetsOnTheConsumerClient4Times()
  {
    List<TopicMetadata> firstTopic = new List<TopicMetadata>
    {
      new TopicMetadata(
        "test topic",
        new List<PartitionMetadata>{
          new PartitionMetadata(0, 0, [1], [1], null),
        },
        null
      ),
    };
    List<TopicMetadata> secondTopic = new List<TopicMetadata>
    {
      new TopicMetadata(
        "other topic",
        new List<PartitionMetadata>{
          new PartitionMetadata(0, 0, [1], [1], null),
          new PartitionMetadata(1, 0, [1], [1], null),
          new PartitionMetadata(2, 0, [1], [1], null),
        },
        null
      ),
    };
    this._adminMock.SetupSequence(s => s.GetMetadata(It.IsAny<string>(), It.IsAny<TimeSpan>()))
      .Returns(new Metadata(new List<BrokerMetadata>(), firstTopic, 1, ""))
      .Returns(new Metadata(new List<BrokerMetadata>(), secondTopic, 1, ""));

    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);
    await sut.Truncate(["test topic", "other topic"]);

    this._consumerMock.Verify(m => m.QueryWatermarkOffsets(It.IsAny<TopicPartition>(), It.IsAny<TimeSpan>()), Times.Exactly(4));
  }

  [Fact]
  public async Task Truncate_IfTwoValuesArePassedInTheTableNamesArgument_IfTheFirstTopicHas1PartitionAndTheSecondHas3Partitions_ItShouldCallQueryWatermarkOffsetsOnTheConsumerClientWithTheExpectedArgumentsForTheFirstPartitionOfTheFirstTopic()
  {
    List<TopicMetadata> firstTopic = new List<TopicMetadata>
    {
      new TopicMetadata(
        "test topic",
        new List<PartitionMetadata>{
          new PartitionMetadata(0, 0, [1], [1], null),
        },
        null
      ),
    };
    List<TopicMetadata> secondTopic = new List<TopicMetadata>
    {
      new TopicMetadata(
        "other topic",
        new List<PartitionMetadata>{
          new PartitionMetadata(0, 0, [1], [1], null),
          new PartitionMetadata(1, 0, [1], [1], null),
          new PartitionMetadata(2, 0, [1], [1], null),
        },
        null
      ),
    };
    this._adminMock.SetupSequence(s => s.GetMetadata(It.IsAny<string>(), It.IsAny<TimeSpan>()))
      .Returns(new Metadata(new List<BrokerMetadata>(), firstTopic, 1, ""))
      .Returns(new Metadata(new List<BrokerMetadata>(), secondTopic, 1, ""));

    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);
    await sut.Truncate(["test topic", "other topic"]);

    this._consumerMock.Verify(m => m.QueryWatermarkOffsets(new TopicPartition("test topic", new Partition(0)), TimeSpan.FromSeconds(10)), Times.Once());
  }

  [Fact]
  public async Task Truncate_IfTwoValuesArePassedInTheTableNamesArgument_IfTheFirstTopicHas1PartitionAndTheSecondHas3Partitions_ItShouldCallQueryWatermarkOffsetsOnTheConsumerClientWithTheExpectedArgumentsForTheFirstPartitionOfTheSecondTopic()
  {
    List<TopicMetadata> firstTopic = new List<TopicMetadata>
    {
      new TopicMetadata(
        "test topic",
        new List<PartitionMetadata>{
          new PartitionMetadata(0, 0, [1], [1], null),
        },
        null
      ),
    };
    List<TopicMetadata> secondTopic = new List<TopicMetadata>
    {
      new TopicMetadata(
        "other topic",
        new List<PartitionMetadata>{
          new PartitionMetadata(1, 0, [1], [1], null),
          new PartitionMetadata(2, 0, [1], [1], null),
          new PartitionMetadata(3, 0, [1], [1], null),
        },
        null
      ),
    };
    this._adminMock.SetupSequence(s => s.GetMetadata(It.IsAny<string>(), It.IsAny<TimeSpan>()))
      .Returns(new Metadata(new List<BrokerMetadata>(), firstTopic, 1, ""))
      .Returns(new Metadata(new List<BrokerMetadata>(), secondTopic, 1, ""));

    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);
    await sut.Truncate(["test topic", "other topic"]);

    this._consumerMock.Verify(m => m.QueryWatermarkOffsets(new TopicPartition("other topic", new Partition(1)), TimeSpan.FromSeconds(10)), Times.Once());
  }

  [Fact]
  public async Task Truncate_IfTwoValuesArePassedInTheTableNamesArgument_IfTheFirstTopicHas1PartitionAndTheSecondHas3Partitions_ItShouldCallQueryWatermarkOffsetsOnTheConsumerClientWithTheExpectedArgumentsForTheSecondPartitionOfTheSecondTopic()
  {
    List<TopicMetadata> firstTopic = new List<TopicMetadata>
    {
      new TopicMetadata(
        "test topic",
        new List<PartitionMetadata>{
          new PartitionMetadata(0, 0, [1], [1], null),
        },
        null
      ),
    };
    List<TopicMetadata> secondTopic = new List<TopicMetadata>
    {
      new TopicMetadata(
        "other topic",
        new List<PartitionMetadata>{
          new PartitionMetadata(1, 0, [1], [1], null),
          new PartitionMetadata(2, 0, [1], [1], null),
          new PartitionMetadata(3, 0, [1], [1], null),
        },
        null
      ),
    };
    this._adminMock.SetupSequence(s => s.GetMetadata(It.IsAny<string>(), It.IsAny<TimeSpan>()))
      .Returns(new Metadata(new List<BrokerMetadata>(), firstTopic, 1, ""))
      .Returns(new Metadata(new List<BrokerMetadata>(), secondTopic, 1, ""));

    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);
    await sut.Truncate(["test topic", "other topic"]);

    this._consumerMock.Verify(m => m.QueryWatermarkOffsets(new TopicPartition("other topic", new Partition(2)), TimeSpan.FromSeconds(10)), Times.Once());
  }

  [Fact]
  public async Task Truncate_IfTwoValuesArePassedInTheTableNamesArgument_IfTheFirstTopicHas1PartitionAndTheSecondHas3Partitions_ItShouldCallQueryWatermarkOffsetsOnTheConsumerClientWithTheExpectedArgumentsForTheThirdPartitionOfTheSecondTopic()
  {
    List<TopicMetadata> firstTopic = new List<TopicMetadata>
    {
      new TopicMetadata(
        "test topic",
        new List<PartitionMetadata>{
          new PartitionMetadata(0, 0, [1], [1], null),
        },
        null
      ),
    };
    List<TopicMetadata> secondTopic = new List<TopicMetadata>
    {
      new TopicMetadata(
        "other topic",
        new List<PartitionMetadata>{
          new PartitionMetadata(1, 0, [1], [1], null),
          new PartitionMetadata(2, 0, [1], [1], null),
          new PartitionMetadata(3, 0, [1], [1], null),
        },
        null
      ),
    };
    this._adminMock.SetupSequence(s => s.GetMetadata(It.IsAny<string>(), It.IsAny<TimeSpan>()))
      .Returns(new Metadata(new List<BrokerMetadata>(), firstTopic, 1, ""))
      .Returns(new Metadata(new List<BrokerMetadata>(), secondTopic, 1, ""));

    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);
    await sut.Truncate(["test topic", "other topic"]);

    this._consumerMock.Verify(m => m.QueryWatermarkOffsets(new TopicPartition("other topic", new Partition(3)), TimeSpan.FromSeconds(10)), Times.Once());
  }

  [Fact]
  public async Task Truncate_IfCallingDeleteRecordsAsyncOnTheAdminClientThrowsAnException_ItShouldLetItBubbleUp()
  {
    var testEx = new Exception("message from unit test");
    this._adminMock.Setup(s => s.DeleteRecordsAsync(It.IsAny<IEnumerable<TopicPartitionOffset>>(), It.IsAny<DeleteRecordsOptions>()))
      .ThrowsAsync(testEx);

    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);

    Exception ex = await Assert.ThrowsAsync<Exception>(async () => await sut.Truncate(["test topic", "other topic"]));
    Assert.Equal(testEx, ex);
  }

  [Fact]
  public async Task Truncate_IfATopicHasNoEventsInASpecificPartition_ItShouldNotAddThatPartitionWhenCallingDeleteRecordsAsyncOnTheAdminClient()
  {
    List<TopicMetadata> topics = new List<TopicMetadata>
    {
      new TopicMetadata(
        "test topic",
        new List<PartitionMetadata>{
          new PartitionMetadata(0, 0, [1], [1], null),
          new PartitionMetadata(1, 0, [1], [1], null),
        },
        null
      ),
    };
    var offsets = new List<TopicPartitionOffset>{
      {
        new TopicPartitionOffset(
          new TopicPartition("test topic", new Partition(1)),
          new Offset(5)
        )
      },
    };

    this._adminMock.Setup(s => s.GetMetadata(It.IsAny<string>(), It.IsAny<TimeSpan>()))
      .Returns(new Metadata(new List<BrokerMetadata>(), topics, 1, ""));

    this._consumerMock.SetupSequence(s => s.QueryWatermarkOffsets(It.IsAny<TopicPartition>(), It.IsAny<TimeSpan>()))
      .Returns(new WatermarkOffsets(0, 0))
      .Returns(new WatermarkOffsets(0, 5));

    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);
    await sut.Truncate(["test topic"]);

    this._adminMock.Verify(m => m.DeleteRecordsAsync(offsets, null), Times.Once());
  }

  [Fact]
  public async Task Truncate_IfATopicHasNoEventsInAnyOfItsPartitions_ItShouldNotCallDeleteRecordsAsyncOnTheAdminClient()
  {
    List<TopicMetadata> topics = new List<TopicMetadata>
    {
      new TopicMetadata(
        "test topic",
        new List<PartitionMetadata>{
          new PartitionMetadata(0, 0, [1], [1], null),
          new PartitionMetadata(1, 0, [1], [1], null),
        },
        null
      ),
    };
    var offsets = new List<TopicPartitionOffset>{
      {
        new TopicPartitionOffset(
          new TopicPartition("test topic", new Partition(0)),
          new Offset(0)
        )
      },
      {
        new TopicPartitionOffset(
          new TopicPartition("test topic", new Partition(1)),
          new Offset(0)
        )
      },
    };

    this._adminMock.Setup(s => s.GetMetadata(It.IsAny<string>(), It.IsAny<TimeSpan>()))
      .Returns(new Metadata(new List<BrokerMetadata>(), topics, 1, ""));

    this._consumerMock.SetupSequence(s => s.QueryWatermarkOffsets(It.IsAny<TopicPartition>(), It.IsAny<TimeSpan>()))
      .Returns(new WatermarkOffsets(0, 0))
      .Returns(new WatermarkOffsets(0, 0));

    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);
    await sut.Truncate(["test topic"]);

    this._adminMock.Verify(m => m.DeleteRecordsAsync(It.IsAny<IEnumerable<TopicPartitionOffset>>(), It.IsAny<DeleteRecordsOptions>()), Times.Never());
  }

  [Fact]
  public async Task InsertFixtures_ItShouldCallProduceAsyncOnTheProducerInstanceOnceWithTheExpectedArguments()
  {
    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);

    var message = new Message<dynamic, dynamic> { };
    Message<dynamic, dynamic>[] messages = [message];

    await sut.InsertFixtures("test topic", messages);

    this._producerMock.Verify(m => m.ProduceAsync("test topic", message, default), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfThereAre3Fixtures_ItShouldCallProduceAsyncOnTheProducerInstance3Times()
  {
    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);

    Message<dynamic, dynamic>[] messages = [
      new Message<dynamic, dynamic> { },
      new Message<dynamic, dynamic> { },
      new Message<dynamic, dynamic> { },
    ];

    await sut.InsertFixtures("test topic", messages);

    this._producerMock.Verify(m => m.ProduceAsync("test topic", It.IsAny<Message<dynamic, dynamic>>(), default), Times.Exactly(3));
  }

  [Fact]
  public async Task InsertFixtures_IfThereAre3Fixtures_ItShouldCallProduceAsyncOnTheProducerInstanceWithTheExpectedArgumentsForTheFirstFixture()
  {
    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);

    var message = new Message<dynamic, dynamic>
    {
      Key = new ExpandoObject(),
      Value = new ExpandoObject()
    };
    message.Key.id = "1st key";
    message.Value.something = "1st something";

    Message<dynamic, dynamic>[] messages = [
      message,
      new Message<dynamic, dynamic> { },
      new Message<dynamic, dynamic> { },
    ];

    await sut.InsertFixtures("test topic", messages);

    this._producerMock.Verify(m => m.ProduceAsync("test topic", message, default), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfThereAre3Fixtures_ItShouldCallProduceAsyncOnTheProducerInstanceWithTheExpectedArgumentsForTheSecondFixture()
  {
    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);

    var message = new Message<dynamic, dynamic>
    {
      Key = new ExpandoObject(),
      Value = new ExpandoObject()
    };
    message.Key.id = "2nd key";
    message.Value.something = "2nd something";

    Message<dynamic, dynamic>[] messages = [
      new Message<dynamic, dynamic> { },
      message,
      new Message<dynamic, dynamic> { },
    ];

    await sut.InsertFixtures("test topic", messages);

    this._producerMock.Verify(m => m.ProduceAsync("test topic", message, default), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfThereAre3Fixtures_ItShouldCallProduceAsyncOnTheProducerInstanceWithTheExpectedArgumentsForTheThirdFixture()
  {
    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);

    var message = new Message<dynamic, dynamic>
    {
      Key = new ExpandoObject(),
      Value = new ExpandoObject()
    };
    message.Key.id = "3rd key";
    message.Value.something = "3rd something";

    Message<dynamic, dynamic>[] messages = [
      new Message<dynamic, dynamic> { },
      new Message<dynamic, dynamic> { },
      message,
    ];

    await sut.InsertFixtures("test topic", messages);

    this._producerMock.Verify(m => m.ProduceAsync("test topic", message, default), Times.Once());
  }

  [Fact]
  public async Task InsertFixtures_IfCallingProduceAsyncOnTheProducerInstanceThrowsAnException_ItShouldLetItBubbleUp()
  {
    var testEx = new Exception("some test message.");
    this._producerMock.Setup(s => s.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<dynamic, dynamic>>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(testEx);

    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);

    Message<dynamic, dynamic>[] messages = [
      new Message<dynamic, dynamic> { },
      new Message<dynamic, dynamic> { },
      new Message<dynamic, dynamic> { },
    ];

    Exception ex = await Assert.ThrowsAsync<Exception>(async () => await sut.InsertFixtures("test topic", messages));
    Assert.Equal(testEx, ex);
  }

  [Fact]
  public async Task Close_ItShouldCallCloseOnTheConsumerInstanceOnce()
  {
    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);
    await sut.Close();

    this._consumerMock.Verify(m => m.Close(), Times.Once());
  }

  [Fact]
  public async Task Close_ItShouldCallDisposeOnTheConsumerInstanceOnce()
  {
    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);
    await sut.Close();

    this._consumerMock.Verify(m => m.Dispose(), Times.Once());
  }

  [Fact]
  public async Task Close_ItShouldCallFlushOnTheProducerInstanceOnce()
  {
    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);
    await sut.Close();

    this._producerMock.Verify(m => m.Flush(TimeSpan.FromSeconds(5)), Times.Once());
  }

  [Fact]
  public async Task Close_ItShouldCallDisposeOnTheProducerInstanceOnce()
  {
    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);
    await sut.Close();

    this._producerMock.Verify(m => m.Dispose(), Times.Once());
  }

  [Fact]
  public async Task Close_ItShouldCallDisposeOnTheAdminInstanceOnce()
  {
    var sut = new KafkaDriver<dynamic, dynamic>(this._adminMock.Object, this._consumerMock.Object, this._producerMock.Object);
    await sut.Close();

    this._adminMock.Verify(m => m.Dispose(), Times.Once());
  }
}