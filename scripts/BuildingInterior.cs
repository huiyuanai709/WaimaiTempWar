using Godot;

namespace DeliveryTemperatureWar;

/// <summary>
/// Lobby past the street drop-off. A working elevator rides up; a broken one
/// forces a ramp where stamina and time can spill the soup.
/// </summary>
public partial class BuildingInterior : Node3D
{
    private enum Step
    {
        WaitEnter,
        ToCall,
        Call,
        ToCab,
        Ride,
        ToRamp,
        Climb,
        ToHall,
        ToDoor,
        Code,
        Deliver,
        Done,
    }

    private Courier? _courier;
    private BikeController? _bike;
    private DeliveryGame? _game;
    private EventDirector? _events;
    private Achievements? _achievements;
    private TemperatureSim? _temp;
    private AnimatableBody3D? _cab;
    private StaticBody3D? _doors;
    private StaticBody3D? _upperGate;
    private StaticBody3D? _stairGate;
    private Vector3 _cabStart;
    private Step _step = Step.WaitEnter;
    private float _wait;
    private float _ride;
    private float _code;
    private bool _entered;
    private bool _doorsOpen;
    private bool _arrived;
    private bool _counted;

    public bool Stairs { get; private set; }
    public bool UsedElevator { get; private set; }
    public bool UsedStairs { get; private set; }
    public bool Finished { get; private set; }
    public string Status { get; private set; } = "";
    public float StairCooling => _entered && _courier != null && _courier.Climbing ? 0.35f : 0f;

    private PhaseEvents? _pain;
    private float _wrongHold;

    public override void _EnterTree()
    {
        AddToGroup("building");
    }

    public override void _Ready()
    {
        GlobalPosition = Track.Lobby;
        _courier = GetNode<Courier>("../Courier");
        _bike = GetNode<BikeController>("../Bike");
        _game = GetNode<DeliveryGame>("..");
        _events = GetNode<EventDirector>("../Events");
        _achievements = GetNode<Achievements>("../Achievements");
        _temp = GetNode<TemperatureSim>("../Temperature");
        _pain = GetNode<PhaseEvents>("../Events/Pain");
        if (OS.GetEnvironment("WAIMAI_STAIRS") == "1")
        {
            Stairs = true;
        }
        else if (OS.GetEnvironment("WAIMAI_ELEVATOR") == "1" || DeliveryGame.IsAutotest())
        {
            Stairs = false;
        }
        else
        {
            Stairs = _events.ArmedGate && !_events.ArmedElevator;
        }

        BuildLobby();
        GD.Print(Stairs ? "LOBBY stairs" : "LOBBY elevator");
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (_game == null || _events == null || _courier == null)
        {
            return;
        }

        if (!_entered)
        {
            if (_events.ArrivalStarted && (DeliveryGame.IsAutotest() || _pressedEnter))
            {
                Enter();
            }

            return;
        }

        UpdateClimb();
        if (!Stairs)
        {
            TickCab(dt);
        }

        if (DeliveryGame.IsAutotest() && _step != Step.Done)
        {
            Autopilot(dt);
        }

        Status = Stairs
            ? "302 室在楼上\n电梯故障，只能走楼梯。停下来能回体力。"
            : "302 室在楼上\n按按钮等电梯，进厢后再关门。";
        if (_game.Phase == DeliveryGame.RunPhase.Indoors)
        {
            _events.SetBanner(IndoorBanner());
        }
    }

