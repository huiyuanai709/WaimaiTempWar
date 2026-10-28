using System;
using System.Collections.Generic;
using Godot;

namespace DeliveryTemperatureWar;

public partial class EventDirector : Node
{
    private readonly HashSet<DeliveryEvent> _armed = new();
    private readonly HashSet<DeliveryEvent> _triggered = new();
    private readonly RandomNumberGenerator _rng = new();
    private DeliveryEvent? _active;
    private DeliveryEvent? _queued;
    private float _eventTime;
    private bool _arrivalStarted;
    private bool _ranLight;
    private float _dogPulse;
    private float _crushAtBrake;
    private AnimatableBody3D? _brakeCar;
    private Vector3 _brakeVelocity;
    private bool _brakeReleased;
    private AnimatableBody3D? _crossCar;
    private Area3D? _wind;
    private Node3D? _dog;
    private GpuParticles3D? _rain;
    private BikeController? _bike;
    private CargoRig? _cargo;
    private Streetscape? _street;
    private Achievements? _achievements;

    public string Banner { get; private set; } = "";
    public string ArmedLine { get; private set; } = "";
    public float WindBonus { get; private set; }
    public float ExtraCooling { get; private set; }
    public bool ControlsLocked { get; private set; }
    public bool WantsBrake { get; private set; }
    public float HoldRemaining { get; private set; }
    public bool HardBrakeRan { get; private set; }
    public bool HardBrakeCleared { get; private set; }
    public int DistinctCount => _triggered.Count;

    [Signal]
    public delegate void HoldStartedEventHandler();

    [Signal]
    public delegate void ArrivalFinishedEventHandler();

    public override void _EnterTree()
    {
        AddToGroup("events");
    }

    public override void _Ready()
    {
        _rng.Randomize();
        _bike = GetNode<BikeController>("../Bike");
        _cargo = GetNode<CargoRig>("../Cargo");
        _street = GetNode<Streetscape>("../Street");
        _achievements = GetNode<Achievements>("../Achievements");
        Arm();
        _street.Hook(this);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        WantsBrake = false;
        if (_crossCar != null)
        {
            TickCross(dt);
        }

        if (_active is DeliveryEvent current)
        {
            _eventTime += dt;
            switch (current)
            {
                case DeliveryEvent.RedLight:
                    TickRed();
                    break;
                case DeliveryEvent.SuddenBrake:
                    TickBrake(dt);
                    break;
                case DeliveryEvent.DogChase:
                    TickDog(dt);
                    break;
                case DeliveryEvent.RainGust:
                    TickRain();
                    break;
                case DeliveryEvent.ElevatorWait:
                    TickHold(6.4f, "电梯等待  Elevator");
                    break;
                case DeliveryEvent.WrongGate:
                    TickHold(4.6f, "门禁错误  Wrong gate code");
                    break;
            }
        }
        else if (_queued is DeliveryEvent next && !_arrivalStarted)
        {
            DeliveryEvent queued = next;
            _queued = null;
            Begin(queued);
        }
    }

    public void OnZone(DeliveryEvent kind)
    {
        if (!_armed.Contains(kind) || _triggered.Contains(kind))
        {
            return;
        }

        if (kind is DeliveryEvent.ElevatorWait or DeliveryEvent.WrongGate)
        {
            return;
        }

        // Crossing the zone is the trigger, even if another event is still playing.
        Note(kind);
        if (_arrivalStarted)
        {
            return;
        }

        if (_active == null)
        {
            Begin(kind);
        }
        else if (_queued == null)
        {
            _queued = kind;
        }
    }

    public void RequestArrival()
    {
        if (_arrivalStarted)
        {
            return;
        }

        _arrivalStarted = true;
        StopRouteEffects();
        _active = null;
        _queued = null;
        DeliveryEvent arrival = _armed.Contains(DeliveryEvent.WrongGate) && !_armed.Contains(DeliveryEvent.ElevatorWait)
            ? DeliveryEvent.WrongGate
            : _armed.Contains(DeliveryEvent.ElevatorWait)
                ? DeliveryEvent.ElevatorWait
                : DeliveryEvent.ElevatorWait;
        Begin(arrival);
    }

