using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using VimeoUpdate.Domain;
using VimeoUpdate.Extensions;
using VimeoUpdate.Interface;

namespace VimeoUpdate.Repository;

public class RabbitMQMessageRepository : IRabbitMQMessageRepository
{
    private readonly RabbitMQClient _rabbitMQ;

    public RabbitMQMessageRepository(IOptions<RabbitMQConfiguration> rabbitMQConfig)
        => _rabbitMQ = new RabbitMQClient(rabbitMQConfig);


    public async Task PublishMessage<TOutPut>(string QUEUE_NAME, TOutPut message, bool diposed = true)
    {
        try
        {
            using (var _channel = _rabbitMQ.CreateChannel())
            {
                if (_channel is not null)
                {
                    _channel.QueueDeclare(
                        queue: QUEUE_NAME,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null
                    );

                    byte[] bytesMessage = await GetMessage(message);

                    _channel.BasicPublish(
                        exchange: "",
                        routingKey: QUEUE_NAME,
                        basicProperties: null,
                        body: bytesMessage
                    );
                }
            }
            if (diposed)
            {
                _rabbitMQ.Dispose();
            }
        }
        catch (Exception) { }
    }

    public async Task ConsumeMessage<TOutPut>(string QUEUE_NAME)
    {
        using (var _channel = _rabbitMQ.CreateChannel())
        {
            if (_channel is not null)
            {
                _channel.QueueDeclare(
                    queue: QUEUE_NAME,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null
                );

                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += async (sender, eventArgs) =>
                {
                    var contentArray = eventArgs.Body.ToArray();
                    var contentString = Encoding.UTF8.GetString(contentArray);
                    var message = await ResultMessage<TOutPut>(contentString);

                    _channel.BasicAck(eventArgs.DeliveryTag, false);
                };

                _channel.BasicConsume(
                    queue: QUEUE_NAME,
                    autoAck: false,
                    consumer: consumer
                );
            }
        }
        await Task.FromResult(true);

        _rabbitMQ.Dispose();
    }


    private async Task<byte[]> GetMessage<TOutPut>(TOutPut message)
    {
        var stringfiedMessage = JsonConvert.SerializeObject(message);
        var bytesMessage = Encoding.UTF8.GetBytes(stringfiedMessage);
        return await Task.FromResult(bytesMessage);
    }

    private async Task<TOutPut> ResultMessage<TOutPut>(string contentString)
    {
        var message = JsonConvert.DeserializeObject<TOutPut>(contentString);
        return await Task.FromResult(message!);
    }
}
