using Newtonsoft.Json;
using System;
using System.Text.Json.Serialization;

namespace SolmangoNET.Models;

[Serializable]
public class MintInfoModel
{
    [JsonPropertyName("rarity")]
    [JsonProperty("rarity")]
    public RarityModel? Rarity { get; init; }

    [JsonPropertyName("metadata")]
    [JsonProperty("metadata")]
    public TokenMetadataModel? Metadata { get; init; }

    public MintInfoModel()
    {
    }
}