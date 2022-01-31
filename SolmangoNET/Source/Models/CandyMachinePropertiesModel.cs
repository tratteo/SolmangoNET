using Newtonsoft.Json;
using System;

namespace SolmangoNET.Models;

[Serializable]
public class CandyMachinePropertiesModel
{
    [JsonProperty("uuid")]
    public string Uuid { get; init; }

    [JsonProperty("candyMachine")]
    public string Address { get; init; }

    public CandyMachinePropertiesModel()
    {
        Uuid = string.Empty;
        Address = string.Empty;
    }
}