namespace VimeoUpdate.Domain;

public class RabbitMQConfiguration
{
    public const string ConfigurationSection = "RabbitMQ";
    public string? HostName { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? Exchange { get; set; }
    public int Port { get; set; }
}
