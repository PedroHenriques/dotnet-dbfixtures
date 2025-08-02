using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Newtonsoft.Json;

namespace DbFixtures.Kafka.Tests.Integration;

[Trait("Type", "Integration")]
public class KafkaDriverTests : IDisposable, IAsyncLifetime
{
  private const string TOPIC_NAME = "testTopic";
  private const string SAFE_TOPIC_NAME = "safeTopic";
  private readonly IAdminClient _adminClient;
  private readonly IConsumer<Ignore, Ignore> _consumer;
  private readonly IConsumer<TestKey, TestValue> _realConsumer;
  private readonly IProducer<TestKey, TestValue> _producer;
  private readonly KafkaDriver<TestKey, TestValue> _sut;

  public KafkaDriverTests()
  {
    this._adminClient = new AdminClientBuilder(
      new AdminClientConfig { BootstrapServers = "broker:29092" }
    ).Build();

    this._consumer = new ConsumerBuilder<Ignore, Ignore>(
      new ConsumerConfig
      {
        BootstrapServers = "broker:29092",
        GroupId = "cleanup-group",
        AutoOffsetReset = AutoOffsetReset.Latest
      }
    ).Build();
    this._realConsumer = new ConsumerBuilder<TestKey, TestValue>(
      new ConsumerConfig
      {
        BootstrapServers = "broker:29092",
        GroupId = "real-group",
        AutoOffsetReset = AutoOffsetReset.Earliest,
      }
    )
    .SetKeyDeserializer(new NewtonsoftJsonDeserializer<TestKey>())
    .SetValueDeserializer(new NewtonsoftJsonDeserializer<TestValue>())
    .SetErrorHandler((_, e) => Console.WriteLine($"Error: {e.Reason}"))
    .Build();

    this._producer = new ProducerBuilder<TestKey, TestValue>(
      new ProducerConfig
      {
        BootstrapServers = "broker:29092",
      }
    )
    .SetKeySerializer(new NewtonsoftJsonSerializer<TestKey>())
    .SetValueSerializer(new NewtonsoftJsonSerializer<TestValue>())
    .Build();

    this._sut = new KafkaDriver<TestKey, TestValue>(this._adminClient, this._consumer, this._producer);
  }

  public void Dispose()
  {
    this._consumer.Close();
    this._consumer.Dispose();
    this._realConsumer.Close();
    this._realConsumer.Dispose();
    this._producer.Flush(TimeSpan.FromSeconds(5));
    this._producer.Dispose();
    this._adminClient.Dispose();
  }

  public Task DisposeAsync()
  {
    return Task.CompletedTask;
  }

  public async Task InitializeAsync()
  {
    try
    {
      await _adminClient.CreateTopicsAsync(new[]
      {
        new TopicSpecification { Name = SAFE_TOPIC_NAME, NumPartitions = 1, ReplicationFactor = 1 },
        new TopicSpecification { Name = TOPIC_NAME, NumPartitions = 2, ReplicationFactor = 1 },
      });
    }
    catch (CreateTopicsException ex)
    {
      if (ex.Results.Any(r => r.Error.Code != ErrorCode.TopicAlreadyExists)) { throw; }
    }

    await this._sut.Truncate(new[] { SAFE_TOPIC_NAME, TOPIC_NAME });
  }

  [Fact]
  public async Task Truncate_ItShouldDeleteAllMessagesInAllPartitionsOfTheCorrectTopic()
  {
    await this._producer.ProduceAsync(
      new TopicPartition(SAFE_TOPIC_NAME, new Partition(0)),
      new Message<TestKey, TestValue>
      {
        Key = new TestKey { Id = "safe key 1" },
        Value = new TestValue { Name = "safe value 1" }
      }
    );

    var safeFirstOffsets = this._consumer.QueryWatermarkOffsets(new TopicPartition(SAFE_TOPIC_NAME, new Partition(0)), TimeSpan.FromSeconds(5));

    Assert.Equal(1, safeFirstOffsets.High - safeFirstOffsets.Low);

    await this._producer.ProduceAsync(
      new TopicPartition(TOPIC_NAME, new Partition(0)),
      new Message<TestKey, TestValue>
      {
        Key = new TestKey { Id = "key 1" },
        Value = new TestValue { Name = "value 1" }
      }
    );
    await this._producer.ProduceAsync(
      new TopicPartition(TOPIC_NAME, new Partition(0)),
      new Message<TestKey, TestValue>
      {
        Key = new TestKey { Id = "key 2" },
        Value = new TestValue { Name = "value 2" }
      }
    );
    await this._producer.ProduceAsync(
      new TopicPartition(TOPIC_NAME, new Partition(1)),
      new Message<TestKey, TestValue>
      {
        Key = new TestKey { },
        Value = new TestValue { Name = "value 3" }
      }
    );

    var firstOffsets = this._consumer.QueryWatermarkOffsets(new TopicPartition(TOPIC_NAME, new Partition(0)), TimeSpan.FromSeconds(5));
    var secondOffsets = this._consumer.QueryWatermarkOffsets(new TopicPartition(TOPIC_NAME, new Partition(1)), TimeSpan.FromSeconds(5));

    Assert.Equal(2, firstOffsets.High - firstOffsets.Low);
    Assert.Equal(1, secondOffsets.High - secondOffsets.Low);

    await this._sut.Truncate([TOPIC_NAME]);

    safeFirstOffsets = this._consumer.QueryWatermarkOffsets(new TopicPartition(SAFE_TOPIC_NAME, new Partition(0)), TimeSpan.FromSeconds(5));

    Assert.Equal(1, safeFirstOffsets.High - safeFirstOffsets.Low);

    firstOffsets = this._consumer.QueryWatermarkOffsets(new TopicPartition(TOPIC_NAME, new Partition(0)), TimeSpan.FromSeconds(5));
    secondOffsets = this._consumer.QueryWatermarkOffsets(new TopicPartition(TOPIC_NAME, new Partition(1)), TimeSpan.FromSeconds(5));

    Assert.Equal(0, firstOffsets.High - firstOffsets.Low);
    Assert.Equal(0, secondOffsets.High - secondOffsets.Low);
  }

