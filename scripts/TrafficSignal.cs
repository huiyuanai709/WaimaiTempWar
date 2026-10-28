using Godot;

namespace DeliveryTemperatureWar;

public partial class TrafficSignal : Node3D
{
    private MeshInstance3D _red = null!;
    private MeshInstance3D _yellow = null!;
    private MeshInstance3D _green = null!;
    private StandardMaterial3D _redMat = null!;
    private StandardMaterial3D _yellowMat = null!;
    private StandardMaterial3D _greenMat = null!;
    private float _cycle;
    private float _forced;
    private readonly bool _inverted;

    public bool MainIsRed { get; private set; }

    public TrafficSignal(bool inverted)
    {
        _inverted = inverted;
    }

    public TrafficSignal()
    {
    }

    public override void _Ready()
    {
        MeshUtil.Cylinder(this, 0.07f, 3.3f, new Vector3(0f, 1.65f, 0f), new Color("2a2e32"), false);
        var head = new Node3D { Position = new Vector3(0f, 3.2f, 0.22f) };
        AddChild(head);
        MeshUtil.Box(head, new Vector3(0.38f, 1.15f, 0.28f), Vector3.Zero, new Color("1a1d20"));

        _redMat = LampMat(new Color("ff3b30"));
        _yellowMat = LampMat(new Color("ffd60a"));
        _greenMat = LampMat(new Color("34c759"));
        _red = MeshUtil.Sphere(head, 0.14f, new Vector3(0f, 0.36f, 0.12f), _redMat);
        _yellow = MeshUtil.Sphere(head, 0.14f, new Vector3(0f, 0f, 0.12f), _yellowMat);
        _green = MeshUtil.Sphere(head, 0.14f, new Vector3(0f, -0.36f, 0.12f), _greenMat);
        Show(false, false, true);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_forced > 0f)
        {
            _forced -= dt;
            bool red = !_inverted;
            Show(red, false, !red);
            MainIsRed = red;
            return;
        }

        _cycle += dt;
        float m = _cycle % 16f;
        bool mainRed = m >= 9.2f;
        bool mainYellow = m >= 8f && m < 9.2f;
        bool mainGreen = m < 8f;
        if (_inverted)
        {
            Show(!mainRed && !mainYellow, mainYellow, mainRed);
            MainIsRed = !mainRed;
        }
        else
        {
            Show(mainRed, mainYellow, mainGreen);
            MainIsRed = mainRed;
        }
    }

    public void ForceRed(float seconds)
    {
        _forced = Mathf.Max(_forced, seconds);
    }

    private void Show(bool red, bool yellow, bool green)
    {
        Paint(_redMat, new Color("ff3b30"), red);
        Paint(_yellowMat, new Color("ffd60a"), yellow);
        Paint(_greenMat, new Color("34c759"), green);
    }

    private static StandardMaterial3D LampMat(Color color)
    {
        var mat = MeshUtil.Fresh(color.Darkened(0.55f), 0.35f);
        mat.EmissionEnabled = true;
        mat.Emission = color;
        mat.EmissionEnergyMultiplier = 0.15f;
        return mat;
    }

    private static void Paint(StandardMaterial3D mat, Color color, bool on)
    {
        mat.AlbedoColor = on ? color : color.Darkened(0.62f);
        mat.Emission = color;
        mat.EmissionEnergyMultiplier = on ? 3.4f : 0.05f;
    }
}
