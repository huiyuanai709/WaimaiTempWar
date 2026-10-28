using System.Collections.Generic;
using Godot;

namespace DeliveryTemperatureWar;

/// <summary>
/// Crowded noodle counter. The matching ticket is a rigid bag on the counter.
/// People and a queue rail block the straight line, so you squeeze along the wall.
/// </summary>
public partial class PickupShop : Node3D
{
    private enum Step
    {
        Gap,
        Wall,
        Bag,
        Grab,
        Leave,
        Door,
        Bike,
        Mount,
        Done,
    }

    private sealed class CounterBag
    {
        public RigidBody3D Body = null!;
        public int Number;
        public Vector3 Rest;
        public float Tipped;
    }

    private readonly List<CounterBag> _bags = new();
    private readonly List<AnimatableBody3D> _people = new();
    private readonly List<Vector3> _personHome = new();
    private Courier? _courier;
    private EventDirector? _events;
    private TemperatureSim? _temp;
    private DeliveryGame? _game;
    private BikeController? _bike;
    private CargoRig? _cargo;
    private Achievements? _achievements;
    private Step _step = Step.Gap;
    private float _clock;
    private int _called = 12;
    private bool _mounted;
    private float _cutTimer;
    private bool _cutClear;
    private PhaseEvents? _pain;
    private Label3D? _lookalike;

    public bool Claimed { get; private set; }
    public string Status { get; private set; } = "";

    public override void _EnterTree()
    {
        AddToGroup("shop");
    }

    public override void _Ready()
    {
        GlobalPosition = new Vector3(Track.ShopX, 0f, Track.ShopZ);
        _courier = GetNode<Courier>("../Courier");
        _events = GetNode<EventDirector>("../Events");
        _temp = GetNode<TemperatureSim>("../Temperature");
        _game = GetNode<DeliveryGame>("..");
        _bike = GetNode<BikeController>("../Bike");
        _cargo = GetNode<CargoRig>("../Cargo");
        _achievements = GetNode<Achievements>("../Achievements");
        _pain = GetNode<PhaseEvents>("../Events/Pain");
        BuildRoom();
        if (_pain.Has("same_name") && _lookalike != null)
        {
            _lookalike.Text = "17 王";
        }

        if (_pain.Has("door_bikes"))
        {
            MeshUtil.Solid(this, new Vector3(0.7f, 0.7f, 0.36f), new Vector3(-4.15f, 0.35f, 0.9f), new Color("f2c14e"));
            MeshUtil.Solid(this, new Vector3(0.55f, 1.1f, 0.08f), new Vector3(-4.2f, 0.7f, -0.85f), new Color("d64545"));
        }
        _courier.GlobalPosition = ToGlobal(new Vector3(-3.3f, 0.08f, 1.75f));
        _cargo.SetMealLoaded(false);
        GD.Print("SHOP ticket 17");
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _clock += dt;
        int stage = Mathf.FloorToInt(_clock / 2.4f);
        bool cut = _pain != null && _pain.Has("queue_cut");
        _called = cut
            ? stage switch { 0 => 12, 1 => 23, 2 => 19, _ => Track.Ticket }
            : stage switch { 0 => 12, 1 => 23, _ => Track.Ticket };
        RecoverBags(dt);
        PacePeople(dt);
        if (_game == null || _game.Phase != DeliveryGame.RunPhase.Pickup)
        {
            return;
        }

        Status = $"取餐号 {Track.Ticket}    叫号 {_called}\n柜台有三袋，只有 {Track.Ticket} 号是你的热汤面";
        _events?.SetBanner(Prompt());
        if (DeliveryGame.IsAutotest() && !_mounted)
        {
            Autopilot(dt);
        }
    }

    public bool TryInteract(Courier courier)
    {
        if (_game == null || _game.Phase != DeliveryGame.RunPhase.Pickup || _mounted)
        {
            return false;
        }

        if (courier.HasMeal && NearBike())
        {
            Mount();
            return true;
        }

        CounterBag? nearest = NearestBag(out float distance);
        if (nearest == null || distance > 1.45f)
        {
            return courier.HasMeal && NearBike() && MountedNow();
        }

        if (nearest.Number == Track.Ticket)
        {
            if (Claimed)
            {
                return true;
            }

            Claimed = true;
            nearest.Body.Visible = false;
            nearest.Body.CollisionLayer = 0;
            courier.GiveMeal();
            _temp?.StartClock();
            _pain?.NotifyClaimed();
            GD.Print("PICKUP claimed");
            _events?.SetBanner(_called == Track.Ticket
                ? "叫到 17 号，汤面到手"
                : "还没叫到，但标签是 17，你拿对了");
            return true;
        }

        courier.MarkWrong();
        nearest.Tipped = 1.1f;
        nearest.Body.Freeze = false;
        nearest.Body.ApplyImpulse(new Vector3(0.4f, 1.6f, 0.2f));
        GD.Print($"PICKUP wrong {nearest.Number}");
        _events?.SetBanner($"拿错了，这是 {nearest.Number} 号");
        if (courier.HasMeal)
        {
            _cargo?.AddSpill(0.04f);
        }

        return true;
    }