    private void Arm()
    {
        _armed.Clear();
        if (DeliveryGame.IsAutotest())
        {
            _armed.Add(DeliveryEvent.RedLight);
            _armed.Add(DeliveryEvent.DogChase);
            _armed.Add(DeliveryEvent.ElevatorWait);
        }
        else
        {
            string env = OS.GetEnvironment("WAIMAI_EVENTS");
            if (!string.IsNullOrWhiteSpace(env))
            {
                ArmFromEnv(env);
            }
            else
            {
                ArmRandom();
            }
        }

        if (!_armed.Contains(DeliveryEvent.ElevatorWait) && !_armed.Contains(DeliveryEvent.WrongGate))
        {
            _armed.Add(DeliveryEvent.ElevatorWait);
        }

        var names = new List<string>();
        foreach (DeliveryEvent ev in _armed)
        {
            names.Add(ShortName(ev));
        }

        ArmedLine = "路况  " + string.Join(" · ", names);
        GD.Print("ARMED " + ArmedLine);
    }

    private void ArmRandom()
    {
        var route = new List<DeliveryEvent>
        {
            DeliveryEvent.RedLight,
            DeliveryEvent.SuddenBrake,
            DeliveryEvent.DogChase,
            DeliveryEvent.RainGust,
        };
        for (int i = route.Count - 1; i > 0; i--)
        {
            int j = _rng.RandiRange(0, i);
            (route[i], route[j]) = (route[j], route[i]);
        }

        _armed.Add(route[0]);
        _armed.Add(route[1]);
        _armed.Add(_rng.Randf() < 0.5f ? DeliveryEvent.ElevatorWait : DeliveryEvent.WrongGate);
    }

    private void ArmFromEnv(string env)
    {
        string text = env.ToLowerInvariant();
        if (text.Contains("red"))
        {
            _armed.Add(DeliveryEvent.RedLight);
        }

        if (text.Contains("brake"))
        {
            _armed.Add(DeliveryEvent.SuddenBrake);
        }

        if (text.Contains("dog"))
        {
            _armed.Add(DeliveryEvent.DogChase);
        }

        if (text.Contains("rain"))
        {
            _armed.Add(DeliveryEvent.RainGust);
        }

        if (text.Contains("gate"))
        {
            _armed.Add(DeliveryEvent.WrongGate);
        }

        if (text.Contains("elevator") || _armed.Count < 2)
        {
            _armed.Add(DeliveryEvent.ElevatorWait);
        }
    }

    private void Begin(DeliveryEvent ev)
    {
        _active = ev;
        _eventTime = 0f;
        Note(ev);
        switch (ev)
        {
            case DeliveryEvent.RedLight:
                Banner = "红灯 — 停在线前   Red light, stop";
                _street?.ForceMainRed(6.4f);
                break;
            case DeliveryEvent.SuddenBrake:
                Banner = "前车急刹！  Sudden brake";
                SpawnBrakeCar();
                break;
            case DeliveryEvent.DogChase:
                Banner = "有狗追车！  Dog chase";
                ExtraCooling = 0.2f;
                SpawnDog();
                break;
            case DeliveryEvent.RainGust:
                Banner = "风雨交加  Rain gust";
                WindBonus = 1.25f;
                SpawnWind();
                _cargo?.NudgeLid(2.4f);
                break;
            case DeliveryEvent.ElevatorWait:
                Banner = "电梯等待  Elevator";
                ControlsLocked = true;
                ExtraCooling = 0.32f;
                EmitSignal(SignalName.HoldStarted);
                break;
            case DeliveryEvent.WrongGate:
                Banner = "门禁错误  Wrong gate code";
                ControlsLocked = true;
                ExtraCooling = 0.4f;
                _cargo?.AddSpill(0.02f);
                EmitSignal(SignalName.HoldStarted);
                break;
        }
    }

    private void Note(DeliveryEvent ev)
    {
        if (!_triggered.Add(ev))
        {
            return;
        }

        GD.Print($"EVENT {ev}");
        if (_triggered.Count >= 3)
        {
            _achievements?.Unlock("triple_event");
        }
    }

