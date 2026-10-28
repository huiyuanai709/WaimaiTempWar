using Godot;

namespace DeliveryTemperatureWar;

public partial class BikeController : RigidBody3D
{
    [Export] public float EngineForce = 1250f;
    [Export] public float BrakeForce = 2400f;
    [Export] public float MaxSpeed = 9.2f;
    [Export] public float YawRate = 1.85f;

    private float _throttle;
    private float _brake;
    private bool _reverse;
    private float _steer;
    private float _impact;
    private MeshInstance3D? _rearWheel;
    private MeshInstance3D? _frontWheel;
    private Node3D? _model;
    private Node3D? _rider;
    private StandardMaterial3D? _brakeLamp;
    private EventDirector? _events;
    private CargoRig? _cargo;

    private PhaseEvents? _pain;

    public bool Autopilot { get; set; }
    public bool Mounted { get; private set; }
    public float ForwardSpeed { get; private set; }
    public float Steer => _steer;
    public float Throttle => _throttle;
    public float Brake => _brake;

    public override void _EnterTree()
    {
        AddToGroup("bike");
    }

    public override void _Ready()
    {
        Mass = 95f;
        CanSleep = false;
        ContinuousCd = true;
        ContactMonitor = true;
        MaxContactsReported = 8;
        CollisionLayer = Layers.Bike;
        CollisionMask = Layers.World | Layers.Traffic;
        LinearDamp = 0.08f;
        AngularDamp = 0.4f;
        PhysicsMaterialOverride = new PhysicsMaterial
        {
            Friction = 0.35f,
            Bounce = 0.0f,
        };
        CenterOfMassMode = CenterOfMassModeEnum.Custom;
        CenterOfMass = new Vector3(0f, 0.35f, -0.05f);
        GlobalPosition = new Vector3(Track.LaneX, 0.02f, Track.StartZ);
        GlobalRotation = Vector3.Zero;
        BuildBody();
        Mounted = false;
        Freeze = true;
        RiderSkins.EnsureLoaded();
        RiderSkins.Changed += ApplyRider;
        ApplyRider();
    }

    public override void _ExitTree()
    {
        RiderSkins.Changed -= ApplyRider;
    }

    private void ApplyRider()
    {
        _rider?.QueueFree();
        if (_model == null)
        {
            return;
        }

        _rider = RiderLooks.Build(RiderSkins.Current, true);
        _rider.Position = new Vector3(0f, 0.82f, 0.06f);
        _rider.Visible = Mounted;
        _model.AddChild(_rider);
    }

    private void ShowRider(bool visible)
    {
        if (_rider != null)
        {
            _rider.Visible = visible;
        }
    }

    public void WakeForRide(bool autopilot)
    {
        Mounted = true;
        Freeze = false;
        Sleeping = false;
        Autopilot = autopilot;
        ShowRider(true);
    }

    public void Park()
    {
        Autopilot = false;
        Mounted = false;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        ForwardSpeed = 0f;
        Freeze = true;
        ShowRider(false);
    }

