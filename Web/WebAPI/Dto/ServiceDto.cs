using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace WebApiPlugin.Dto;

public class ServiceDto
{
    [JsonPropertyName("serviceId")]
    public int ServiceId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("serverType")]
    public string ServerType { get; set; } = "";

    [JsonPropertyName("securityType")]
    public string SecurityType { get; set; } = "";

    [JsonPropertyName("remotePort")]
    public int RemotePort { get; set; }

    [JsonPropertyName("bindPort")]
    public int BindPort { get; set; }

    [JsonPropertyName("autoStart")]
    public bool AutoStart { get; set; }

    [JsonPropertyName("started")]
    public bool Started { get; set; }

    [JsonPropertyName("localMachineId")]
    public int LocalMachineId { get; set; }

    [JsonPropertyName("remoteMachineId")]
    public int RemoteMachineId { get; set; }

    [JsonPropertyName("localAddress")]
    public string? LocalAddress { get; set; }

    [JsonPropertyName("remoteAddress")]
    public string? RemoteAddress { get; set; }

    [JsonPropertyName("spoofMachineId")]
    public int? SpoofMachineId { get; set; }

    [JsonPropertyName("spoofAddress")]
    public string? SpoofAddress { get; set; }
}

public class AddServiceRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("serverType")]
    public int ServerType { get; set; } // API.ServerType enum value

    [JsonPropertyName("securityType")]
    public int SecurityType { get; set; } // SilkroadSecurityAPI.SecurityType

    [JsonPropertyName("remotePort")]
    public int RemotePort { get; set; }

    [JsonPropertyName("bindPort")]
    public int BindPort { get; set; }

    [JsonPropertyName("byteLimitation")]
    public int ByteLimitation { get; set; }

    [JsonPropertyName("autoStart")]
    public bool AutoStart { get; set; }

    [JsonPropertyName("localMachineId")]
    public int LocalMachineId { get; set; }

    [JsonPropertyName("remoteMachineId")]
    public int RemoteMachineId { get; set; }

    [JsonPropertyName("spoofMachineId")]
    public int? SpoofMachineId { get; set; }
}

public class UpdateServiceRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("remotePort")]
    public int? RemotePort { get; set; }

    [JsonPropertyName("bindPort")]
    public int? BindPort { get; set; }

    [JsonPropertyName("byteLimitation")]
    public int? ByteLimitation { get; set; }

    [JsonPropertyName("autoStart")]
    public bool? AutoStart { get; set; }

    [JsonPropertyName("localMachineId")]
    public int? LocalMachineId { get; set; }

    [JsonPropertyName("remoteMachineId")]
    public int? RemoteMachineId { get; set; }

    [JsonPropertyName("spoofMachineId")]
    public int? SpoofMachineId { get; set; }

    /// <summary>When true, set SpoofMachine to null. Use this to clear spoof (spoof is nullable, do not use 0).</summary>
    [JsonProperty("clearSpoof")]
    [JsonPropertyName("clearSpoof")]
    public bool ClearSpoof { get; set; }

    [JsonPropertyName("restart")]
    public bool Restart { get; set; }
}
