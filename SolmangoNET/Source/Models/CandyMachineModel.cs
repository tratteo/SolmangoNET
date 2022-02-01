using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SolmangoNET.Models;

[Serializable]
public class CandyMachineModel
{
    [JsonPropertyName("program")]
    [JsonProperty("program")]
    public CandyMachinePropertiesModel CandyMachineProgram { get; init; }

    [JsonPropertyName("items")]
    [JsonProperty("items")]
    public Dictionary<int, CandyMachineItemModel> Items { get; init; }

    public CandyMachineModel()
    {
        CandyMachineProgram = new CandyMachinePropertiesModel();
        Items = new Dictionary<int, CandyMachineItemModel>();
    }
}