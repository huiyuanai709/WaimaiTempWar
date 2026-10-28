using Godot;

namespace DeliveryTemperatureWar;

/// <summary>
/// Original low-poly courier mascots built from primitives. The silhouettes
/// nod at internet penguin and round-critter memes. They are not ports of any
/// game's mesh, texture, or character model.
/// </summary>
public enum RiderKind
{
    FatGuga,
    ThinGuga,
    Doro,
}

public static class RiderSkins
{
    private const string Path = "user://rider.cfg";
    private static bool _loaded;

    public static RiderKind Current { get; private set; } = RiderKind.FatGuga;

    public static event System.Action? Changed;

    public static string Label(RiderKind kind)
    {
        return kind switch
        {
            RiderKind.FatGuga => "胖咕嘎",
            RiderKind.ThinGuga => "瘦咕嘎",
            RiderKind.Doro => "Doro",
            _ => kind.ToString(),
        };
    }

    public static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        Current = DeliveryGame.IsAutotest() ? RiderKind.FatGuga : Read();
        GD.Print($"RIDER_SKIN={Current}");
        if (DeliveryGame.IsAutotest())
        {
            GD.Print(
                "RIDER_BUILT "
                + $"FatGuga={CountMeshes(RiderKind.FatGuga)} "
                + $"ThinGuga={CountMeshes(RiderKind.ThinGuga)} "
                + $"Doro={CountMeshes(RiderKind.Doro)}");
        }
    }

    public static void Select(RiderKind kind)
    {
        EnsureLoaded();
        if (Current == kind)
        {
            GD.Print($"RIDER_SKIN={Current}");
            return;
        }

        Current = kind;
        if (!DeliveryGame.IsAutotest())
        {
            Write(kind);
        }

        GD.Print($"RIDER_SKIN={Current}");
        Changed?.Invoke();
    }

    public static void Cycle()
    {
        EnsureLoaded();
        int next = ((int)Current + 1) % 3;
        Select((RiderKind)next);
    }

    private static int CountMeshes(RiderKind kind)
    {
        Node3D built = RiderLooks.Build(kind, false);
        int count = 0;
        Count(built, ref count);
        built.Free();
        return count;
    }

    private static void Count(Node node, ref int count)
    {
        if (node is MeshInstance3D)
        {
            count++;
        }

        foreach (Node child in node.GetChildren())
        {
            Count(child, ref count);
        }
    }

    private static RiderKind Read()
    {
        var file = new ConfigFile();
        if (file.Load(Path) != Error.Ok)
        {
            return RiderKind.FatGuga;
        }

        string name = file.GetValue("rider", "skin", "FatGuga").AsString();
        return name switch
        {
            "ThinGuga" => RiderKind.ThinGuga,
            "Doro" => RiderKind.Doro,
            _ => RiderKind.FatGuga,
        };
    }

    private static void Write(RiderKind kind)
    {
        var file = new ConfigFile();
        file.SetValue("rider", "skin", kind.ToString());
        file.Save(Path);
    }
}

public static class RiderLooks
{
    public static Vector3 MealHand(RiderKind kind)
    {
        return kind switch
        {
            RiderKind.ThinGuga => new Vector3(0.2f, 0.92f, -0.16f),
            RiderKind.Doro => new Vector3(0.3f, 0.58f, -0.16f),
            _ => new Vector3(0.38f, 0.62f, -0.18f),
        };
    }

    public static Node3D Build(RiderKind kind, bool sitting, float scale = 1f)
    {
        var root = new Node3D { Name = kind.ToString() };
        switch (kind)
        {
            case RiderKind.ThinGuga:
                BuildThin(root, sitting);
                break;
            case RiderKind.Doro:
                BuildDoro(root, sitting);
                break;
            default:
                BuildFat(root, sitting);
                break;
        }

        root.Scale = Vector3.One * scale;
        return root;
    }

