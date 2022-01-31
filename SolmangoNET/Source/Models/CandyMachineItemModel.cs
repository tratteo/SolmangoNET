using Newtonsoft.Json;
using System;

namespace SolmangoNET.Models;

[Serializable]
public class CandyMachineItemModel
{
    [JsonProperty("link")]
    public string Link { get; init; }

    [JsonProperty("name")]
    public string Name { get; init; }

    [JsonProperty("onChain")]
    public bool OnChain { get; init; }

    [JsonProperty("rarityOrder")]
    public int RarityOrder { get; set; }

    [JsonProperty("verifyRun")]
    public bool VerifyRun { get; init; }

    public CandyMachineItemModel()
    {
        Link = string.Empty;
        Name = string.Empty;
    }
}