using Godot;

namespace DeliveryTemperatureWar;

public partial class TemperatureSim : Node
{
    [Export] public float BaseCooling = 0.28f;
    [Export] public float SpeedCooling = 0.045f;
    [Export] public float LidCooling = 1.45f;
    [Export] public float SpillCooling = 1.1f;
    [Export] public float CrushCooling = 0.9f;

    private BikeController? _bike;
    private CargoRig? _cargo;
    private EventDirector? _events;
    private DeliveryGame? _game;

    public float Celsius { get; private set; } = Track.StartCelsius;
    public bool Paused { get; set; }

    public override void _EnterTree()
    {
        AddToGroup("temp");
    }

    public override void _Process(double delta)
    {
        if (Paused)
        {
            return;
        }

        _game ??= GetTree().GetFirstNodeInGroup("game") as DeliveryGame;
        if (_game != null && _game.Phase == DeliveryGame.RunPhase.Done)
        {
            return;
        }

        _bike ??= GetTree().GetFirstNodeInGroup("bike") as BikeController;
        _cargo ??= GetTree().GetFirstNodeInGroup("cargo") as CargoRig;
        _events ??= GetTree().GetFirstNodeInGroup("events") as EventDirector;
        if (_bike == null || _cargo == null)
        {
            return;
        }

        float dt = (float)delta;
        float wind = Mathf.Max(0f, _bike.ForwardSpeed) * SpeedCooling;
        if (_events != null)
        {
            wind += _events.WindBonus;
        }

        float extra = _events?.ExtraCooling ?? 0f;
        float loss = BaseCooling
            + wind
            + LidCooling * _cargo.LidOpen01
            + SpillCooling * _cargo.Spill01
            + CrushCooling * _cargo.Crush01
            + extra;
        Celsius = Mathf.Max(0f, Celsius - loss * dt);
        _cargo.SetHeatVisual(Celsius);
    }
}
