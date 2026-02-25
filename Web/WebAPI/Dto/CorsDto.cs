using Newtonsoft.Json;

namespace WebAPI.Dto;

public class CorsOriginRequest
{
    [JsonProperty("origin")]
    public string? Origin { get; set; }
}

public class CorsOriginsResponse
{
    [JsonProperty("default")]
    public List<string> Default { get; set; } = new();

    [JsonProperty("plugin")]
    public List<string> Plugin { get; set; } = new();
}
