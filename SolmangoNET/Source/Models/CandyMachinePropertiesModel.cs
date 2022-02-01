using Newtonsoft.Json;
using System;
using System.Text.Json.Serialization;

namespace SolmangoNET.Models;

[Serializable]
public class CandyMachinePropertiesModel
{
    [JsonPropertyName("uuid")]
    [JsonProperty("uuid")]
    public string Uuid { get; init; }

    [JsonPropertyName("candyMachine")]
    [JsonProperty("candyMachine")]
    public string Address { get; init; }

    public CandyMachinePropertiesModel()
    {
        Uuid = string.Empty;
        Address = string.Empty;
    }
}