    private void EndActive(bool finishedArrival)
    {
        DeliveryEvent? ending = _active;
        _active = null;
        Banner = "";
        WantsBrake = false;
        if (ending is DeliveryEvent.DogChase)
        {
            ExtraCooling = 0f;
        }

        if (ending is DeliveryEvent.RainGust)
        {
            WindBonus = 0f;
        }

        if (finishedArrival)
        {
            ControlsLocked = false;
            ExtraCooling = 0f;
            HoldRemaining = 0f;
            EmitSignal(SignalName.ArrivalFinished);
            return;
        }

        if (_queued is DeliveryEvent next && !_arrivalStarted)
        {
            _queued = null;
            Begin(next);
        }
    }

    private void TickRed()
    {
        if (_bike == null)
        {
            return;
        }

        float z = _bike.GlobalPosition.Z;
        WantsBrake = z < Track.RedZ + 8f && z > Track.IntersectionZ - 6f;
        bool inBox = Mathf.Abs(_bike.GlobalPosition.X) < 5.6f
            && z < Track.IntersectionZ + 4f
            && z > Track.IntersectionZ - 8f;
        if (inBox && _bike.ForwardSpeed > 3.5f && !_ranLight)
        {
            _ranLight = true;
            Banner = "闯红灯！  Ran the light";
            SpawnCrossCar();
        }

        if (_eventTime > 5.6f)
        {
            EndActive(false);
        }
    }

    private void TickBrake(float dt)
    {
        if (_brakeCar != null && !_brakeReleased)
        {
            float speed = _brakeVelocity.Length();
            if (speed > 0.08f)
            {
                float next = Mathf.Max(0f, speed - 9.5f * dt);
                _brakeVelocity = _brakeVelocity.Normalized() * next;
            }
            else
            {
                _brakeVelocity = Vector3.Zero;
            }

            Vector3 position = _brakeCar.GlobalPosition + _brakeVelocity * dt;
            position.Y = 0f;
            _brakeCar.GlobalPosition = position;
        }

        if (_eventTime > 3.6f)
        {
            _brakeReleased = true;
            HardBrakeRan = true;
            float crush = _cargo?.Crush01 ?? 1f;
            HardBrakeCleared = crush - _crushAtBrake < 0.08f;
            if (HardBrakeCleared)
            {
                _achievements?.Unlock("hard_brake");
            }

            EndActive(false);
        }
    }

    private void TickDog(float dt)
    {
        if (_dog != null && _bike != null)
        {
            Vector3 side = -_bike.GlobalTransform.Basis.X * 1.05f;
            Vector3 target = _bike.GlobalPosition + side;
            target.Y = 0.32f;
            float blend = 1f - Mathf.Exp(-7f * dt);
            _dog.GlobalPosition = _dog.GlobalPosition.Lerp(target, blend);
            Vector3 look = _bike.GlobalPosition;
            look.Y = _dog.GlobalPosition.Y;
            if (_dog.GlobalPosition.DistanceSquaredTo(look) > 0.05f)
            {
                _dog.LookAt(look, Vector3.Up);
            }
        }

        _dogPulse -= dt;
        if (_dogPulse <= 0f && _bike != null)
        {
            _dogPulse = 0.42f;
            float kick = _rng.RandfRange(12f, 22f) * (_rng.Randf() < 0.5f ? -1f : 1f);
            _bike.ApplyCentralImpulse(_bike.GlobalTransform.Basis.X * kick);
            _cargo?.NudgeContents(kick * 0.004f);
        }

        if (_eventTime > 4.3f)
        {
            ExtraCooling = 0f;
            _dog?.QueueFree();
            _dog = null;
            EndActive(false);
        }
    }

    private void TickRain()
    {
        _bike?.ApplyCentralForce(Vector3.Right * 380f);
        if (_eventTime > 5.1f)
        {
            WindBonus = 0f;
            _wind?.QueueFree();
            _wind = null;
            _rain?.QueueFree();
            _rain = null;
            EndActive(false);
        }
    }

    private void TickHold(float duration, string label)
    {
        HoldRemaining = Mathf.Max(0f, duration - _eventTime);
        Banner = $"{label}  {HoldRemaining:0.0}s";
        ControlsLocked = true;
        if (_eventTime >= duration)
        {
            EndActive(true);
        }
    }

    private void StopRouteEffects()
    {
        ExtraCooling = 0f;
        WindBonus = 0f;
        WantsBrake = false;
        _dog?.QueueFree();
        _dog = null;
        _wind?.QueueFree();
        _wind = null;
        _rain?.QueueFree();
        _rain = null;
        _brakeReleased = true;
    }

