using Newtonsoft.Json;
using System;
using System.Text.Json.Serialization;

namespace SolmangoNET.Models;

[Serializable]
public class MemberModel : IEquatable<MemberModel>
{
    [JsonPropertyName("address")]
    [JsonProperty("address")]
    public string Address { get; private set; }

    [JsonPropertyName("whitelisted")]
    [JsonProperty("whitelisted")]
    public bool Whitelisted { get; set; }

    [JsonPropertyName("vote")]
    [JsonProperty("vote")]
    public string Vote { get; set; }

    [JsonPropertyName("last_vote_power")]
    [JsonProperty("last_vote_power")]
    public int LastVotePower { get; set; }

    [JsonPropertyName("promised")]
    [JsonProperty("promised")]
    public int Promised { get; set; }

    public MemberModel(string address)
    {
        Address = address;
        Whitelisted = false;
        Vote = string.Empty;
        LastVotePower = 0;
        Promised = 0;
    }

    public bool Equals(MemberModel? other) => other is not null && other.Address == Address;
}