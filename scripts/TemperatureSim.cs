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
    private PhaseEvents? _pain;
    private BuildingInterior? _building;

    public float Celsius { get; private set; } = Track.StartCelsius;
    public bool Paused { get; set; } = true;

    public void StartClock()
    {
        Paused = false;
    }

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
        float wind = _bike.Mounted ? Mathf.Max(0f, _bike.ForwardSpeed) * SpeedCooling : 0f;
        if (_events != null)
        {
            wind += _events.WindBonus;
        }

        _pain ??= GetTree().GetFirstNodeInGroup("pain") as PhaseEvents;
        _building ??= GetTree().GetFirstNodeInGroup("building") as BuildingInterior;
        float extra = (_events?.ExtraCooling ?? 0f) + (_pain?.Cooling ?? 0f) + (_building?.StairCooling ?? 0f);
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
