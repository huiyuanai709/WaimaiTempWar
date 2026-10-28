using Godot;

namespace DeliveryTemperatureWar;

public partial class Hud : CanvasLayer
{
    private Label _title = null!;
    private Label _temp = null!;
    private Label _stats = null!;
    private Label _route = null!;
    private Label _banner = null!;
    private Label _hint = null!;
    private Label _toast = null!;
    private Label _result = null!;
    private ColorRect _fill = null!;
    private PanelContainer _resultPanel = null!;
    private PanelContainer _achPanel = null!;
    private VBoxContainer _achList = null!;
    private float _toastTime;
    private string _toastText = "";
    private bool _showAchievements;
    private DeliveryGame? _game;
    private TemperatureSim? _tempSim;
    private CargoRig? _cargo;
    private BikeController? _bike;
    private EventDirector? _events;
    private Achievements? _achievements;

    public override void _Ready()
    {
        Layer = 20;
        Build();
        _achievements = GetNode<Achievements>("../Achievements");
        _achievements.Unlocked += OnUnlocked;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
        {
            return;
        }

        if (key.PhysicalKeycode == Key.R)
        {
            GetTree().ReloadCurrentScene();
            GetViewport().SetInputAsHandled();
        }
        else if (key.PhysicalKeycode == Key.Tab)
        {
            _showAchievements = !_showAchievements;
            GetViewport().SetInputAsHandled();
        }
        else if (key.PhysicalKeycode == Key.F9 && _achievements != null)
        {
            _achievements.Reset();
            _toastText = "成就已清空  Achievements reset";
            _toastTime = 2.4f;
        }
        else if (key.PhysicalKeycode == Key.F)
        {
            Cargo()?.TryShutLid();
        }
        else if (key.PhysicalKeycode == Key.Key1)
        {
            ChooseRider(RiderKind.FatGuga);
        }
        else if (key.PhysicalKeycode == Key.Key2)
        {
            ChooseRider(RiderKind.ThinGuga);
        }
        else if (key.PhysicalKeycode == Key.Key3)
        {
            ChooseRider(RiderKind.Doro);
        }
        else if (key.PhysicalKeycode == Key.Q)
        {
            RiderSkins.Cycle();
            _toastText = $"骑手  {RiderSkins.Label(RiderSkins.Current)}";
            _toastTime = 1.8f;
            GetViewport().SetInputAsHandled();
        }
    }

