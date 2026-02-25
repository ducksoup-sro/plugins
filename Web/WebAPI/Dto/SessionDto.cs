using System.Text.Json.Serialization;

namespace WebAPI.Dto;

public class SessionDto
{
    [JsonPropertyName("guid")]
    public string Guid { get; set; } = "";

    [JsonPropertyName("serverType")]
    public string ServerType { get; set; } = "";

    [JsonPropertyName("remoteEndPoint")]
    public string? RemoteEndPoint { get; set; }
}

public class SessionDetailDto : SessionDto
{
    /// <summary>True when the session is still in the active sessions list.</summary>
    [JsonPropertyName("isConnected")]
    public bool IsConnected { get; set; } = true;

    [JsonPropertyName("characterGameReady")]
    public bool CharacterGameReady { get; set; }

    [JsonPropertyName("charName")]
    public string? CharName { get; set; }

    [JsonPropertyName("charLevel")]
    public byte? CharLevel { get; set; }

    [JsonPropertyName("jobName")]
    public string? JobName { get; set; }

    [JsonPropertyName("regionId")]
    public ushort? RegionId { get; set; }
}

public class PacketLogEntryDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "";

    [JsonPropertyName("msgId")]
    public ushort MsgId { get; set; }

    [JsonPropertyName("encrypted")]
    public bool Encrypted { get; set; }

    [JsonPropertyName("massive")]
    public bool Massive { get; set; }

    [JsonPropertyName("hex")]
    public string Hex { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";
}

public class SessionDataDto
{
    [JsonPropertyName("characterGameReady")]
    public bool CharacterGameReady { get; set; }

    [JsonPropertyName("charId")]
    public int CharId { get; set; }

    [JsonPropertyName("charNameSent")]
    public bool CharNameSent { get; set; }

    [JsonPropertyName("charInfo")]
    public SessionCharInfoDto? CharInfo { get; set; }

    [JsonPropertyName("data")]
    public List<SessionDataEntryDto> Data { get; set; } = new();
}

public class SessionDataEntryDto
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}

public class SessionCharInfoDto
{
    [JsonPropertyName("charName")]
    public string? CharName { get; set; }

    [JsonPropertyName("curLevel")]
    public byte CurLevel { get; set; }

    [JsonPropertyName("jobName")]
    public string? JobName { get; set; }

    [JsonPropertyName("jobLevel")]
    public byte JobLevel { get; set; }

    [JsonPropertyName("uniqueCharId")]
    public uint UniqueCharId { get; set; }

    [JsonPropertyName("jid")]
    public uint Jid { get; set; }

    [JsonPropertyName("hp")]
    public uint Hp { get; set; }

    [JsonPropertyName("mp")]
    public uint Mp { get; set; }

    [JsonPropertyName("regionId")]
    public ushort RegionId { get; set; }
}

public class SendPacketRequest
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = ""; // Session Guid

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "Client"; // "Client" = SendToClient, "Module" = SendToServer

    [JsonPropertyName("msgId")]
    public ushort MsgId { get; set; }

    [JsonPropertyName("encrypted")]
    public bool Encrypted { get; set; }

    [JsonPropertyName("massive")]
    public bool Massive { get; set; }

    [JsonPropertyName("data")]
    public string? Data { get; set; } // Format: "type:;:value;:;..." or omit when using rawHex

    [JsonPropertyName("rawHex")]
    public string? RawHex { get; set; } // Optional: send raw hex bytes (e.g. "A1B2C3")
}

/// <summary>Same as SendPacketRequest but without clientId; optional serverType for broadcast target.</summary>
public class BroadcastPacketRequest
{
    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "Client";

    [JsonPropertyName("msgId")]
    public ushort MsgId { get; set; }

    [JsonPropertyName("encrypted")]
    public bool Encrypted { get; set; }

    [JsonPropertyName("massive")]
    public bool Massive { get; set; }

    [JsonPropertyName("data")]
    public string? Data { get; set; }

    [JsonPropertyName("rawHex")]
    public string? RawHex { get; set; }

    [JsonPropertyName("serverType")]
    public string? ServerType { get; set; } // "AgentServer", "GatewayServer", "DownloadServer"
}
