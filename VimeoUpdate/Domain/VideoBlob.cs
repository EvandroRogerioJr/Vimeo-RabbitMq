using System.ComponentModel.DataAnnotations;

namespace VimeoUpdate.Domain
{
    public class VideoBlob
    {
        [Key]
        public long IdCliente { get; set; }

        public string? LinkBlob { get; set; }

        public string? NomeVideo {  get; set; }

        public int QuantidadeErro { get; set; }
    }
}