    private void ChooseRider(RiderKind kind)
    {
        RiderSkins.Select(kind);
        _toastText = $"骑手  {RiderSkins.Label(kind)}";
        _toastTime = 1.8f;
        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _game ??= GetNode<DeliveryGame>("..");
        _tempSim ??= GetNode<TemperatureSim>("../Temperature");
        _cargo ??= GetNode<CargoRig>("../Cargo");
        _bike ??= GetNode<BikeController>("../Bike");
        _events ??= GetNode<EventDirector>("../Events");
        _achievements ??= GetNode<Achievements>("../Achievements");

        float celsius = _tempSim.Celsius;
        _temp.Text = $"温度  {celsius:0.0}°C";
        _temp.Modulate = celsius >= 70f
            ? new Color("8dffb0")
            : celsius >= Track.TargetCelsius
                ? new Color("ffd36b")
                : new Color("ff8d8d");
        _fill.AnchorRight = Mathf.Clamp(celsius / 100f, 0.02f, 1f);
        _fill.Color = _temp.Modulate;

        var courier = GetNodeOrNull<Courier>("../Courier");
        var shop = GetNodeOrNull<PickupShop>("../Shop");
        var building = GetNodeOrNull<BuildingInterior>("../Building");
        var pain = GetNodeOrNull<PhaseEvents>("../Events/Pain");
        float spill = _cargo.Spill01 * 100f;
        float crush = _cargo.Crush01 * 100f;
        float speed = courier != null && courier.OnFoot
            ? courier.PlanarSpeed * 3.6f
            : Mathf.Abs(_bike.ForwardSpeed) * 3.6f;
        float eta = Eta();
        string lid = _cargo.LidOpen01 > 0.25f ? "    盖子开着  Press F" : "";
        string stamina = _game.Phase == DeliveryGame.RunPhase.Indoors && building != null && building.Stairs
            ? $"    体力 {courier?.Stamina * 100f:0}%"
            : "";
        _stats.Text = $"洒出 {spill:0}%    压损 {crush:0}%    预计 {eta:0}s    {speed:0} km/h{lid}{stamina}";
        if (_game.Phase == DeliveryGame.RunPhase.Pickup && shop != null)
        {
            _route.Text = shop.Status;
        }
        else if (_game.Phase == DeliveryGame.RunPhase.Indoors && building != null)
        {
            _route.Text = building.Status + "\n" + _events.ArmedLine;
        }
        else
        {
            _route.Text = $"订单  热汤面 Hot noodle soup    目标 ≥ {Track.TargetCelsius:0}°C\n{_events.ArmedLine}";
        }

        _hint.Text = _game.Phase switch
        {
            DeliveryGame.RunPhase.Pickup => "W A S D 走动    E 取餐 / 上车    1 胖咕嘎  2 瘦咕嘎  3 Doro",
            DeliveryGame.RunPhase.Indoors => "W A S D 走动    E 电梯 / 送达    1/2/3 换骑手    Q 轮换",
            _ => "W 油门    A D 转向    1 胖咕嘎  2 瘦咕嘎  3 Doro    Q 轮换",
        };

        string banner = _events.Banner;
        if (string.IsNullOrEmpty(banner) && _game.Phase == DeliveryGame.RunPhase.Ride)
        {
            if (_game.Elapsed < 3.2f)
            {
                banner = "取餐完成 · 保住 60°C 以上";
            }
            else if (celsius < Track.TargetCelsius)
            {
                banner = "已经低于 60°C";
            }
            else if (celsius < 64f)
            {
                banner = "快凉了 — 抓紧送到";
            }
        }

        _banner.Text = banner;
        _banner.Visible = banner.Length > 0;

        if (_toastTime > 0f)
        {
            _toastTime -= dt;
            _toast.Text = _toastText;
            _toast.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(_toastTime, 0f, 1f));
        }
        else if (pain != null && pain.ToastLeft > 0f)
        {
            _toast.Text = pain.Toast;
            _toast.Modulate = Colors.White;
        }
        else
        {
            _toast.Text = "";
        }

        bool done = _game.Phase == DeliveryGame.RunPhase.Done;
        _resultPanel.Visible = done;
        if (done)
        {
            _result.Text = _game.ResultText;
        }

