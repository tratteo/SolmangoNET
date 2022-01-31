using System;

namespace SolmangoNET.Models;

[Serializable]
public class CollectionModel
{
    public string Name { get; init; }

    public string Description { get; init; }

    public string Symbol { get; init; }

    public int Supply { get; init; }

    public CollectionModel()
    {
        Description = string.Empty;
        Name = string.Empty;
        Symbol = string.Empty;
        Supply = 0;
    }
}