    public override void _PhysicsProcess(double delta)
    {
        _events ??= GetTree().GetFirstNodeInGroup("events") as EventDirector;
        _cargo ??= GetTree().GetFirstNodeInGroup("cargo") as CargoRig;
        _pain ??= GetTree().GetFirstNodeInGroup("pain") as PhaseEvents;
        if (!Mounted)
        {
            return;
        }

        float kick = _pain?.ConsumeImpulse() ?? 0f;
        if (Mathf.Abs(kick) > 0.1f)
        {
            ApplyCentralImpulse(GlobalTransform.Basis.X * kick);
        }

        if (_pain?.ConsumeLid() == true)
        {
            _cargo?.NudgeLid(2.2f);
        }

        if (_pain?.ConsumeFood() == true)
        {
            _cargo?.NudgeContents(0.45f);
        }

        float spill = _pain?.ConsumeSpill() ?? 0f;
        if (spill > 0f)
        {
            _cargo?.AddSpill(spill);
        }

        bool locked = _events != null && _events.ControlsLocked;
        if (Autopilot)
        {
            AutopilotInput(locked);
        }
        else
        {
            KeyboardInput(locked);
        }
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        float step = state.GetStep();
        Basis basis = state.Transform.Basis;
        Vector3 forward = -basis.Z;
        forward.Y = 0f;
        if (forward.LengthSquared() < 0.0001f)
        {
            forward = Vector3.Forward;
        }

        forward = forward.Normalized();
        Vector3 right = basis.X;
        right.Y = 0f;
        right = right.Normalized();

        Vector3 velocity = state.LinearVelocity;
        ForwardSpeed = velocity.Dot(forward);

        float impulse = 0f;
        int contacts = state.GetContactCount();
        for (int i = 0; i < contacts; i++)
        {
            impulse += state.GetContactImpulse(i).Length();
        }

        _impact = impulse;

        if (_events != null && _events.ControlsLocked)
        {
            velocity.X = Mathf.Lerp(velocity.X, 0f, 1f - Mathf.Exp(-8f * step));
            velocity.Z = Mathf.Lerp(velocity.Z, 0f, 1f - Mathf.Exp(-8f * step));
            state.LinearVelocity = velocity;
            Vector3 stop = state.AngularVelocity;
            stop.Y = 0f;
            state.AngularVelocity = stop;
            return;
        }

        Vector3 lateral = right * velocity.Dot(right);
        float grip = contacts > 0 ? 3.5f : 11f;
        velocity -= lateral * (1f - Mathf.Exp(-grip * step));
        if (contacts > 0)
        {
            Vector3 away = Vector3.Zero;
            for (int i = 0; i < contacts; i++)
            {
                Vector3 normal = basis * state.GetContactLocalNormal(i);
                Vector3 horizontal = normal;
                horizontal.Y = 0f;
                if (horizontal.Length() > 0.45f)
                {
                    away += horizontal;
                }
            }

            if (away.LengthSquared() > 0.04f && Mathf.Abs(ForwardSpeed) < 1.4f)
            {
                away = away.Normalized();
                Transform3D freed = state.Transform;
                freed.Origin += away * 0.14f;
                state.Transform = freed;
                velocity += away * 2.4f;
            }
        }
        float cap = MaxSpeed * Mathf.Clamp(_pain?.BikeScale ?? 1f, 0.45f, 1f);
        if (ForwardSpeed > cap)
        {
            velocity -= forward * (ForwardSpeed - cap);
        }

        velocity.Y = Mathf.Clamp(velocity.Y, -8f, 3.5f);
        if (velocity.Length() > 16f)
        {
            velocity = velocity.Normalized() * 16f;
        }

        float drive = 0f;
        if (_brake > 0f && Mathf.Abs(ForwardSpeed) > 0.25f)
        {
            drive = ForwardSpeed > 0f ? -16f : 16f;
        }
        else if (_reverse && ForwardSpeed > -2.6f)
        {
            drive = -7f;
        }
        else if (_throttle > 0f && ForwardSpeed < cap)
        {
            drive = 14f * _throttle * Mathf.Clamp(_pain?.BikeScale ?? 1f, 0.45f, 1f);
        }

        velocity += forward * drive * step;
        ForwardSpeed = velocity.Dot(forward);
        if (ForwardSpeed > cap)
        {
            velocity -= forward * (ForwardSpeed - cap);
        }

        state.LinearVelocity = velocity;

        Vector3 back = -forward;
        if (back.LengthSquared() > 0.0001f)
        {
            back = back.Normalized();
            Vector3 xAxis = Vector3.Up.Cross(back).Normalized();
            Transform3D upright = state.Transform;
            upright.Basis = new Basis(xAxis, Vector3.Up, back);
            state.Transform = upright;
        }

        float yawTarget = -_steer * YawRate * Mathf.Clamp(Mathf.Abs(ForwardSpeed) / 3f, 0.45f, 1f);
        state.AngularVelocity = new Vector3(0f, yawTarget, 0f);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        float spin = ForwardSpeed * dt / 0.32f;
        _rearWheel?.RotateY(spin);
        _frontWheel?.RotateY(spin);
        if (_model != null)
        {
            Vector3 lean = _model.RotationDegrees;
            lean.Z = Mathf.Lerp(lean.Z, -_steer * 8f, 1f - Mathf.Exp(-8f * dt));
            _model.RotationDegrees = lean;
        }

        if (_brakeLamp != null)
        {
            bool on = _brake > 0.2f;
            _brakeLamp.EmissionEnergyMultiplier = on ? 3f : 0.15f;
        }
    }

    public float ConsumeImpact()
    {
        float value = _impact;
        _impact = 0f;
        return value;
    }

