using Godot;

namespace DeliveryTemperatureWar;

/// <summary>
/// On-foot body used in the shop and inside the destination building.
/// The street ride still uses the e-bike.
/// </summary>
public partial class Courier : CharacterBody3D
{
    private const float WalkSpeed = 3.5f;
    private const float ClimbSpeed = 2.15f;

    private bool _seeking;
    private Vector3 _goal;
    private float _stumbleCd;
    private MeshInstance3D? _handMeal;

    public bool HasMeal { get; private set; }
    public bool WrongGrab { get; private set; }
    public bool OnFoot { get; private set; } = true;
    public bool Climbing { get; set; }
    public float Stamina { get; private set; } = 1f;
    public float PlanarSpeed { get; private set; }

    public override void _EnterTree()
    {
        AddToGroup("courier");
    }

    public override void _Ready()
    {
        CollisionLayer = Layers.Walker;
        CollisionMask = Layers.World | Layers.Traffic;
        FloorSnapLength = 0.35f;
        FloorMaxAngle = Mathf.DegToRad(56f);
        FloorStopOnSlope = true;
        SafeMargin = 0.06f;
        var shape = new CollisionShape3D
        {
            Position = new Vector3(0f, 0.78f, 0f),
            Shape = new CapsuleShape3D { Radius = 0.28f, Height = 1.5f },
        };
        AddChild(shape);
        var body = new Node3D { Name = "Body" };
        AddChild(body);
        MeshUtil.Box(body, new Vector3(0.42f, 0.7f, 0.28f), new Vector3(0f, 1.05f, 0f), new Color("0f766e"));
        MeshUtil.Box(body, new Vector3(0.28f, 0.22f, 0.24f), new Vector3(0f, 1.55f, 0f), new Color("f0c9a0"));
        MeshUtil.Box(body, new Vector3(0.34f, 0.16f, 0.34f), new Vector3(0f, 1.72f, 0f), new Color("ffc53d"));
        var hand = new Marker3D { Name = "Hand", Position = new Vector3(0.28f, 1.05f, -0.22f) };
        AddChild(hand);
        _handMeal = MeshUtil.Box(hand, new Vector3(0.32f, 0.2f, 0.26f), Vector3.Zero, new Color("e25b2a"));
        _handMeal.Visible = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!OnFoot || DeliveryGame.IsAutotest())
        {
            return;
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.PhysicalKeycode == Key.E)
        {
            Interact();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!OnFoot)
        {
            PlanarSpeed = 0f;
            return;
        }

        float dt = (float)delta;
        Vector3 wish = Vector3.Zero;
        bool auto = DeliveryGame.IsAutotest() && _seeking;
        if (auto)
        {
            Vector3 to = _goal - GlobalPosition;
            to.Y = 0f;
            if (to.Length() > 0.2f)
            {
                wish = to.Normalized();
            }
        }
        else if (!DeliveryGame.IsAutotest())
        {
            wish = KeyboardWish();
        }

        bool exhausted = Climbing && Stamina <= 0.02f;
        if (Climbing && Stamina < 0.28f && wish.LengthSquared() > 0.01f)
        {
            wish = Vector3.Zero;
        }

        var pain = GetTree().GetFirstNodeInGroup("pain") as PhaseEvents;
        float speed = (Climbing ? ClimbSpeed : WalkSpeed) * (pain?.FootScale ?? 1f);
        if (Stamina < 0.2f)
        {
            speed *= 0.55f;
        }

        Vector3 velocity = Velocity;
        Vector3 planar = new Vector3(wish.X, 0f, wish.Z);
        if (planar.LengthSquared() > 0.01f && IsOnFloor())
        {
            Vector3 normal = GetFloorNormal();
            Vector3 along = planar - normal * planar.Dot(normal);
            if (along.LengthSquared() > 0.0001f)
            {
                velocity = along.Normalized() * speed;
            }
        }
        else
        {
            velocity.X = planar.X * speed;
            velocity.Z = planar.Z * speed;
            if (!IsOnFloor())
            {
                velocity.Y -= (Climbing ? 22f : 14f) * dt;
            }
            else if (velocity.Y < 0f)
            {
                velocity.Y = -0.2f;
            }
        }

        Velocity = velocity;
        MoveAndSlide();
        PlanarSpeed = new Vector2(Velocity.X, Velocity.Z).Length();

        if (wish.LengthSquared() > 0.04f)
        {
            LookAt(GlobalPosition + wish, Vector3.Up);
        }

        if (Climbing && PlanarSpeed > 0.35f)
        {
            Stamina = Mathf.Max(0f, Stamina - 0.16f * dt);
        }
        else
        {
            Stamina = Mathf.Min(1f, Stamina + 0.42f * dt);
        }

        if (exhausted && Climbing)
        {
            _stumbleCd -= dt;
            if (_stumbleCd <= 0f)
            {
                _stumbleCd = 0.85f;
                Stumble();
            }
        }
    }

    public void Seek(Vector3 world)
    {
        _goal = world;
        _seeking = true;
    }

    public void StopSeek()
    {
        _seeking = false;
    }

    public bool Reached(float radius)
    {
        Vector3 flat = _goal - GlobalPosition;
        flat.Y = 0f;
        return flat.Length() <= radius;
    }

    public void GiveMeal()
    {
        HasMeal = true;
        if (_handMeal != null)
        {
            _handMeal.Visible = true;
        }
    }

    public void MarkWrong()
    {
        WrongGrab = true;
    }

    public void Board()
    {
        OnFoot = false;
        _seeking = false;
        Visible = false;
        CollisionLayer = 0;
        CollisionMask = 0;
        if (_handMeal != null)
        {
            _handMeal.Visible = false;
        }
    }

    public void AppearAt(Vector3 world)
    {
        GlobalPosition = world;
        Velocity = Vector3.Zero;
        Visible = true;
        OnFoot = true;
        CollisionLayer = Layers.Walker;
        CollisionMask = Layers.World | Layers.Traffic;
        Stamina = 1f;
    }

    public void Interact()
    {
        var shop = GetTree().GetFirstNodeInGroup("shop") as PickupShop;
        var building = GetTree().GetFirstNodeInGroup("building") as BuildingInterior;
        if (shop != null && shop.TryInteract(this))
        {
            return;
        }

        building?.TryInteract(this);
    }

    private void Stumble()
    {
        var cargo = GetTree().GetFirstNodeInGroup("cargo") as CargoRig;
        cargo?.AddSpill(0.035f);
        cargo?.NudgeContents(0.8f);
        var events = GetTree().GetFirstNodeInGroup("events") as EventDirector;
        events?.SetBanner("腿软了，汤晃出来  Stumbled");
        GD.Print("STUMBLE");
    }

    private Vector3 KeyboardWish()
    {
        Camera3D? cam = GetViewport().GetCamera3D();
        Vector3 forward = Vector3.Forward;
        Vector3 right = Vector3.Right;
        if (cam != null)
        {
            forward = -cam.GlobalBasis.Z;
            right = cam.GlobalBasis.X;
        }

        forward.Y = 0f;
        right.Y = 0f;
        if (forward.LengthSquared() < 0.001f)
        {
            forward = Vector3.Forward;
        }

        forward = forward.Normalized();
        right = right.Normalized();
        Vector3 wish = Vector3.Zero;
        if (Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up))
        {
            wish += forward;
        }

        if (Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down))
        {
            wish -= forward;
        }

        if (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left))
        {
            wish -= right;
        }

        if (Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right))
        {
            wish += right;
        }

        return wish.LengthSquared() > 1f ? wish.Normalized() : wish;
    }
}
