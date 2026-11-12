namespace DmProviderFeatureProbe;

internal abstract class Animal
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

internal sealed class Dog : Animal
{
    public int BarkVolume { get; set; }
}

internal sealed class Cat : Animal
{
    public int Lives { get; set; }
}

internal abstract class Vehicle
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

internal sealed class Car : Vehicle
{
    public int Doors { get; set; }
}

internal sealed class Truck : Vehicle
{
    public decimal Payload { get; set; }
}

/// <summary>
/// A key per concrete type has no server generator until the model names one, so the probe supplies
/// its own Guid and does not depend on one.
/// </summary>
internal abstract class Payment
{
    public Guid Id { get; set; }

    public decimal Amount { get; set; }
}

internal sealed class CardPayment : Payment
{
    public string Last4 { get; set; } = string.Empty;
}

internal sealed class CashPayment : Payment
{
    public string Currency { get; set; } = string.Empty;
}
