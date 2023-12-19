using System.Text;
using System;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using RestSharp;
using Microsoft.Extensions.Options;
using VimeoUpdate.Domain;
using Newtonsoft.Json;
using System.Diagnostics.Tracing;

public class VimeoQueueConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ConnectionFactory _factory;
    private readonly string _blobConnection;
    private readonly string containerName = "videos";
    private const string VimeoApiBaseUrl = "https://api.vimeo.com/";
    private const string AccessToken = "AcessTokenVimeo";
    private readonly string QUEUE_NAME = "vimeo"; // Nome da sua fila

    public VimeoQueueConsumer(IServiceProvider serviceProvider, IConfiguration configuration, IOptions<RabbitMQConfiguration> rabbitMQConfig)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _blobConnection = configuration.GetConnectionString("BlobStorageConnection")!;
        _factory = new ConnectionFactory
        {
            HostName = rabbitMQConfig.Value.HostName
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };

        using (var scope = _serviceProvider.CreateScope())
        {
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(
                    queue: QUEUE_NAME,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null
                );

                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += async (sender, eventArgs) =>
                {
                    var contentArray = eventArgs.Body.ToArray();
                    var contentString = Encoding.UTF8.GetString(contentArray);

                    var videoBlob = JsonConvert.DeserializeObject<VideoBlob>(contentString);
                    byte[] videoBytes = await DownloadBlobToFile(videoBlob!.LinkBlob!);

                    string videoLinkUpload = await UploadVideo(videoBytes.Length);

                    await UploadVideoToVimeo(videoLinkUpload, videoBytes);

                    channel.BasicAck(eventArgs.DeliveryTag, false);

                };

                channel.BasicConsume(
                    queue: QUEUE_NAME,
                    autoAck: false,
                    consumer: consumer
                );

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
        }
    }

    private async Task<byte[]> DownloadBlobToFile(string blobUrl)
    {
        try
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient(_blobConnection);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobUrl);

            BlobDownloadInfo blobDownloadInfo = await blobClient.DownloadAsync();

            using (var ms = new MemoryStream())
            {
                await blobDownloadInfo.Content.CopyToAsync(ms);
                byte[] downloadedBytes = ms.ToArray();

                if (downloadedBytes.Length > 0)
                {
                    Console.WriteLine("Download do blob concluído com sucesso!");
                    return downloadedBytes;
                }
                else
                {
                    Console.WriteLine("O download do blob não retornou dados.");
                    return null;
                }
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao realizar download do blob: {ex.Message}");
            return null;
        }
    }



    public async Task<string> UploadVideo(long size)
    {
        var vimeoUploadRequest = new
        {
            upload = new
            {
                approach = "tus",
                size = size.ToString()
            }
        };

        var client = new RestClient();

        var request = new RestRequest(VimeoApiBaseUrl + "me/videos", Method.Post);

        // Adicione o cabeçalho de autorização
        request.AddHeader("Authorization", $"Bearer {AccessToken}");

        // Adicione o corpo da requisição (JSON)
        request.AddJsonBody(vimeoUploadRequest);

        var response = await client.ExecuteAsync(request);

        if (response.IsSuccessful)
        {
            var responseData = response.Content;
            var vimeoUploadResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(responseData);

            return vimeoUploadResponse!.upload.upload_link;
        }
        else
        {
            Console.WriteLine($"Erro ao fazer upload. Status Code: {response.StatusCode}");
            return null; // ou lance uma exceção, dependendo do seu tratamento de erro desejado
        }
    }

    private async Task UploadVideoToVimeo(string uploadLink, byte[] videoBytes)
    {
        try
        {
            int chunkSize = 128 * 1024 * 1024;
            int offset = 0;

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Tus-Resumable", "1.0.0");
                httpClient.DefaultRequestHeaders.Add("Upload-Offset", "0");

                while (offset < videoBytes.Length)
                {
                    int remainingBytes = videoBytes.Length - offset;
                    int currentChunkSize = Math.Min(chunkSize, remainingBytes);

                    byte[] chunk = new byte[currentChunkSize];
                    Array.Copy(videoBytes, offset, chunk, 0, currentChunkSize);

                    using (var content = new ByteArrayContent(chunk))
                    {
                        content.Headers.Add("Content-Type", "application/offset+octet-stream");

                        var response = await httpClient.PatchAsync(uploadLink, content);

                        if (!response.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"Erro ao fazer upload. Status Code: {response.StatusCode}");
                        }

                        offset += currentChunkSize;

                        httpClient.DefaultRequestHeaders.Remove("Upload-Offset");
                        httpClient.DefaultRequestHeaders.Add("Upload-Offset", offset.ToString());
                    }
                }
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro durante o upload: {ex.Message}");
        }
    }
}
