using Godot;

namespace DeliveryTemperatureWar;

/// <summary>
/// Shared layout numbers so the street, events, and ETA stay aligned.
/// The rider starts facing -Z and the drop-off is toward negative Z.
/// </summary>
public static class Track
{
    public const float LaneX = 1.65f;
    public const float StartZ = 8f;
    public const float RedZ = -18f;
    public const float IntersectionZ = -34f;
    public const float DogZ = -58f;
    public const float RainZ = -74f;
    public const float BrakeZ = -88f;
    public const float DeliveryX = 1.65f;
    public const float DeliveryZ = -108f;
    public const float ShopX = 12.2f;
    public const float ShopZ = 8f;
    public const int Ticket = 17;
    public static readonly Vector3 Lobby = new(58f, 0f, -36f);

    public const float TargetCelsius = 60f;
    public const float SpillFail = 0.5f;
    public const float StartCelsius = 86f;
}

public static class Layers
{
    public const uint World = 1;
    public const uint Bike = 2;
    public const uint Cargo = 4;
    public const uint Traffic = 8;
    public const uint Bag = 16;
    public const uint Walker = 32;
}