    private void KeyboardInput(bool locked)
    {
        if (locked)
        {
            _throttle = 0f;
            _brake = 1f;
            _reverse = false;
            _steer = 0f;
            return;
        }

        bool forward = Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up);
        bool back = Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down);
        bool left = Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left);
        bool right = Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right);
        bool handbrake = Input.IsPhysicalKeyPressed(Key.Space);
        _throttle = forward ? 1f : 0f;
        _brake = back || handbrake ? 1f : 0f;
        _reverse = back && !forward && !handbrake;
        _steer = 0f;
        if (left)
        {
            _steer -= 1f;
        }

        if (right)
        {
            _steer += 1f;
        }
    }

    private void AutopilotInput(bool locked)
    {
        if (locked)
        {
            _throttle = 0f;
            _brake = 1f;
            _reverse = false;
            _steer = 0f;
            return;
        }

        if (_cargo != null && _cargo.LidOpen01 > 0.28f)
        {
            _cargo.TryShutLid();
        }

        float targetX = Track.LaneX;
        bool obstacle = ObstacleAhead(out float distance);
        if (obstacle && distance < 11f)
        {
            targetX = -1.35f;
        }

        // Positive steer yaws right. Too far right means steer left (negative).
        float xFix = (targetX - GlobalPosition.X) * 0.5f;
        float yawFix = GlobalRotation.Y * 1.8f;
        _steer = Mathf.Clamp(xFix + yawFix, -1f, 1f);
        bool brake = (_events != null && _events.WantsBrake) || (obstacle && distance < 6.5f);
        _throttle = brake ? 0f : 0.82f;
        _brake = brake ? 1f : 0f;
        _reverse = false;
    }

    private bool ObstacleAhead(out float distance)
    {
        distance = 99f;
        if (GetWorld3D() == null)
        {
            return false;
        }

        Vector3 from = GlobalTransform * new Vector3(0f, 0.55f, -1.35f);
        Vector3 forward = -GlobalTransform.Basis.Z;
        forward.Y = 0f;
        if (forward.LengthSquared() < 0.001f)
        {
            return false;
        }

        forward = forward.Normalized();
        var query = PhysicsRayQueryParameters3D.Create(from, from + forward * 12f, Layers.World | Layers.Traffic);
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        query.CollideWithAreas = false;
        Godot.Collections.Dictionary hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0)
        {
            return false;
        }

        Vector3 point = hit["position"].AsVector3();
        distance = from.DistanceTo(point);
        return true;
    }

    private void BuildBody()
    {
        _model = new Node3D { Name = "Model" };
        AddChild(_model);
        Color yellow = new Color("ffc53d");
        Color black = new Color("202226");
        MeshUtil.Box(_model, new Vector3(0.22f, 0.08f, 1.25f), new Vector3(0f, 0.46f, -0.05f), yellow);
        _rearWheel = MeshUtil.Cylinder(_model, 0.32f, 0.14f, new Vector3(0f, 0.32f, 0.42f), black, true);
        _frontWheel = MeshUtil.Cylinder(_model, 0.32f, 0.14f, new Vector3(0f, 0.32f, -0.72f), black, true);
        MeshUtil.Box(_model, new Vector3(0.06f, 0.42f, 0.06f), new Vector3(0f, 0.62f, -0.62f), black);
        MeshUtil.Box(_model, new Vector3(0.52f, 0.04f, 0.05f), new Vector3(0f, 0.84f, -0.64f), black);
        MeshUtil.Box(_model, new Vector3(0.2f, 0.06f, 0.26f), new Vector3(0f, 0.74f, 0.08f), black);
        var headlamp = MeshUtil.Fresh(new Color("fff4d2"), 0.3f);
        headlamp.EmissionEnabled = true;
        headlamp.Emission = new Color("fff1c9");
        headlamp.EmissionEnergyMultiplier = 1.4f;
        MeshUtil.Sphere(_model, 0.06f, new Vector3(0f, 0.58f, -0.78f), headlamp);
        _brakeLamp = MeshUtil.Fresh(new Color("ff5a5a"), 0.4f);
        _brakeLamp.EmissionEnabled = true;
        _brakeLamp.Emission = new Color("ff2a2a");
        _brakeLamp.EmissionEnergyMultiplier = 0.15f;
        MeshUtil.Sphere(_model, 0.045f, new Vector3(-0.12f, 0.55f, 0.58f), _brakeLamp);
        MeshUtil.Sphere(_model, 0.045f, new Vector3(0.12f, 0.55f, 0.58f), _brakeLamp);

        var shape = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(0.62f, 1.08f, 1.35f) },
            Position = new Vector3(0f, 0.54f, -0.08f),
        };
        AddChild(shape);

        var plate = MeshUtil.Box(GetNode("Rack"), new Vector3(0.54f, 0.045f, 0.5f), new Vector3(0f, 0.02f, 0f), yellow);
        plate.Name = "RackPlate";
        MeshUtil.Sign(GetNode("Rack"), "热达", new Vector3(0f, 0.12f, 0f), 0.005f);
    }
}