    private bool _pressedEnter;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_entered || DeliveryGame.IsAutotest())
        {
            return;
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.PhysicalKeycode == Key.E && _events != null && _events.ArrivalStarted)
        {
            _pressedEnter = true;
            GetViewport().SetInputAsHandled();
        }
    }

    public bool TryInteract(Courier courier)
    {
        if (!_entered || Finished || _game == null || _game.Phase != DeliveryGame.RunPhase.Indoors)
        {
            return false;
        }

        if (!Stairs && !_doorsOpen && Near(courier, CallPoint(), 1.5f))
        {
            if (_wait <= 0f)
            {
                _wait = 0.01f;
                GD.Print("ELEVATOR call");
            }

            return true;
        }

        if (CanDeliver() && Near(courier, DoorPoint(), 1.5f))
        {
            Deliver();
            return true;
        }

        return false;
    }

    private void Enter()
    {
        _entered = true;
        _pressedEnter = false;
        _bike?.Park();
        _courier!.AppearAt(ToGlobal(new Vector3(0f, 0.12f, 3.5f)));
        _courier.LookAt(ToGlobal(new Vector3(0f, 0.12f, 0f)), Vector3.Up);
        _game!.BeginIndoors();
        _step = Stairs ? Step.ToRamp : Step.ToCall;
        GD.Print(Stairs ? "ENTER stairs" : "ENTER elevator");
    }

    private void Autopilot(float dt)
    {
        if (_courier == null)
        {
            return;
        }

        switch (_step)
        {
            case Step.ToCall:
                Seek(CallPoint(), 0.45f, Step.Call);
                break;
            case Step.Call:
                _courier.StopSeek();
                TryInteract(_courier);
                if (_doorsOpen)
                {
                    _step = Step.ToCab;
                }

                break;
            case Step.ToCab:
                Seek(CabPoint(), 0.4f, Step.Ride);
                break;
            case Step.Ride:
                _courier.StopSeek();
                if (_arrived)
                {
                    _step = Step.ToDoor;
                }

                break;
            case Step.ToRamp:
                Seek(ToGlobal(new Vector3(4.55f, 0.15f, 4.15f)), 0.4f, Step.Climb);
                break;
            case Step.Climb:
                Seek(ToGlobal(new Vector3(4.55f, 3.05f, -1.85f)), 0.45f, Step.ToHall);
                break;
            case Step.ToHall:
                Seek(ToGlobal(new Vector3(0.5f, 3.1f, -1.85f)), 0.4f, Step.ToDoor);
                break;
            case Step.ToDoor:
                Seek(DoorPoint(), 0.5f, Step.Code);
                break;
            case Step.Code:
                _courier.StopSeek();
                if (_code >= DoorNeed())
                {
                    _step = Step.Deliver;
                }

                break;
            case Step.Deliver:
                _courier.StopSeek();
                TryInteract(_courier);
                break;
        }

        if (DoorNeed() > 0f && _code < DoorNeed() && _courier != null && Near(_courier, DoorPoint(), 1.6f))
        {
            _code += dt;
        }
    }

    private float DoorNeed()
    {
        return (Stairs ? 1.5f : 0f) + (_pain?.DoorExtra ?? 0f);
    }

    private float CallNeed()
    {
        return 3.1f + (_pain?.ElevatorExtra ?? 0f);
    }

    private void Seek(Vector3 world, float radius, Step next)
    {
        _courier!.Seek(world);
        if (_courier.Reached(radius))
        {
            _step = next;
        }
    }

    private void TickCab(float dt)
    {
        if (_wait > 0f && !_doorsOpen)
        {
            _wait += dt;
            if (_wait > CallNeed())
            {
                _doorsOpen = true;
                if (_doors != null)
                {
                    _doors.CollisionLayer = 0;
                    _doors.Position = new Vector3(1.7f, 1.35f, -1.35f);
                }

                GD.Print("ELEVATOR open");
            }
        }

        if (_courier == null || _cab == null || !_doorsOpen || _arrived)
        {
            return;
        }

        if (!_rideStarted && Near(_courier, CabPoint(), 0.95f))
        {
            _rideStarted = true;
            _ride = 0.01f;
            if (!_counted && _events != null)
            {
                _counted = true;
                _events.CountEvent(DeliveryEvent.ElevatorWait);
            }

            GD.Print("ELEVATOR ride");
        }

        if (!_rideStarted)
        {
            return;
        }

        _ride += dt;
        float blend = Mathf.Clamp(_ride / 3.2f, 0f, 1f);
        Vector3 pos = _cabStart;
        pos.Y += 3f * blend;
        _cab.GlobalPosition = pos;
        if (Near(_courier, CabPoint(), 1.3f))
        {
            Vector3 held = _courier.GlobalPosition;
            held.Y = _cab.GlobalPosition.Y + 0.12f;
            held.X = Mathf.Lerp(held.X, _cab.GlobalPosition.X, 0.2f);
            held.Z = Mathf.Lerp(held.Z, _cab.GlobalPosition.Z, 0.2f);
            _courier.GlobalPosition = held;
            _courier.Velocity = Vector3.Zero;
        }

        if (blend >= 1f)
        {
            if (_pain != null && _pain.WrongFloor && _wrongHold < 2.2f)
            {
                _wrongHold += dt;
                return;
            }

            _arrived = true;
            UsedElevator = true;
            if (_upperGate != null)
            {
                _upperGate.CollisionLayer = 0;
                _upperGate.Visible = false;
            }
        }
    }

    private bool _rideStarted;

    private void UpdateClimb()
    {
        if (_courier == null || !Stairs)
        {
            if (_courier != null)
            {
                _courier.Climbing = false;
            }

            return;
        }

        Vector3 local = ToLocal(_courier.GlobalPosition);
        bool onRamp = local.X > 3.3f && local.X < 5.9f && local.Y < 3.2f && local.Z < 2.8f && local.Z > -3.2f;
        _courier.Climbing = onRamp && local.Y < 2.9f;
        if (onRamp && local.Y > 2.55f)
        {
            UsedStairs = true;
            if (!_counted && _events != null)
            {
                _counted = true;
                _events.CountEvent(DeliveryEvent.WrongGate);
            }
        }
    }

    private bool CanDeliver()
    {
        return UsedElevator || UsedStairs;
    }

    private void Deliver()
    {
        if (Finished || _game == null)
        {
            return;
        }

        if (_code < DoorNeed())
        {
            return;
        }

        Finished = true;
        _step = Step.Done;
        _courier?.StopSeek();
        GD.Print(UsedStairs ? "DOOR stairs" : "DOOR elevator");
        _game.FinishDelivery();
    }

    private string IndoorBanner()
    {
        if (Stairs && _courier != null && _courier.Climbing && _courier.Stamina < 0.3f)
        {
            return "喘口气再爬  Rest on the stairs";
        }

        if (Stairs && !UsedStairs)
        {
            return "电梯故障  走楼梯上 302";
        }

        if (!Stairs && _wait <= 0f)
        {
            return "E 呼叫电梯";
        }

        if (!Stairs && !_doorsOpen)
        {
            return $"电梯来了  {Mathf.Max(0f, CallNeed() - _wait):0.0}s";
        }

        if (!Stairs && !_arrived)
        {
            return "进电梯，正在上行";
        }

        if (_code > 0f && _code < DoorNeed())
        {
            return $"门口耗着  {Mathf.Max(0f, DoorNeed() - _code):0.0}s";
        }

        if (_wrongHold > 0f && !_arrived)
        {
            return "坐过站了  Wrong floor";
        }

        return CanDeliver() ? "E 送到 302" : "上楼";
    }

    private static bool Near(Node3D body, Vector3 world, float radius)
    {
        Vector3 flat = body.GlobalPosition - world;
        flat.Y = 0f;
        return flat.Length() <= radius;
    }

    private Vector3 CallPoint()
    {
        return ToGlobal(new Vector3(1.35f, 0.2f, -0.55f));
    }

    private Vector3 CabPoint()
    {
        return ToGlobal(new Vector3(0f, _cab != null ? _cab.Position.Y + 0.2f : 0.2f, -2.45f));
    }

    private Vector3 DoorPoint()
    {
        return ToGlobal(new Vector3(-0.2f, 3.15f, 1.7f));
    }

    private void AddWalkRamp(Vector3 lowTop, Vector3 highTop, float width)
    {
        Vector3 along = highTop - lowTop;
        float angleRad = Mathf.Atan2(along.Y, -along.Z);
        const float extra = 0.85f;
        const float thick = 0.5f;
        float halfL = (along.Length() + extra) * 0.5f;
        float halfT = thick * 0.5f;
        float c = Mathf.Cos(angleRad);
        float s = Mathf.Sin(angleRad);
        float oy = halfT * c + halfL * s;
        float oz = halfT * s - halfL * c;
        Vector3 center = new Vector3(highTop.X, highTop.Y - oy, highTop.Z - oz);
        MeshUtil.Solid(
            this,
            new Vector3(width, thick, halfL * 2f),
            center,
            new Color("9aa3ad"),
            new Vector3(Mathf.RadToDeg(angleRad), 0f, 0f));
    }

    private void BuildLobby()
    {
        Color wall = new Color("efe6da");
        Color floor = new Color("b7b1a8");
        MeshUtil.Solid(this, new Vector3(12f, 0.16f, 9.2f), new Vector3(0.3f, -0.08f, 1.1f), floor);
        // Upstairs floor stays left of the ramp. A short pad sits under the
        // high end only, where the slope is already above the floor.
        MeshUtil.Solid(this, new Vector3(9.1f, 0.16f, 6.4f), new Vector3(-1.15f, 2.92f, 1.05f), floor);
        MeshUtil.Solid(this, new Vector3(3.15f, 0.16f, 0.7f), new Vector3(4.78f, 2.92f, -2.05f), floor);
        MeshUtil.Solid(this, new Vector3(12.4f, 6.2f, 0.16f), new Vector3(0.3f, 3.1f, 4.75f), wall);
        MeshUtil.Solid(this, new Vector3(0.16f, 6.2f, 9.4f), new Vector3(-5.85f, 3.1f, 0.2f), wall);
        MeshUtil.Solid(this, new Vector3(0.16f, 6.2f, 9.4f), new Vector3(6.35f, 3.1f, 0.2f), wall);
        // Shaft sides stop below the upstairs floor so the stair landing can reach the hall.
        MeshUtil.Solid(this, new Vector3(0.16f, 2.65f, 2.6f), new Vector3(-1.45f, 1.325f, -2.7f), wall);
        MeshUtil.Solid(this, new Vector3(0.16f, 2.65f, 2.6f), new Vector3(1.45f, 1.325f, -2.7f), wall);
        MeshUtil.Solid(this, new Vector3(2.9f, 6.2f, 0.16f), new Vector3(0f, 3.1f, -3.95f), wall);

        _cab = new AnimatableBody3D
        {
            Name = "Cab",
            SyncToPhysics = true,
            CollisionLayer = Layers.World,
            CollisionMask = 0,
            Position = new Vector3(0f, -0.08f, -2.4f),
        };
        MeshUtil.Box(_cab, new Vector3(2.5f, 0.16f, 2.45f), Vector3.Zero, new Color("d9d3c8"));
        _cab.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(2.5f, 0.16f, 2.45f) },
        });
        AddChild(_cab);
        _cabStart = _cab.GlobalPosition;

        _doors = MeshUtil.Solid(this, new Vector3(2.5f, 2.3f, 0.12f), new Vector3(0f, 1.2f, -1.35f), new Color("8aa0b2"));
        _upperGate = MeshUtil.Solid(this, new Vector3(2.5f, 2.2f, 0.12f), new Vector3(0f, 4.15f, -1.32f), new Color("8aa0b2"));
        if (Stairs)
        {
            _upperGate.CollisionLayer = 0;
            _upperGate.Visible = false;
        }
        MeshUtil.Sign(this, Stairs ? "电梯故障" : "电梯", new Vector3(1.7f, 2.1f, -0.9f), 0.008f);
        MeshUtil.Sign(this, "302", new Vector3(-0.2f, 4.5f, 1.55f), 0.01f);
        MeshUtil.Box(this, new Vector3(0.7f, 2.1f, 0.08f), new Vector3(-0.2f, 4.15f, 2.15f), new Color("6b4a32"), false);

        AddWalkRamp(new Vector3(4.55f, -0.3f, 1.55f), new Vector3(4.55f, 3f, -1.35f), 1.7f);
        _stairGate = MeshUtil.Solid(this, new Vector3(1.8f, 1.4f, 0.16f), new Vector3(4.55f, 0.8f, 2.15f), new Color("5c656e"));
        if (Stairs)
        {
            _stairGate.CollisionLayer = 0;
            _stairGate.Visible = false;
        }

        MeshUtil.Sign(this, Stairs ? "楼梯" : "楼梯关闭", new Vector3(4.55f, 2.2f, 2.3f), 0.007f);
    }
}