    private bool MountedNow()
    {
        Mount();
        return true;
    }

    private void Mount()
    {
        if (_mounted || _courier == null || _bike == null || _game == null || !_courier.HasMeal)
        {
            return;
        }

        if (_pain != null && _pain.MountLock > 0.05f)
        {
            return;
        }

        _mounted = true;
        _courier.StopSeek();
        _courier.Board();
        _bike.WakeForRide(DeliveryGame.IsAutotest());
        _cargo?.SetMealLoaded(true);
        _game.BeginRide();
        if (!_courier.WrongGrab)
        {
            _achievements?.Unlock("clean_pickup");
        }

        _events?.SetBanner("餐进保温箱 · 保住 60°C");
        GD.Print("MOUNT");
    }

    private void Autopilot(float dt)
    {
        if (_courier == null)
        {
            return;
        }

        Vector3 goal = _step switch
        {
            Step.Gap => LocalPoint(-3.3f, -2.35f),
            Step.Wall => LocalPoint(1.15f, -2.35f),
            Step.Bag => LocalPoint(1.15f, 0.35f),
            Step.Leave => LocalPoint(1.15f, -2.35f),
            Step.Door => LocalPoint(-3.2f, 0f),
            Step.Bike => LocalPoint(-6.4f, 0f),
            Step.Mount => _bike!.GlobalPosition + new Vector3(1.2f, 0f, 0f),
            _ => _courier.GlobalPosition,
        };

        if (_step == Step.Grab)
        {
            _courier.StopSeek();
            TryInteract(_courier);
            if (_courier.HasMeal)
            {
                _step = Step.Leave;
            }

            return;
        }

        if (_step == Step.Mount)
        {
            _courier.Seek(goal);
            if (NearBike())
            {
                _courier.StopSeek();
                Mount();
                _step = Step.Done;
            }

            return;
        }

        if (_step == Step.Door && _courier.Reached(0.4f) && _pain != null && _pain.Has("queue_cut") && !_cutClear)
        {
            _courier.StopSeek();
            _cutTimer += dt;
            if (_cutTimer < 2f)
            {
                return;
            }

            _cutClear = true;
        }

        _courier.Seek(goal);
        if (_courier.Reached(0.4f))
        {
            _step = _step switch
            {
                Step.Gap => Step.Wall,
                Step.Wall => Step.Bag,
                Step.Bag => Step.Grab,
                Step.Leave => Step.Door,
                Step.Door => Step.Bike,
                Step.Bike => Step.Mount,
                _ => _step,
            };
        }
    }

    private string Prompt()
    {
        if (_courier == null)
        {
            return "到店取餐";
        }

        if (_courier.HasMeal)
        {
            if (_pain != null && _pain.MountLock > 0.05f)
            {
                return $"再等两分钟  {_pain.MountLock:0.0}s";
            }

            if (!_cutClear && _pain != null && _pain.Has("queue_cut") && _cutTimer > 0f)
            {
                return "门口挤满了，出不去";
            }

            return NearBike() ? "E 放进保温箱，上车" : "挤出去，汤已经在降温";
        }

        CounterBag? nearest = NearestBag(out float distance);
        if (nearest != null && distance < 1.45f)
        {
            return nearest.Number == Track.Ticket ? "E 拿走 17 号热汤面" : $"E 看清楚，这是 {nearest.Number} 号";
        }

        return "人多。从左边挤到柜台，别拿错袋子";
    }

    private bool NearBike()
    {
        return _courier != null && _bike != null && _courier.GlobalPosition.DistanceTo(_bike.GlobalPosition) < 1.85f;
    }

    private CounterBag? NearestBag(out float distance)
    {
        distance = 999f;
        CounterBag? best = null;
        if (_courier == null)
        {
            return null;
        }

        foreach (CounterBag bag in _bags)
        {
            float d = _courier.GlobalPosition.DistanceTo(bag.Body.GlobalPosition);
            if (d < distance)
            {
                distance = d;
                best = bag;
            }
        }

        return best;
    }

    private void RecoverBags(float dt)
    {
        foreach (CounterBag bag in _bags)
        {
            if (bag.Tipped <= 0f)
            {
                continue;
            }

            bag.Tipped -= dt;
            if (bag.Tipped <= 0f)
            {
                bag.Body.Freeze = true;
                bag.Body.LinearVelocity = Vector3.Zero;
                bag.Body.AngularVelocity = Vector3.Zero;
                bag.Body.GlobalPosition = bag.Rest;
                bag.Body.Rotation = Vector3.Zero;
            }
        }
    }