    private void SpawnBrakeCar()
    {
        if (_bike == null)
        {
            return;
        }

        _crushAtBrake = _cargo?.Crush01 ?? 0f;
        _brakeCar = PropFactory.TrafficCar(new Color("d64545"));
        GetParent().AddChild(_brakeCar);
        Vector3 forward = -_bike.GlobalTransform.Basis.Z;
        forward.Y = 0f;
        forward = forward.LengthSquared() < 0.001f ? Vector3.Forward : forward.Normalized();
        Vector3 position = _bike.GlobalPosition + forward * 10.5f;
        position.Y = 0f;
        _brakeCar.GlobalPosition = position;
        _brakeCar.GlobalBasis = _bike.GlobalBasis;
        _brakeVelocity = forward * 4f;
    }

    private void SpawnCrossCar()
    {
        _crossCar = PropFactory.TrafficCar(new Color("f4f4f4"));
        GetParent().AddChild(_crossCar);
        _crossCar.GlobalPosition = new Vector3(-16f, 0f, Track.IntersectionZ);
        _crossCar.GlobalBasis = Basis.LookingAt(Vector3.Right, Vector3.Up);
    }

    private void TickCross(float dt)
    {
        if (_crossCar == null)
        {
            return;
        }

        Vector3 position = _crossCar.GlobalPosition + Vector3.Right * 11f * dt;
        position.Y = 0f;
        _crossCar.GlobalPosition = position;
        if (position.X > 18f)
        {
            _crossCar.QueueFree();
            _crossCar = null;
        }
    }

    private void SpawnDog()
    {
        if (_bike == null)
        {
            return;
        }

        _dog = PropFactory.Dog();
        GetParent().AddChild(_dog);
        _dog.GlobalPosition = _bike.GlobalPosition + new Vector3(-2.4f, 0.32f, 1.2f);
        _dogPulse = 0.2f;
    }

    private void SpawnWind()
    {
        if (_bike == null)
        {
            return;
        }

        _wind = new Area3D
        {
            Name = "Gust",
            Monitoring = false,
            Monitorable = false,
            CollisionLayer = 0,
            CollisionMask = Layers.Bag,
            GravitySpaceOverride = Area3D.SpaceOverride.Disabled,
            Gravity = 0f,
            WindForceMagnitude = 1.8f,
            WindAttenuationFactor = 0.04f,
        };
        _wind.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(7f, 3.5f, 7f) },
        });
        _bike.AddChild(_wind);
        _wind.Position = new Vector3(0f, 1.1f, 0.2f);
        _wind.RotationDegrees = new Vector3(0f, 90f, 0f);
        _wind.WindSourcePath = _wind.GetPath();

        if (DisplayServer.GetName() == "headless")
        {
            return;
        }

        _rain = new GpuParticles3D
        {
            Amount = 380,
            Lifetime = 0.9f,
            VisibilityAabb = new Aabb(new Vector3(-12f, -4f, -12f), new Vector3(24f, 16f, 24f)),
            Position = new Vector3(0f, 6f, 0f),
        };
        var process = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(7f, 0.4f, 7f),
            Direction = new Vector3(0.35f, -1f, 0.1f),
            Spread = 12f,
            InitialVelocityMin = 10f,
            InitialVelocityMax = 14f,
            Gravity = new Vector3(3f, -12f, 0f),
        };
        _rain.ProcessMaterial = process;
        var drop = new QuadMesh { Size = new Vector2(0.035f, 0.42f) };
        var dropMat = MeshUtil.Fresh(new Color(0.75f, 0.85f, 1f, 0.45f), 0.2f);
        dropMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        dropMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        drop.Material = dropMat;
        _rain.DrawPass1 = drop;
        _bike.AddChild(_rain);
    }

    private static string ShortName(DeliveryEvent ev)
    {
        return ev switch
        {
            DeliveryEvent.RedLight => "红灯",
            DeliveryEvent.SuddenBrake => "急刹",
            DeliveryEvent.DogChase => "狗追",
            DeliveryEvent.RainGust => "风雨",
            DeliveryEvent.ElevatorWait => "电梯",
            DeliveryEvent.WrongGate => "门禁",
            _ => ev.ToString(),
        };
    }
}
