namespace DmProviderFeatureProbe;

internal sealed class Customer
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Owned type in the owner's table.</summary>
    public Address Address { get; set; } = new();

    /// <summary>Complex type, one column per member, no identity of its own.</summary>
    public Money Balance { get; set; }

    /// <summary>Stored as its integer value.</summary>
    public Tier Tier { get; set; }

    /// <summary>Stored as text by a converter, the same enum type.</summary>
    public Tier TierText { get; set; }
}

internal sealed class Address
{
    public string Street { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;
}

internal struct Money
{
    public decimal Amount { get; set; }

    public string Currency { get; set; }
}

internal enum Tier
{
    Bronze,
    Silver,
    Gold,
}

internal sealed class Shipping
{
    public string Street { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;
}

/// <summary>A primitive collection, mapped as JSON by convention on EF Core 8 and later.</summary>
internal sealed class Tagged
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];
}