    private void PacePeople(float dt)
    {
        float wave = Mathf.Sin(_clock * 1.3f) * 0.38f;
        for (int i = 0; i < _people.Count; i++)
        {
            Vector3 home = _personHome[i];
            _people[i].GlobalPosition = ToGlobal(home + new Vector3(wave, 0f, 0f));
        }
    }

    private Vector3 LocalPoint(float x, float z)
    {
        return ToGlobal(new Vector3(x, 0.08f, z));
    }

    private void BuildRoom()
    {
        Color wall = new Color("f3e6d0");
        Color trim = new Color("6b4a32");
        MeshUtil.Solid(this, new Vector3(9.2f, 0.16f, 6.8f), new Vector3(0f, -0.08f, 0f), new Color("d8c3a5"));
        MeshUtil.Solid(this, new Vector3(9.2f, 2.7f, 0.18f), new Vector3(0f, 1.35f, -3.3f), wall);
        MeshUtil.Solid(this, new Vector3(9.2f, 2.7f, 0.18f), new Vector3(0f, 1.35f, 3.3f), wall);
        MeshUtil.Solid(this, new Vector3(0.18f, 2.7f, 6.8f), new Vector3(4.5f, 1.35f, 0f), wall);
        MeshUtil.Solid(this, new Vector3(0.18f, 2.7f, 2.3f), new Vector3(-4.5f, 1.35f, -2.15f), wall);
        MeshUtil.Solid(this, new Vector3(0.18f, 2.7f, 2.3f), new Vector3(-4.5f, 1.35f, 2.15f), wall);
        MeshUtil.Solid(this, new Vector3(3.2f, 0.08f, 0.08f), new Vector3(-0.6f, 0.92f, 1.15f), trim);
        MeshUtil.Solid(this, new Vector3(0.1f, 0.92f, 0.1f), new Vector3(0.95f, 0.46f, 1.15f), trim);
        MeshUtil.Solid(this, new Vector3(0.1f, 0.92f, 0.1f), new Vector3(-2.15f, 0.46f, 1.15f), trim);
        MeshUtil.Solid(this, new Vector3(1.4f, 1.02f, 3.1f), new Vector3(2.7f, 0.51f, 0.35f), new Color("8c5a3c"));
        MeshUtil.Sign(this, "热汤面", new Vector3(2.2f, 2.15f, 0.35f), 0.01f);
        MeshUtil.Sign(this, "从左边挤", new Vector3(-3.4f, 1.8f, -1.4f), 0.006f);
        AddBag(12, new Vector3(1.85f, 1.16f, -0.7f), new Color("ded6c8"));
        AddBag(Track.Ticket, new Vector3(1.85f, 1.16f, 0.35f), new Color("e25b2a"));
        AddBag(23, new Vector3(1.85f, 1.16f, 1.45f), new Color("8d99a6"));
        AddPerson(new Vector3(-0.4f, 0f, 1.85f), new Color("355c7d"));
        AddPerson(new Vector3(0.55f, 0f, 1.7f), new Color("c44536"));
        AddPerson(new Vector3(-1.3f, 0f, 0.55f), new Color("6b4c7a"));
        AddPerson(new Vector3(0.2f, 0f, -0.35f), new Color("2f6f4e"));
    }

    private void AddBag(int number, Vector3 position, Color color)
    {
        var body = new RigidBody3D
        {
            Name = $"Bag{number}",
            Position = position,
            Mass = 0.55f,
            Freeze = true,
            CanSleep = false,
            CollisionLayer = Layers.Cargo,
            CollisionMask = Layers.World,
        };
        MeshUtil.Box(body, new Vector3(0.34f, 0.2f, 0.28f), Vector3.Zero, color);
        Label3D label = MeshUtil.Sign(body, number == Track.Ticket ? "17 汤面" : number.ToString(), new Vector3(0f, 0.22f, 0f), 0.005f);
        if (number == 12)
        {
            _lookalike = label;
        }
        AddChild(body);
        _bags.Add(new CounterBag { Body = body, Number = number, Rest = ToGlobal(position) });
    }

    private void AddPerson(Vector3 local, Color shirt)
    {
        var person = new AnimatableBody3D
        {
            SyncToPhysics = true,
            CollisionLayer = Layers.World,
            CollisionMask = 0,
            Position = local,
        };
        MeshUtil.Box(person, new Vector3(0.46f, 1.15f, 0.36f), new Vector3(0f, 0.95f, 0f), shirt);
        MeshUtil.Box(person, new Vector3(0.26f, 0.24f, 0.24f), new Vector3(0f, 1.68f, 0f), new Color("f0c9a0"));
        person.AddChild(new CollisionShape3D
        {
            Position = new Vector3(0f, 0.9f, 0f),
            Shape = new BoxShape3D { Size = new Vector3(0.5f, 1.7f, 0.42f) },
        });
        AddChild(person);
        _people.Add(person);
        _personHome.Add(local);
    }
}
