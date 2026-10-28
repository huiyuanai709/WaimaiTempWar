using System.Globalization;
using System.Text;
using Godot;

namespace DeliveryTemperatureWar;

public partial class DeliveryGame : Node3D
{
    public enum RunPhase
    {
        Pickup,
        Ride,
        Indoors,
        Done,
    }

    private EventDirector? _events;
    private TemperatureSim? _temp;
    private CargoRig? _cargo;
    private Achievements? _achievements;
    private bool _finishOnce;
    private bool _autotestDone;
    private float _doneHold;
    private float _nextAutotestLog;
    private float _maxSag;
    private float _maxTilt;
    private string _autotestWhy = "";

    public RunPhase Phase { get; private set; } = RunPhase.Pickup;
    public bool Success { get; private set; }
    public string ResultText { get; private set; } = "";
    public float Elapsed { get; private set; }
    public float FinalCelsius { get; private set; }
    public float FinalSpill { get; private set; }
    public float FinalCrush { get; private set; }

    public static bool IsAutotest()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--autotest")
            {
                return true;
            }
        }

        foreach (string arg in OS.GetCmdlineArgs())
        {
            if (arg == "--autotest")
            {
                return true;
            }
        }

        return OS.GetEnvironment("WAIMAI_AUTOTEST") == "1";
    }

    public override void _EnterTree()
    {
        AddToGroup("game");
    }

    public override void _Ready()
    {
        SetupAtmosphere();
        _events = GetNode<EventDirector>("Events");
        _temp = GetNode<TemperatureSim>("Temperature");
        _cargo = GetNode<CargoRig>("Cargo");
        _achievements = GetNode<Achievements>("Achievements");
        if (IsAutotest())
        {
            GD.Print("AUTOTEST on");
        }
    }

    public void BeginRide()
    {
        Phase = RunPhase.Ride;
        GetNodeOrNull<PhaseEvents>("Events/Pain")?.OnPhase(RunPhase.Ride);
    }

    public void BeginIndoors()
    {
        Phase = RunPhase.Indoors;
        GetNodeOrNull<PhaseEvents>("Events/Pain")?.OnPhase(RunPhase.Indoors);
    }

    public void FinishDelivery()
    {
        Finish();
    }

    public override void _Process(double delta)
    {
        if (Phase != RunPhase.Done)
        {
            Elapsed += (float)delta;
        }

        if (_cargo != null)
        {
            _maxSag = Mathf.Max(_maxSag, _cargo.BagSag);
            _maxTilt = Mathf.Max(_maxTilt, _cargo.FoodTilt);
        }

        if (!IsAutotest() || _autotestDone)
        {
            return;
        }

        if (Elapsed > _nextAutotestLog)
        {
            _nextAutotestLog = Elapsed + 5f;
            var logBike = GetNode<BikeController>("Bike");
            GD.Print($"TICK t={Elapsed:0.00} pos={logBike.GlobalPosition} vel={logBike.LinearVelocity} yaw={logBike.Rotation.Y:0.00} steer={logBike.Steer:0.00} thr={logBike.Throttle:0.00} brk={logBike.Brake:0.00} spd={logBike.ForwardSpeed:0.00}");
        }

        var bike = GetNode<BikeController>("Bike");
        var walker = GetNodeOrNull<Courier>("Courier");
        if (walker != null && walker.OnFoot && walker.GlobalPosition.Y < -2f)
        {
            Conclude(false, "courier fell");
            return;
        }

        if (bike.GlobalPosition.Y < -1.2f)
        {
            Conclude(false, "bike fell");
            return;
        }

        if (Elapsed > 110f && Phase != RunPhase.Done)
        {
            Conclude(false, "timeout");
            return;
        }

        if (Phase == RunPhase.Done)
        {
            if (DisplayServer.GetName() != "headless")
            {
                _doneHold += (float)delta;
                if (_doneHold < 8f)
                {
                    return;
                }
            }

            var shop = GetNodeOrNull<PickupShop>("Shop");
            var building = GetNodeOrNull<BuildingInterior>("Building");
            var pain = GetNodeOrNull<PhaseEvents>("Events/Pain");
            bool indoor = building != null && building.Finished && _achievements != null && (
                building.UsedStairs
                    ? _achievements.IsUnlocked("stair_hot")
                    : _achievements.IsUnlocked("elevator_clean"));
            bool pool = pain != null
                && pain.Has("queue_cut")
                && pain.Has("same_name")
                && pain.Has("puddle")
                && pain.Has("wet_brake")
                && pain.Has("full_car");
            bool ok = Success
                && _achievements != null
                && _achievements.IsUnlocked("hot_delivery")
                && _achievements.IsUnlocked("clean_pickup")
                && _achievements.IsUnlocked("pain_hot")
                && _events != null
                && _events.DistinctCount >= 3
                && _cargo != null
                && _cargo.CoolerLive
                && _maxSag > 0.02f
                && _maxTilt > 0.05f
                && shop != null
                && shop.Claimed
                && indoor
                && pool
                && FileHas("hot_delivery")
                && FileHas("clean_pickup");
            Conclude(ok, ok ? "ok" : _autotestWhy.Length == 0 ? "checks failed" : _autotestWhy);
        }
    }

    private void Finish()
    {
        if (_finishOnce || _temp == null || _cargo == null || _achievements == null || _events == null)
        {
            return;
        }

        _finishOnce = true;
        Phase = RunPhase.Done;
        _temp.Paused = true;
        FinalCelsius = _temp.Celsius;
        FinalSpill = _cargo.Spill01;
        FinalCrush = _cargo.Crush01;
        Success = FinalCelsius >= Track.TargetCelsius && FinalSpill < Track.SpillFail;
        var building = GetNodeOrNull<BuildingInterior>("Building");
        var pain = GetNodeOrNull<PhaseEvents>("Events/Pain");
        if (Success)
        {
            _achievements.Unlock("hot_delivery");
            if (FinalSpill < 0.01f)
            {
                _achievements.Unlock("zero_spill");
            }

            if (pain != null && pain.Count >= 3)
            {
                _achievements.Unlock("pain_hot");
            }
        }

        if (building != null && building.UsedElevator && FinalSpill < 0.01f)
        {
            _achievements.Unlock("elevator_clean");
        }

        if (building != null && building.UsedStairs && FinalCelsius >= Track.TargetCelsius)
        {
            _achievements.Unlock("stair_hot");
        }

        if (_events.Saw(DeliveryEvent.RainGust) && FinalSpill < 0.01f)
        {
            _achievements.Unlock("rain_dry");
        }

        string heat = FinalCelsius >= 75f
            ? "滚烫，准时热乎。"
            : FinalCelsius >= 68f
                ? "还烫，顾客会满意。"
                : FinalCelsius >= Track.TargetCelsius
                    ? "刚好够热，再慢就凉了。"
                    : "低于 60°C，这单凉了。";
        string spill = FinalSpill >= Track.SpillFail ? "汤洒了，盒子空了一半。" : "";
        string how = building != null && building.UsedStairs ? "楼梯送到 302。" : "电梯送到 302。";
        string title = Success ? "送达成功  Delivered hot" : "送达失败  Order ruined";
        var body = new StringBuilder();
        body.AppendLine(title);
        body.AppendLine($"温度 {FinalCelsius:0.0}°C    洒出 {FinalSpill * 100f:0}%    压损 {FinalCrush * 100f:0}%");
        body.AppendLine(heat);
        body.AppendLine(how);
        if (pain != null)
        {
            body.AppendLine($"路况事件 {pain.Count}  {pain.FiredLine()}");
        }
        if (spill.Length > 0)
        {
            body.AppendLine(spill);
        }

        body.Append("成就  ");
        bool any = false;
        foreach (AchievementEntry entry in Achievements.Catalog)
        {
            if (_achievements.IsUnlocked(entry.Id))
            {
                if (any)
                {
                    body.Append(" · ");
                }

                body.Append(entry.NameZh);
                any = true;
            }
        }

        if (!any)
        {
            body.Append("这单还没解锁");
        }

        ResultText = body.ToString();
        GD.Print($"RESULT success={Success} temp={FinalCelsius:0.0} spill={FinalSpill:0.00} crush={FinalCrush:0.00} events={_events.DistinctCount}");
    }

    private void Conclude(bool ok, string why)
    {
        if (_autotestDone)
        {
            return;
        }

        _autotestDone = true;
        _autotestWhy = why;
        var bike = GetNode<BikeController>("Bike");
        string line = string.Format(
            CultureInfo.InvariantCulture,
            "AUTOTEST_JSON {{\"ok\":{0},\"why\":\"{1}\",\"temp\":{2:0.00},\"spill\":{3:0.000},\"crush\":{4:0.000},\"sag\":{5:0.000},\"tilt\":{6:0.000},\"events\":{7},\"cooler\":{8},\"soft\":{9},\"pool\":\"{10}\",\"indoor\":\"{11}\",\"pos\":\"{12:0.0},{13:0.0},{14:0.0}\"}}",
            ok ? "true" : "false",
            why,
            _temp?.Celsius ?? -1f,
            _cargo?.Spill01 ?? -1f,
            _cargo?.Crush01 ?? -1f,
            _maxSag,
            _maxTilt,
            _events?.DistinctCount ?? 0,
            _cargo?.CoolerLive == true ? "true" : "false",
            _cargo?.SoftBodyActive == true ? "true" : "false",
            GetNodeOrNull<PhaseEvents>("Events/Pain")?.FiredLine() ?? "",
            GetNodeOrNull<BuildingInterior>("Building")?.UsedStairs == true ? "stairs" : "elevator",
            bike.GlobalPosition.X,
            bike.GlobalPosition.Y,
            bike.GlobalPosition.Z);
        GD.Print(line);
        using Godot.FileAccess file = Godot.FileAccess.Open("/tmp/waimai-autotest.txt", Godot.FileAccess.ModeFlags.Write);
        file?.StoreString(line + "\n");
        GetTree().Quit(ok ? 0 : 1);
    }

    private static bool FileHas(string id)
    {
        var cfg = new ConfigFile();
        if (cfg.Load(Achievements.FilePath) != Error.Ok)
        {
            return false;
        }

        return cfg.GetValue("achievements", id, false).AsBool();
    }

    private void SetupAtmosphere()
    {
        var world = GetNode<WorldEnvironment>("WorldEnvironment");
        var skyMat = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color("6ea8dc"),
            SkyHorizonColor = new Color("f3c9a2"),
            GroundBottomColor = new Color("3e4a3c"),
            GroundHorizonColor = new Color("e0c09a"),
            SunAngleMax = 35f,
        };
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = skyMat },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 0.9f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            FogEnabled = true,
            FogLightColor = new Color("d5e2ee"),
            FogDensity = 0.0035f,
            FogAerialPerspective = 0.35f,
        };
        world.Environment = env;
        var sun = GetNode<DirectionalLight3D>("Sun");
        sun.RotationDegrees = new Vector3(-46f, 32f, 0f);
        sun.LightColor = new Color("fff0dc");
        sun.LightEnergy = 1.2f;
        sun.ShadowEnabled = true;
        sun.DirectionalShadowMaxDistance = 70f;
    }
}