    private static void BuildFat(Node3D root, bool sitting)
    {
        float bodyY = sitting ? 0.34f : 0.5f;
        float headY = sitting ? 0.7f : 0.9f;
        float footZ = sitting ? -0.28f : 0.02f;
        Color black = new Color("1b1c1f");
        Color white = new Color("f6f3ec");
        Color yellow = new Color("f2c14e");
        Ball(root, 0.36f, new Vector3(0f, bodyY, 0f), black, new Vector3(1.18f, 0.96f, 1.05f));
        Ball(root, 0.22f, new Vector3(0f, bodyY - 0.02f, -0.14f), white, new Vector3(0.92f, 1.05f, 0.5f));
        Ball(root, 0.24f, new Vector3(0f, headY, -0.02f), black, new Vector3(1.05f, 0.92f, 1f));
        Ball(root, 0.11f, new Vector3(0f, headY - 0.04f, -0.16f), white, new Vector3(1f, 0.85f, 0.55f));
        MeshUtil.Box(root, new Vector3(0.11f, 0.05f, 0.12f), new Vector3(0f, headY - 0.08f, -0.3f), yellow);
        Eye(root, new Vector3(-0.09f, headY + 0.04f, -0.22f));
        Eye(root, new Vector3(0.09f, headY + 0.04f, -0.22f));
        MeshUtil.Box(root, new Vector3(0.24f, 0.05f, 0.03f), new Vector3(0f, headY + 0.03f, -0.2f), new Color("2a3140"));
        Foot(root, -0.12f, footZ, yellow);
        Foot(root, 0.12f, footZ, yellow);
        Flipper(root, -1f, bodyY + 0.02f, sitting, black);
        Flipper(root, 1f, bodyY + 0.02f, sitting, black);
    }

    private static void BuildThin(Node3D root, bool sitting)
    {
        Color black = new Color("1b1c1f");
        Color white = new Color("f6f3ec");
        Color yellow = new Color("f2c14e");
        float lean = sitting ? -0.12f : 0f;
        MeshUtil.Box(root, new Vector3(0.07f, 0.34f, 0.07f), new Vector3(-0.07f, sitting ? 0.12f : 0.18f, sitting ? -0.16f : 0.02f), black);
        MeshUtil.Box(root, new Vector3(0.07f, 0.34f, 0.07f), new Vector3(0.07f, sitting ? 0.12f : 0.18f, sitting ? -0.16f : 0.02f), black);
        Foot(root, -0.07f, sitting ? -0.28f : 0.04f, yellow);
        Foot(root, 0.07f, sitting ? -0.28f : 0.04f, yellow);
        Ball(root, 0.16f, new Vector3(0f, sitting ? 0.48f : 0.62f, lean), black, new Vector3(0.72f, 2.15f, 0.62f));
        Ball(root, 0.1f, new Vector3(0f, sitting ? 0.46f : 0.58f, lean - 0.08f), white, new Vector3(0.7f, 1.3f, 0.4f));
        Ball(root, 0.07f, new Vector3(0f, sitting ? 0.86f : 1.12f, lean), black, Vector3.One);
        Ball(root, 0.15f, new Vector3(0f, sitting ? 1.02f : 1.32f, lean - 0.02f), black, new Vector3(0.92f, 1.15f, 0.9f));
        MeshUtil.Box(root, new Vector3(0.08f, 0.04f, 0.1f), new Vector3(0f, sitting ? 0.98f : 1.28f, lean - 0.18f), yellow);
        Eye(root, new Vector3(-0.06f, sitting ? 1.08f : 1.38f, lean - 0.14f), 0.7f);
        Eye(root, new Vector3(0.06f, sitting ? 1.08f : 1.38f, lean - 0.14f), 0.7f);
        Flipper(root, -1f, sitting ? 0.62f : 0.78f, sitting, black, 0.72f);
        Flipper(root, 1f, sitting ? 0.62f : 0.78f, sitting, black, 0.72f);
    }