        _achPanel.Visible = _showAchievements;
        if (_showAchievements)
        {
            RefreshAchievements();
        }

    }

    private CargoRig Cargo()
    {
        _cargo ??= GetNode<CargoRig>("../Cargo");
        return _cargo;
    }

    private float Eta()
    {
        if (_bike == null || _events == null)
        {
            return 0f;
        }

        float dx = _bike.GlobalPosition.X - Track.DeliveryX;
        float dz = _bike.GlobalPosition.Z - Track.DeliveryZ;
        float distance = Mathf.Sqrt(dx * dx + dz * dz);
        return distance / 7.2f + _events.HoldRemaining;
    }

    private void OnUnlocked(string id)
    {
        foreach (AchievementEntry entry in Achievements.Catalog)
        {
            if (entry.Id != id)
            {
                continue;
            }

            _toastText = $"成就解锁  {entry.NameZh}  {entry.NameEn}";
            _toastTime = 3.2f;
            return;
        }
    }

    private void RefreshAchievements()
    {
        foreach (Node child in _achList.GetChildren())
        {
            child.QueueFree();
        }

        foreach (AchievementEntry entry in Achievements.Catalog)
        {
            bool on = _achievements != null && _achievements.IsUnlocked(entry.Id);
            string mark = on ? "已解锁" : "未解锁";
            var row = MakeLabel($"{mark}   {entry.NameZh}  {entry.NameEn}\n{entry.Detail}", 16);
            row.Modulate = on ? new Color("b8ffd0") : new Color("c9c9c9");
            row.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _achList.AddChild(row);
        }

        var note = MakeLabel("保存在 user://achievements.cfg    F9 清空", 14);
        note.Modulate = new Color("9aa4ad");
        _achList.AddChild(note);
    }

    private void Build()
    {
        var card = new PanelContainer
        {
            Position = new Vector2(16, 16),
            CustomMinimumSize = new Vector2(420, 0),
        };
        card.AddThemeStyleboxOverride("panel", PanelStyle(0.78f));
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        card.AddChild(box);
        AddChild(card);

        _title = MakeLabel("外卖温度战争", 24);
        box.AddChild(_title);
        box.AddChild(MakeLabel("Delivery Temperature War", 14));
        _temp = MakeLabel("温度", 32);
        box.AddChild(_temp);

        var bar = new Control { CustomMinimumSize = new Vector2(0, 16) };
        var back = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.35f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        back.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _fill = new ColorRect
        {
            Color = new Color("ffd36b"),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorRight = 0.8f,
            AnchorBottom = 1f,
        };
        var marker = new ColorRect
        {
            Color = Colors.White,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 0.6f,
            AnchorRight = 0.6f,
            AnchorBottom = 1f,
            OffsetLeft = -1f,
            OffsetRight = 1f,
        };
        bar.AddChild(back);
        bar.AddChild(_fill);
        bar.AddChild(marker);
        box.AddChild(bar);

        _stats = MakeLabel("洒出", 16);
        _route = MakeLabel("订单", 15);
        _route.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(_stats);
        box.AddChild(_route);

        _banner = MakeLabel("", 26);
        _banner.HorizontalAlignment = HorizontalAlignment.Center;
        _banner.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _banner.OffsetLeft = -420;
        _banner.OffsetRight = 420;
        _banner.OffsetTop = 18;
        _banner.OffsetBottom = 70;
        AddChild(_banner);

        _hint = MakeLabel("W 油门    S / Space 刹车    A D 转向    F 拍盖    Tab 成就    R 重开", 15);
        _hint.HorizontalAlignment = HorizontalAlignment.Center;
        _hint.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _hint.OffsetLeft = -460;
        _hint.OffsetRight = 460;
        _hint.OffsetTop = -42;
        _hint.OffsetBottom = -14;
        AddChild(_hint);

        _toast = MakeLabel("", 18);
        _toast.HorizontalAlignment = HorizontalAlignment.Center;
        _toast.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _toast.OffsetLeft = -420;
        _toast.OffsetRight = 420;
        _toast.OffsetTop = -88;
        _toast.OffsetBottom = -48;
        AddChild(_toast);

        _resultPanel = new PanelContainer();
        _resultPanel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _resultPanel.OffsetLeft = -280;
        _resultPanel.OffsetRight = 280;
        _resultPanel.OffsetTop = -170;
        _resultPanel.OffsetBottom = 170;
        _resultPanel.AddThemeStyleboxOverride("panel", PanelStyle(0.92f));
        _resultPanel.Visible = false;
        var resultBox = new VBoxContainer();
        resultBox.AddThemeConstantOverride("separation", 10);
        _resultPanel.AddChild(resultBox);
        _result = MakeLabel("", 20);
        _result.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        resultBox.AddChild(_result);
        var again = new Button { Text = "再送一单    R", CustomMinimumSize = new Vector2(0, 42) };
        again.FocusMode = Control.FocusModeEnum.None;
        FontFile? font = GameFonts.Ui;
        if (font != null)
        {
            again.AddThemeFontOverride("font", font);
        }

        again.Pressed += () => GetTree().ReloadCurrentScene();
        resultBox.AddChild(again);
        AddChild(_resultPanel);

        _achPanel = new PanelContainer();
        _achPanel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _achPanel.OffsetLeft = -340;
        _achPanel.OffsetRight = 340;
        _achPanel.OffsetTop = -220;
        _achPanel.OffsetBottom = 220;
        _achPanel.AddThemeStyleboxOverride("panel", PanelStyle(0.94f));
        _achPanel.Visible = false;
        _achList = new VBoxContainer();
        _achList.AddThemeConstantOverride("separation", 8);
        _achPanel.AddChild(_achList);
        AddChild(_achPanel);
    }

    private Label MakeLabel(string text, int size)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", Colors.White);
        FontFile? font = GameFonts.Ui;
        if (font != null)
        {
            label.AddThemeFontOverride("font", font);
        }

        return label;
    }

    private static StyleBoxFlat PanelStyle(float alpha)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.07f, 0.09f, alpha),
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 12,
            ContentMarginBottom = 12,
        };
        style.SetCornerRadiusAll(12);
        return style;
    }
}
