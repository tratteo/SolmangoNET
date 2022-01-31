using System;

namespace SolmangoNET.Models;

[Serializable]
public class MemberModel : IEquatable<MemberModel>
{
    public string Address { get; private set; }

    public bool Whitelisted { get; set; }

    public string Vote { get; set; }

    public int LastVotePower { get; set; }

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