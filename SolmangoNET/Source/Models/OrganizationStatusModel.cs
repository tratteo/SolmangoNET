using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SolmangoNET.Models;

[Serializable]
public class OrganizationStatusModel
{
    [JsonPropertyName("collection")]
    [JsonProperty("collection")]
    public CollectionModel Collection { get; init; }

    [JsonPropertyName("balance")]
    [JsonProperty("balance")]
    public ulong Balance { get; init; }

    [JsonPropertyName("votes")]
    [JsonProperty("votes")]
    public Dictionary<string, float> Votes { get; init; }

    public OrganizationStatusModel()
    {
        Collection = new CollectionModel();
        Votes = new Dictionary<string, float>();
    }
}