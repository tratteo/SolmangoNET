// Copyright Matteo Beltrame

using Newtonsoft.Json;
using System;
using System.Text.Json.Serialization;

namespace SolmangoNET.Models;

[Serializable]
public class RarityModel
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public int Id { get; init; }

    [JsonPropertyName("rarity_score")]
    [JsonProperty("rarity_score")]
    public double RarityScore { get; set; }

    [JsonPropertyName("rarity_order")]
    [JsonProperty("rarity_order")]
    public int RarityOrder { get; set; }

    [JsonPropertyName("top_percentage")]
    [JsonProperty("top_percentage")]
    public double Percentage { get; set; }
}