  [Fact]
  public async Task InsertFixtures_ItShouldInsertTheRequestedEventsInTheCorrectTopic()
  {
    Message<TestKey, TestValue>[] expectedMessages = [
      new Message<TestKey, TestValue>
      {
        Key = new TestKey { Id = "safe key 1" },
        Value = new TestValue { Name = "safe value 1" }
      },
      new Message<TestKey, TestValue>
      {
        Key = new TestKey { Id = "safe key 2" },
        Value = new TestValue { Name = "safe value 2" }
      },
      new Message<TestKey, TestValue>
      {
        Key = new TestKey { Id = "safe key 3" },
        Value = new TestValue { Name = "safe value 3" }
      },
    ];

    await this._producer.ProduceAsync(new TopicPartition(SAFE_TOPIC_NAME, new Partition(0)), expectedMessages[0]);

    var safeFirstOffsets = this._consumer.QueryWatermarkOffsets(new TopicPartition(SAFE_TOPIC_NAME, new Partition(0)), TimeSpan.FromSeconds(5));

    Assert.Equal(1, safeFirstOffsets.High - safeFirstOffsets.Low);

    await this._producer.ProduceAsync(
      new TopicPartition(TOPIC_NAME, new Partition(0)),
      new Message<TestKey, TestValue>
      {
        Key = new TestKey { Id = "key 1" },
        Value = new TestValue { Name = "value 1" }
      }
    );
    await this._producer.ProduceAsync(
      new TopicPartition(TOPIC_NAME, new Partition(1)),
      new Message<TestKey, TestValue>
      {
        Key = new TestKey { },
        Value = new TestValue { Name = "value 3" }
      }
    );

    var firstOffsets = this._consumer.QueryWatermarkOffsets(new TopicPartition(TOPIC_NAME, new Partition(0)), TimeSpan.FromSeconds(5));
    var secondOffsets = this._consumer.QueryWatermarkOffsets(new TopicPartition(TOPIC_NAME, new Partition(1)), TimeSpan.FromSeconds(5));

    Assert.Equal(1, firstOffsets.High - firstOffsets.Low);
    Assert.Equal(1, secondOffsets.High - secondOffsets.Low);

    await this._sut.InsertFixtures(
      SAFE_TOPIC_NAME,
      [
        expectedMessages[1],
        expectedMessages[2],
      ]
    );

    safeFirstOffsets = this._consumer.QueryWatermarkOffsets(new TopicPartition(SAFE_TOPIC_NAME, new Partition(0)), TimeSpan.FromSeconds(5));

    Assert.Equal(3, safeFirstOffsets.High - safeFirstOffsets.Low);

    this._realConsumer.Subscribe(SAFE_TOPIC_NAME);

    List<Message<TestKey, TestValue>> records = new List<Message<TestKey, TestValue>> { };
    while (records.Count < 3)
    {
      var cr = _realConsumer.Consume(TimeSpan.FromSeconds(1));
      if (cr != null)
      {
        var msg = cr.Message;
        msg.Timestamp = Timestamp.Default;
        msg.Headers = null;
        records.Add(msg);
      }
    }

    Assert.Equal(
      JsonConvert.SerializeObject(expectedMessages),
      JsonConvert.SerializeObject(records)
    );
  }
}

public class TestKey
{
  [JsonPropertyName("id")]
  [JsonProperty("id")]
  public string? Id { get; set; }
}

public class TestValue
{
  [JsonPropertyName("name")]
  [JsonProperty("name")]
  public required string Name { get; set; }
}

public class NewtonsoftJsonSerializer<T> : ISerializer<T>
{
  public byte[] Serialize(T data, SerializationContext context)
  {
    if (data == null) { return Array.Empty<byte>(); }
    var json = JsonConvert.SerializeObject(data);
    return Encoding.UTF8.GetBytes(json);
  }
}

public class NewtonsoftJsonDeserializer<T> : IDeserializer<T>
{
  public T Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
  {
    if (isNull || data.Length == 0) return default!;
    var json = Encoding.UTF8.GetString(data);
    return JsonConvert.DeserializeObject<T>(json)!;
  }
}