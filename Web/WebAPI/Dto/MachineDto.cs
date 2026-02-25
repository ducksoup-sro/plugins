using System.Text.Json.Serialization;

namespace WebApiPlugin.Dto;

public class MachineDto
{
    [JsonPropertyName("machineId")]
    public int MachineId { get; set; }

    [JsonPropertyName("address")]
    public string Address { get; set; } = "";

    [JsonPropertyName("notice")]
    public string? Notice { get; set; }
}

public class AddMachineRequest
{
    [JsonPropertyName("address")]
    public string Address { get; set; } = "";

    [JsonPropertyName("notice")]
    public string? Notice { get; set; }
}

public class UpdateMachineRequest
{
    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("notice")]
    public string? Notice { get; set; }
}
