# Fixtures Manager Kafka Driver

An abstraction layer for the [Kafka package](https://www.nuget.org/packages/confluent.kafka) to facilitate handling database fixtures for testing purposes, in a Kafka instance.  
This package is ment to be used in conjunction with the [dbfixtures package](https://github.com/PedroHenriques/dotnet-dbfixtures/blob/main/src/DbFixtures/README.md), but can also be used by itself.

## Installation

```sh
dotnet add [path/to/your/csproj/file] package DbFixtures.Kafka
```

## Usage

This package exposes the following interface

```c#
public interface IDriver
{
  // clears the specified "tables" of any content
  public Task Truncate(string[] tableNames);

  // inserts the supplied "rows" into the specified "table"
  public Task InsertFixtures<T>(string tableName, T[] fixtures);

  // cleanup and terminate the connection to the database
  public Task Close();
}
```

### Example

```c#
using System.Text;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Newtonsoft.Json;
using SharedLibs.Types;

namespace DbFixtures.Kafka.Tests.E2E;

[Trait("Type", "E2E")]
public class KafkaDriverTests : IDisposable, IAsyncLifetime
{
  private const string TOPIC_NAME = "someTestTopic";
  private const string SAFE_TOPIC_NAME = "someSafeTopic";
  private readonly IAdminClient _adminClient;
  private readonly IConsumer<Ignore, Ignore> _consumer;
  private readonly IConsumer<TestKey, TestValue> _realConsumer;
  private readonly IProducer<TestKey, TestValue> _producer;
  private readonly IDriver _driver;

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


    this._driver = new KafkaDriver<TestKey, TestValue>(this._adminClient, this._consumer, this._producer);
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
        new TopicSpecification { Name = SAFE_TOPIC_NAME, NumPartitions = 2, ReplicationFactor = 1 },
        new TopicSpecification { Name = TOPIC_NAME, NumPartitions = 1, ReplicationFactor = 1 },
      });
    }
    catch (CreateTopicsException ex)
    {
      if (ex.Results.Any(r => r.Error.Code != ErrorCode.TopicAlreadyExists)) { throw; }
    }

    await this._driver.Truncate(new[] { TOPIC_NAME, SAFE_TOPIC_NAME });
  }

  [Fact]
  public async Task InsertFixtures_ItShouldInsertTheProvidedFixturesInTheSpecifiedTopics()
  {
    await this._producer.ProduceAsync(
      new TopicPartition(SAFE_TOPIC_NAME, new Partition(0)),
      new Message<TestKey, TestValue>
      {
        Key = new TestKey { Id = "safe key 1" },
        Value = new TestValue { Prop1 = "safe value 1", Prop2 = 48 }
      }
    );
    await this._producer.ProduceAsync(
      new TopicPartition(SAFE_TOPIC_NAME, new Partition(1)),
      new Message<TestKey, TestValue>
      {
        Key = new TestKey { Id = "safe key 2" },
        Value = new TestValue { Prop1 = "safe value 2" }
      }
    );

    var safeFirstOffsets = this._consumer.QueryWatermarkOffsets(new TopicPartition(SAFE_TOPIC_NAME, new Partition(0)), TimeSpan.FromSeconds(5));
    var safeSecondOffsets = this._consumer.QueryWatermarkOffsets(new TopicPartition(SAFE_TOPIC_NAME, new Partition(1)), TimeSpan.FromSeconds(5));

    Assert.Equal(1, safeFirstOffsets.High - safeFirstOffsets.Low);
    Assert.Equal(1, safeSecondOffsets.High - safeSecondOffsets.Low);

    await this._producer.ProduceAsync(
      new TopicPartition(TOPIC_NAME, new Partition(0)),
      new Message<TestKey, TestValue>
      {
        Key = new TestKey { Id = "key 1" },
        Value = new TestValue { Prop1 = "value 1", Prop2 = 100 }
      }
    );
    await this._producer.ProduceAsync(
      new TopicPartition(TOPIC_NAME, new Partition(0)),
      new Message<TestKey, TestValue>
      {
        Key = new TestKey { Id = "key 2" },
        Value = new TestValue { Prop1 = "value 2" }
      }
    );

    var firstOffsets = this._consumer.QueryWatermarkOffsets(new TopicPartition(TOPIC_NAME, new Partition(0)), TimeSpan.FromSeconds(5));

    Assert.Equal(2, firstOffsets.High - firstOffsets.Low);

    Message<TestKey, TestValue>[] expectedMessages = [
      new Message<TestKey, TestValue>
      {
        Key = new TestKey { Id = "key 3" },
        Value = new TestValue { Prop1 = "value 3", Prop2 = 123 }
      },
      new Message<TestKey, TestValue>
      {
        Key = new TestKey { Id = "key 4" },
        Value = new TestValue { Prop1 = "value 4", Prop2 = 987 }
      }
    ];

    var sut = new DbFixtures([this._driver]);
    await sut.InsertFixtures(
      [TOPIC_NAME, SAFE_TOPIC_NAME],
      new Dictionary<string, Message<TestKey, TestValue>[]>
      {
        { SAFE_TOPIC_NAME, [] },
        { TOPIC_NAME, expectedMessages },
      }
    );

    safeFirstOffsets = this._consumer.QueryWatermarkOffsets(new TopicPartition(SAFE_TOPIC_NAME, new Partition(0)), TimeSpan.FromSeconds(5));
    safeSecondOffsets = this._consumer.QueryWatermarkOffsets(new TopicPartition(SAFE_TOPIC_NAME, new Partition(1)), TimeSpan.FromSeconds(5));

    Assert.Equal(0, safeFirstOffsets.High - safeFirstOffsets.Low);
    Assert.Equal(0, safeSecondOffsets.High - safeSecondOffsets.Low);

    firstOffsets = this._consumer.QueryWatermarkOffsets(new TopicPartition(TOPIC_NAME, new Partition(0)), TimeSpan.FromSeconds(5));

    Assert.Equal(2, firstOffsets.High - firstOffsets.Low);

    this._realConsumer.Subscribe(TOPIC_NAME);

    List<Message<TestKey, TestValue>> records = new List<Message<TestKey, TestValue>> { };
    while (records.Count < 2)
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
  [JsonPropertyName("prop1")]
  [JsonProperty("prop1")]
  public required string Prop1 { get; set; }

  [JsonPropertyName("prop2")]
  [JsonProperty("prop2")]
  public int Prop2 { get; set; }
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
```