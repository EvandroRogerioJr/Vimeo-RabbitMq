using Microsoft.AspNetCore.Mvc;
using Azure;
using tusdotnet;
using Newtonsoft.Json;
using RabbitMQ.Client;
using Azure.Storage.Blobs;
using System.Text;
using VimeoUpdate.Interface;

namespace VimeoUpdate.Domain
{
    [ApiController]
    [Route("[controller]")]
    public class VideoController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly string _blobConnection;
        private readonly string QUEUE_NAME = "vimeo";
        private readonly IRabbitMQMessageRepository _rabbitMQ;


        public VideoController(IConfiguration configuration, IRabbitMQMessageRepository rabbitMQ)
        {
            _configuration = configuration;
            _blobConnection = _configuration.GetConnectionString("BlobStorageConnection")!;
            _rabbitMQ = rabbitMQ;
        }

        [HttpPost]
        [DisableRequestSizeLimit,
        RequestFormLimits(MultipartBodyLengthLimit = int.MaxValue,
        ValueLengthLimit = int.MaxValue)]
        public async Task<IActionResult> UploadVideo(IFormFile arquivo)
        {
            try
            {
                if (arquivo == null || arquivo.Length == 0)
                {
                    return BadRequest("Arquivo inválido");
                }

                var blobServiceClient = new BlobServiceClient(_blobConnection);
                var blobContainerClient = blobServiceClient.GetBlobContainerClient("videos");
                await blobContainerClient.CreateIfNotExistsAsync();

                var blobName = Guid.NewGuid().ToString() + Path.GetExtension(arquivo.FileName);
                var blobClient = blobContainerClient.GetBlobClient(blobName);

                using (var stream = arquivo.OpenReadStream())
                {
                    await blobClient.UploadAsync(stream, true);
                }

                var blobUrl = blobClient.Name;

                VideoBlob videoBlob = new VideoBlob
                {
                    IdCliente = 1,
                    LinkBlob = blobUrl,
                    NomeVideo = arquivo.FileName,
                    QuantidadeErro = 0
                };

                await _rabbitMQ.PublishMessage<VideoBlob>(QUEUE_NAME, videoBlob, false);

                return Ok("Upload bem-sucedido");
            }
            catch (RequestFailedException ex)
            {
                return StatusCode(500, $"Erro ao realizar upload: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Erro interno do servidor");
            }
        }
        private void UploadVimeo(VideoFile file)
        {

        }
    }
}
