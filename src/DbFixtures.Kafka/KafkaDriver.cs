using Confluent.Kafka;
using SharedLibs.Types;

namespace DbFixtures.Kafka;

public class KafkaDriver<K, V> : IDriver
{
  private readonly IAdminClient _adminClient;
  private readonly IConsumer<Ignore, Ignore> _consumer;
  private readonly IProducer<K, V> _producer;

  public KafkaDriver(
    IAdminClient adminClient, IConsumer<Ignore, Ignore> consumer,
    IProducer<K, V> producer
  )
  {
    this._adminClient = adminClient;
    this._consumer = consumer;
    this._producer = producer;
  }

  public Task Close()
  {
    this._consumer.Close();
    this._consumer.Dispose();
    this._producer.Flush(TimeSpan.FromSeconds(5));
    this._producer.Dispose();
    this._adminClient.Dispose();

    return Task.CompletedTask;
  }

  public async Task InsertFixtures(string tableName, Message<K, V>[] fixtures)
  {
    foreach (var fixture in fixtures)
    {
      await this._producer.ProduceAsync(tableName, fixture);
    }
  }

  async Task IDriver.InsertFixtures<T>(string tableName, T[] fixtures)
  {
    if (fixtures is not Message<K, V>[] typedFixtures)
    {
      throw new ArgumentException($"Expected array of {typeof(Message<K, V>)} for the 'fixtures' argument");
    }

    await InsertFixtures(tableName, typedFixtures);
  }

  public async Task Truncate(string[] tableNames)
  {
    foreach (var tableName in tableNames)
    {
      var metadata = this._adminClient.GetMetadata(tableName, TimeSpan.FromSeconds(10));
      var partitions = metadata.Topics[0].Partitions;

      var topicOffsets = new List<TopicPartitionOffset>();

      foreach (var partition in partitions)
      {
        var partitionInstance = new Partition(partition.PartitionId);
        var offsets = this._consumer.QueryWatermarkOffsets(
          new TopicPartition(tableName, partitionInstance),
          TimeSpan.FromSeconds(10)
        );

        if (offsets.High > offsets.Low)
        {
          topicOffsets.Add(new TopicPartitionOffset(
            new TopicPartition(tableName, partitionInstance),
            offsets.High
          ));
        }
      }

      if (topicOffsets.Count == 0)
      {
        continue;
      }

      await this._adminClient.DeleteRecordsAsync(topicOffsets, null);
    }
  }
}