    private static void BuildDoro(Node3D root, bool sitting)
    {
        Color pink = new Color("f3b7c8");
        Color cream = new Color("fff1e0");
        Color hair = new Color("e6c56a");
        Color ear = new Color("f7c5d4");
        Color inner = new Color("e89aaf");
        float bodyY = sitting ? 0.32f : 0.46f;
        float headY = sitting ? 0.68f : 0.9f;
        Ball(root, 0.34f, new Vector3(0f, bodyY, 0f), pink, new Vector3(1.12f, 0.92f, 1f));
        Ball(root, 0.18f, new Vector3(0f, bodyY + 0.02f, -0.14f), cream, new Vector3(0.9f, 0.8f, 0.48f));
        Ball(root, 0.26f, new Vector3(0f, headY, 0f), cream, new Vector3(1.05f, 0.96f, 1f));
        Ball(root, 0.28f, new Vector3(0f, headY + 0.12f, 0.06f), hair, new Vector3(1.18f, 0.58f, 1.02f));
        Ball(root, 0.1f, new Vector3(-0.18f, headY - 0.08f, 0.02f), hair, Vector3.One);
        Ball(root, 0.1f, new Vector3(0.18f, headY - 0.08f, 0.02f), hair, Vector3.One);
        Ear(root, -1f, headY, ear, inner);
        Ear(root, 1f, headY, ear, inner);
        Eye(root, new Vector3(-0.08f, headY + 0.02f, -0.22f), 0.85f);
        Eye(root, new Vector3(0.08f, headY + 0.02f, -0.22f), 0.85f);
        MeshUtil.Box(root, new Vector3(0.06f, 0.02f, 0.02f), new Vector3(0f, headY - 0.06f, -0.26f), new Color("c47b8a"));
        Foot(root, -0.1f, sitting ? -0.22f : 0.04f, pink);
        Foot(root, 0.1f, sitting ? -0.22f : 0.04f, pink);
        var armL = Ball(root, 0.08f, new Vector3(-0.32f, bodyY + 0.02f, sitting ? -0.12f : 0f), pink, new Vector3(0.7f, 0.55f, 0.7f));
        var armR = Ball(root, 0.08f, new Vector3(0.32f, bodyY + 0.02f, sitting ? -0.12f : 0f), pink, new Vector3(0.7f, 0.55f, 0.7f));
        if (sitting)
        {
            armL.RotationDegrees = new Vector3(55f, 0f, 0f);
            armR.RotationDegrees = new Vector3(55f, 0f, 0f);
        }
    }

    private static void Eye(Node parent, Vector3 position, float scale = 1f)
    {
        Ball(parent, 0.045f * scale, position, new Color("f7f7f7"), new Vector3(1f, 1.35f, 0.7f));
        Ball(parent, 0.02f * scale, position + new Vector3(0f, 0f, -0.03f), new Color("1a1a1a"), Vector3.One);
    }

    private static void Ear(Node parent, float side, float headY, Color outer, Color innerColor)
    {
        var ear = Ball(parent, 0.11f, new Vector3(side * 0.28f, headY - 0.02f, 0.02f), outer, new Vector3(0.48f, 1.65f, 0.42f));
        ear.RotationDegrees = new Vector3(8f, 0f, side * 28f);
        var inner = Ball(parent, 0.06f, new Vector3(side * 0.3f, headY - 0.08f, -0.01f), innerColor, new Vector3(0.4f, 1.15f, 0.32f));
        inner.RotationDegrees = ear.RotationDegrees;
    }

    private static void Foot(Node parent, float x, float z, Color color)
    {
        MeshUtil.Box(parent, new Vector3(0.12f, 0.05f, 0.18f), new Vector3(x, 0.03f, z), color);
    }

    private static void Flipper(Node parent, float side, float y, bool sitting, Color color, float scale = 1f)
    {
        Vector3 pos = sitting
            ? new Vector3(side * 0.28f, y + 0.02f, -0.22f)
            : new Vector3(side * 0.4f, y, 0.02f);
        Vector3 size = new Vector3(0.08f, sitting ? 0.07f : 0.18f, sitting ? 0.26f : 0.22f) * scale;
        MeshInstance3D flipper = MeshUtil.Box(parent, size, pos, color);
        flipper.RotationDegrees = sitting
            ? new Vector3(68f, side * 12f, 0f)
            : new Vector3(0f, 0f, side * 8f);
    }

    private static MeshInstance3D Ball(Node parent, float radius, Vector3 position, Color color, Vector3 scale)
    {
        MeshInstance3D mesh = MeshUtil.Sphere(parent, radius, position, MeshUtil.Shared(color, 0.78f));
        mesh.Scale = scale;
        return mesh;
    }
}
