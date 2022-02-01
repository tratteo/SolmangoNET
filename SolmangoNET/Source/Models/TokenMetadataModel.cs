// Copyright Matteo Beltrame

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SolmangoNET.Models;

[Serializable]
public class TokenMetadataModel
{
    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonPropertyName("symbol")]
    [JsonProperty("symbol")]
    public string Symbol { get; set; }

    [JsonPropertyName("description")]
    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonPropertyName("seller_fee_basis_points")]
    [JsonProperty("seller_fee_basis_points")]
    public int SellerFeeBasisPoints { get; set; }

    [JsonPropertyName("image")]
    [JsonProperty("image")]
    public string Image { get; set; }

    [JsonPropertyName("attributes")]
    [JsonProperty("attributes")]
    public List<AttributeMetadata> Attributes { get; set; }

    [JsonPropertyName("properties")]
    [JsonProperty("properties")]
    public PropertiesMetadata Properties { get; set; }

    [JsonPropertyName("collection")]
    [JsonProperty("collection")]
    public CollectionMetadata Collection { get; set; }

    public TokenMetadataModel()
    {
        Name = string.Empty;
        Symbol = string.Empty;
        Description = string.Empty;
        Image = string.Empty;
        Attributes = new List<AttributeMetadata>();
        Properties = new PropertiesMetadata();
        Collection = new CollectionMetadata();
    }

    [Serializable]
    public class PropertiesMetadata
    {
        [JsonPropertyName("files")]
        [JsonProperty("files")]
        public List<FileMetadata> Files { get; set; }

        [JsonPropertyName("creators")]
        [JsonProperty("creators")]
        public List<CreatorMetadata> Creators { get; set; }

        public PropertiesMetadata()
        {
            Files = new List<FileMetadata>();
            Creators = new List<CreatorMetadata>();
        }

        [Serializable]
        public class FileMetadata
        {
            [JsonPropertyName("uri")]
            [JsonProperty("uri")]
            public string Uri { get; set; }

            [JsonPropertyName("type")]
            [JsonProperty("type")]
            public string Type { get; set; }

            public FileMetadata()
            {
                Uri = string.Empty;
                Type = string.Empty;
            }
        }

        [Serializable]
        public class CreatorMetadata
        {
            [JsonPropertyName("address")]
            [JsonProperty("address")]
            public string Address { get; set; }

            [JsonPropertyName("share")]
            [JsonProperty("share")]
            public int Share { get; set; }

            public CreatorMetadata()
            {
                Address = string.Empty;
            }
        }
    }

    [Serializable]
    public class CollectionMetadata
    {
        [JsonPropertyName("name")]
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonPropertyName("family")]
        [JsonProperty("family")]
        public string Family { get; set; }

        public CollectionMetadata()
        {
            Name = string.Empty;
            Family = string.Empty;
        }
    }

    [Serializable]
    public class AttributeMetadata : IEquatable<AttributeMetadata>
    {
        [JsonPropertyName("trait_type")]
        [JsonProperty("trait_type")]
        public string Trait { get; init; }

        [JsonPropertyName("value")]
        [JsonProperty("value")]
        public string Value { get; init; }

        [JsonPropertyName("rarity")]
        [JsonProperty("rarity")]
        public float Rarity { get; set; }

        public AttributeMetadata()
        {
            Trait = string.Empty;
            Value = string.Empty;
        }

        public bool Equals(AttributeMetadata? other) => other is not null && Trait.Equals(other.Trait);
    }
}