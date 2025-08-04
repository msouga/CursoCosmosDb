using Newtonsoft.Json;

namespace HighPerfLogger.App;

public class LogEntry
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string tenantId { get; set; } // Partition Key
    public DateTime timestamp { get; set; } = DateTime.UtcNow;
    public string correlationId { get; set; } = string.Empty;
    public string service { get; set; } = string.Empty;
    public string user { get; set; } = string.Empty;
    public string level { get; set; } = string.Empty;
    public string message { get; set; } = string.Empty;
    public object? payload { get; set; }
}
