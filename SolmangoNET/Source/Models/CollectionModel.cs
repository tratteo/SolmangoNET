using Newtonsoft.Json;
using System;
using System.Text.Json.Serialization;

namespace SolmangoNET.Models;

[Serializable]
public class CollectionModel
{
    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string Name { get; init; }

    [JsonPropertyName("description")]
    [JsonProperty("description")]
    public string Description { get; init; }

    [JsonPropertyName("symbol")]
    [JsonProperty("symbol")]
    public string Symbol { get; init; }

    [JsonPropertyName("supply")]
    [JsonProperty("supply")]
    public int Supply { get; init; }

    public CollectionModel()
    {
        Description = string.Empty;
        Name = string.Empty;
        Symbol = string.Empty;
        Supply = 0;
    }
}