using Newtonsoft.Json;
using System;
using System.Text.Json.Serialization;

namespace SolmangoNET.Models;

[Serializable]
public class CandyMachineItemModel
{
    [JsonPropertyName("link")]
    [JsonProperty("link")]
    public string Link { get; init; }

    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string Name { get; init; }

    [JsonPropertyName("onChain")]
    [JsonProperty("onChain")]
    public bool OnChain { get; init; }

    [JsonPropertyName("verifyRun")]
    [JsonProperty("verifyRun")]
    public bool VerifyRun { get; init; }

    public CandyMachineItemModel()
    {
        Link = string.Empty;
        Name = string.Empty;
    }
}