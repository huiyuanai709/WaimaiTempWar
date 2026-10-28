using Godot;

namespace DeliveryTemperatureWar;

/// <summary>
/// Original low-poly courier mascots built from primitives, shaped to match
/// the fan-inspired concept art in res://assets/riders/. They are not ports
/// of any game's mesh, texture, or official character model.
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
        if (node is MeshInstance3D or Sprite3D)
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
    private static readonly Color Black = new("1a1b1e");
    private static readonly Color Charcoal = new("24262b");
    private static readonly Color White = new("f7f4ee");
    private static readonly Color Orange = new("f08a2a");
    private static readonly Color HelmetOrange = new("ef7a18");
    private static readonly Color Beak = new("f0a12a");
    private static readonly Color FootOrange = new("e8892a");
    private static readonly Color Blush = new("f3a0b0");
    private static readonly Color Fringe = new("1e2433");
    private static readonly Color VestOrange = new("f07820");
    private static readonly Color BagBlack = new("1c1d21");
    private static readonly Color Cream = new("fff6ee");
    private static readonly Color HairPink = new("f4b4c8");
    private static readonly Color EyeLavender = new("b79ad8");
    private static readonly Color BowPurple = new("8b6bc9");
    private static readonly Color ScarfOrange = new("f07a22");
    private static readonly Color BoxBrown = new("8a5a32");
    private static readonly Color LidPurple = new("9a78c8");
    private static readonly Color StrapPurple = new("7d5fb8");

    private static Texture2D? _fatArt;
    private static Texture2D? _thinArt;
    private static Texture2D? _doroArt;
    private static bool _artTried;

    public static Vector3 MealHand(RiderKind kind)
    {
        return kind switch
        {
            RiderKind.ThinGuga => new Vector3(0.22f, 1.05f, -0.18f),
            RiderKind.Doro => new Vector3(0.34f, 0.55f, -0.2f),
            _ => new Vector3(0.4f, 0.7f, -0.2f),
        };
    }

    public static Node3D Build(RiderKind kind, bool sitting, float scale = 1f)
    {
        EnsureArt();
        var root = new Node3D { Name = kind.ToString() };
        switch (kind)
        {
            case RiderKind.ThinGuga:
                BuildThin(root, sitting);
                if (scale >= 0.95f)
                {
                    AttachConceptFace(root, _thinArt, sitting ? 1.42f : 1.58f, sitting ? -0.16f : -0.02f, 0.42f);
                }
                break;
            case RiderKind.Doro:
                BuildDoro(root, sitting);
                if (scale >= 0.95f)
                {
                    AttachConceptFace(root, _doroArt, sitting ? 0.92f : 1.08f, sitting ? -0.02f : 0.02f, 0.48f);
                }
                break;
            default:
                BuildFat(root, sitting);
                if (scale >= 0.95f)
                {
                    AttachConceptFace(root, _fatArt, sitting ? 0.95f : 1.12f, sitting ? -0.04f : 0f, 0.46f);
                }
                break;
        }

        root.Scale = Vector3.One * scale;
        return root;
    }

    private static void EnsureArt()
    {
        if (_artTried)
        {
            return;
        }

        _artTried = true;
        _fatArt = MeshUtil.LoadImageTexture("res://assets/riders/fat-guga.jpg");
        _thinArt = MeshUtil.LoadImageTexture("res://assets/riders/thin-guga.jpg");
        _doroArt = MeshUtil.LoadImageTexture("res://assets/riders/doro.jpg");
    }

    /// <summary>
    /// Small billboard portrait so the skin reads like the concept art at a glance.
    /// </summary>
    private static void AttachConceptFace(Node3D root, Texture2D? texture, float y, float z, float height)
    {
        if (texture == null)
        {
            return;
        }

        float aspect = texture.GetWidth() / (float)Mathf.Max(1, texture.GetHeight());
        var sprite = new Sprite3D
        {
            Name = "ConceptFace",
            Texture = texture,
            PixelSize = height / Mathf.Max(1, texture.GetHeight()),
            Position = new Vector3(0f, y, z - 0.42f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Shaded = false,
            DoubleSided = true,
            AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
            AlphaScissorThreshold = 0.08f,
            Modulate = new Color(1f, 1f, 1f, 0.98f),
            RenderPriority = 2,
        };
        // Keep width proportional without stretching the portrait.
        sprite.Scale = new Vector3(Mathf.Clamp(aspect, 0.7f, 1.15f), 1f, 1f);
        root.AddChild(sprite);
    }

    private static void BuildFat(Node3D root, bool sitting)
    {
        float bodyY = sitting ? 0.38f : 0.52f;
        float headY = sitting ? 0.78f : 0.98f;
        float footZ = sitting ? -0.26f : 0.04f;
        float lean = sitting ? -0.08f : 0f;

        // Pear body + white belly.
        SoftBall(root, 0.38f, new Vector3(0f, bodyY, lean), Charcoal, new Vector3(1.22f, 1.05f, 1.08f), 0.82f);
        SoftBall(root, 0.24f, new Vector3(0f, bodyY - 0.02f, lean - 0.16f), White, new Vector3(0.95f, 1.12f, 0.52f), 0.88f);

        // Head with white face oval.
        SoftBall(root, 0.26f, new Vector3(0f, headY, lean), Charcoal, new Vector3(1.08f, 0.95f, 1.02f), 0.8f);
        SoftBall(root, 0.14f, new Vector3(0f, headY - 0.02f, lean - 0.18f), White, new Vector3(1.05f, 0.95f, 0.58f), 0.9f);

        // Jagged fringe under the helmet.
        SoftBall(root, 0.05f, new Vector3(-0.08f, headY + 0.08f, lean - 0.2f), Fringe, new Vector3(0.7f, 0.9f, 0.45f), 0.75f);
        SoftBall(root, 0.055f, new Vector3(0f, headY + 0.1f, lean - 0.22f), Fringe, new Vector3(0.75f, 1f, 0.45f), 0.75f);
        SoftBall(root, 0.05f, new Vector3(0.08f, headY + 0.08f, lean - 0.2f), Fringe, new Vector3(0.7f, 0.9f, 0.45f), 0.75f);

        // Orange delivery helmet + black brim + chin strap.
        SoftBall(root, 0.2f, new Vector3(0f, headY + 0.14f, lean + 0.02f), HelmetOrange, new Vector3(1.25f, 0.72f, 1.2f), 0.55f);
        MeshUtil.Box(root, new Vector3(0.42f, 0.045f, 0.18f), new Vector3(0f, headY + 0.05f, lean - 0.12f), Black);
        MeshUtil.Box(root, new Vector3(0.04f, 0.16f, 0.03f), new Vector3(-0.16f, headY - 0.02f, lean - 0.08f), Black);
        MeshUtil.Box(root, new Vector3(0.04f, 0.16f, 0.03f), new Vector3(0.16f, headY - 0.02f, lean - 0.08f), Black);
        MeshUtil.Box(root, new Vector3(0.34f, 0.03f, 0.03f), new Vector3(0f, headY - 0.12f, lean - 0.06f), Black);

        BeakTri(root, new Vector3(0f, headY - 0.06f, lean - 0.3f), Beak);
        GlossEye(root, new Vector3(-0.09f, headY + 0.02f, lean - 0.24f), 1f);
        GlossEye(root, new Vector3(0.09f, headY + 0.02f, lean - 0.24f), 1f);
        SoftBall(root, 0.035f, new Vector3(-0.14f, headY - 0.06f, lean - 0.22f), Blush, new Vector3(1.2f, 0.8f, 0.5f), 0.95f);
        SoftBall(root, 0.035f, new Vector3(0.14f, headY - 0.06f, lean - 0.22f), Blush, new Vector3(1.2f, 0.8f, 0.5f), 0.95f);

        // Orange thermal backpack + straps.
        MeshUtil.Box(root, new Vector3(0.42f, 0.48f, 0.28f), new Vector3(0f, bodyY + 0.06f, lean + 0.28f), HelmetOrange);
        MeshUtil.Box(root, new Vector3(0.44f, 0.05f, 0.3f), new Vector3(0f, bodyY + 0.28f, lean + 0.28f), Orange);
        MeshUtil.Box(root, new Vector3(0.38f, 0.04f, 0.04f), new Vector3(0f, bodyY + 0.02f, lean + 0.42f), Black);
        MeshUtil.Box(root, new Vector3(0.06f, 0.36f, 0.05f), new Vector3(-0.18f, bodyY + 0.08f, lean + 0.08f), Orange);
        MeshUtil.Box(root, new Vector3(0.06f, 0.36f, 0.05f), new Vector3(0.18f, bodyY + 0.08f, lean + 0.08f), Orange);

        ToedFoot(root, -0.14f, footZ, FootOrange);
        ToedFoot(root, 0.14f, footZ, FootOrange);
        RoundFlipper(root, -1f, bodyY + 0.04f, sitting, Black);
        RoundFlipper(root, 1f, bodyY + 0.04f, sitting, Black);
    }

    private static void BuildThin(Node3D root, bool sitting)
    {
        float lean = sitting ? -0.14f : 0f;
        float footZ = sitting ? -0.3f : 0.04f;
        float bodyY = sitting ? 0.55f : 0.72f;
        float neckY = sitting ? 1.02f : 1.28f;
        float headY = sitting ? 1.28f : 1.58f;

        // Long skinny legs + tall torso.
        MeshUtil.Box(root, new Vector3(0.07f, 0.4f, 0.07f), new Vector3(-0.07f, sitting ? 0.14f : 0.2f, footZ + 0.02f), Black);
        MeshUtil.Box(root, new Vector3(0.07f, 0.4f, 0.07f), new Vector3(0.07f, sitting ? 0.14f : 0.2f, footZ + 0.02f), Black);
        SoftBall(root, 0.15f, new Vector3(0f, bodyY, lean), Charcoal, new Vector3(0.62f, 2.4f, 0.58f), 0.8f);
        SoftBall(root, 0.1f, new Vector3(0f, bodyY - 0.05f, lean - 0.08f), White, new Vector3(0.62f, 1.55f, 0.38f), 0.88f);

        // Long neck + small head with white face patch.
        SoftBall(root, 0.07f, new Vector3(0f, neckY, lean), Charcoal, new Vector3(0.85f, 1.55f, 0.85f), 0.78f);
        SoftBall(root, 0.055f, new Vector3(0f, neckY, lean - 0.05f), White, new Vector3(0.7f, 1.35f, 0.45f), 0.9f);
        SoftBall(root, 0.16f, new Vector3(0f, headY, lean), Charcoal, new Vector3(0.95f, 1.05f, 0.95f), 0.8f);
        SoftBall(root, 0.1f, new Vector3(0f, headY - 0.01f, lean - 0.12f), White, new Vector3(1.05f, 1.15f, 0.55f), 0.9f);

        SoftBall(root, 0.035f, new Vector3(-0.05f, headY + 0.1f, lean - 0.14f), Fringe, new Vector3(0.7f, 0.8f, 0.4f), 0.75f);
        SoftBall(root, 0.04f, new Vector3(0.02f, headY + 0.12f, lean - 0.15f), Fringe, new Vector3(0.75f, 0.9f, 0.4f), 0.75f);

        BeakTri(root, new Vector3(0f, headY - 0.04f, lean - 0.24f), Beak, 0.75f);
        PatchEye(root, new Vector3(-0.07f, headY + 0.04f, lean - 0.16f));
        PatchEye(root, new Vector3(0.07f, headY + 0.04f, lean - 0.16f));

        // Oversized open orange vest + wide collar.
        MeshUtil.Box(root, new Vector3(0.34f, 0.72f, 0.08f), new Vector3(-0.12f, bodyY + 0.02f, lean - 0.02f), VestOrange);
        MeshUtil.Box(root, new Vector3(0.34f, 0.72f, 0.08f), new Vector3(0.12f, bodyY + 0.02f, lean - 0.02f), VestOrange);
        MeshUtil.Box(root, new Vector3(0.48f, 0.1f, 0.14f), new Vector3(0f, bodyY + 0.38f, lean - 0.02f), VestOrange);
        MeshUtil.Box(root, new Vector3(0.2f, 0.08f, 0.16f), new Vector3(-0.14f, bodyY + 0.42f, lean - 0.08f), VestOrange);
        MeshUtil.Box(root, new Vector3(0.2f, 0.08f, 0.16f), new Vector3(0.14f, bodyY + 0.42f, lean - 0.08f), VestOrange);

        // Tall black delivery bag.
        MeshUtil.Box(root, new Vector3(0.34f, 0.7f, 0.24f), new Vector3(0f, bodyY + 0.08f, lean + 0.24f), BagBlack);
        MeshUtil.Box(root, new Vector3(0.3f, 0.04f, 0.04f), new Vector3(0f, bodyY + 0.28f, lean + 0.36f), Charcoal);
        MeshUtil.Box(root, new Vector3(0.05f, 0.4f, 0.04f), new Vector3(-0.12f, bodyY + 0.1f, lean + 0.08f), BagBlack);
        MeshUtil.Box(root, new Vector3(0.05f, 0.4f, 0.04f), new Vector3(0.12f, bodyY + 0.1f, lean + 0.08f), BagBlack);

        ToedFoot(root, -0.08f, footZ, FootOrange, 0.85f);
        ToedFoot(root, 0.08f, footZ, FootOrange, 0.85f);
        RoundFlipper(root, -1f, bodyY + 0.1f, sitting, Black, 0.7f);
        RoundFlipper(root, 1f, bodyY + 0.1f, sitting, Black, 0.7f);
    }

    private static void BuildDoro(Node3D root, bool sitting)
    {
        float bodyY = sitting ? 0.34f : 0.42f;
        float headY = sitting ? 0.78f : 0.92f;
        float lean = sitting ? -0.06f : 0f;

        // White four-leg blob body.
        SoftBall(root, 0.36f, new Vector3(0f, bodyY, lean), Cream, new Vector3(1.2f, 0.95f, 1.15f), 0.9f);
        SoftBall(root, 0.28f, new Vector3(0f, headY, lean), Cream, new Vector3(1.12f, 1.02f, 1.08f), 0.9f);

        // Four stubby legs.
        SoftBall(root, 0.09f, new Vector3(-0.16f, 0.1f, lean - 0.14f), Cream, new Vector3(0.9f, 0.7f, 0.9f), 0.92f);
        SoftBall(root, 0.09f, new Vector3(0.16f, 0.1f, lean - 0.14f), Cream, new Vector3(0.9f, 0.7f, 0.9f), 0.92f);
        SoftBall(root, 0.09f, new Vector3(-0.16f, 0.1f, lean + 0.14f), Cream, new Vector3(0.9f, 0.7f, 0.9f), 0.92f);
        SoftBall(root, 0.09f, new Vector3(0.16f, 0.1f, lean + 0.14f), Cream, new Vector3(0.9f, 0.7f, 0.9f), 0.92f);

        // Pink bangs swept right + left side bun + purple bow.
        SoftBall(root, 0.2f, new Vector3(0.04f, headY + 0.14f, lean + 0.02f), HairPink, new Vector3(1.35f, 0.55f, 1.15f), 0.85f);
        SoftBall(root, 0.08f, new Vector3(0.16f, headY + 0.04f, lean - 0.16f), HairPink, new Vector3(1.1f, 0.7f, 0.55f), 0.85f);
        SoftBall(root, 0.07f, new Vector3(0.08f, headY + 0.02f, lean - 0.2f), HairPink, new Vector3(1f, 0.65f, 0.5f), 0.85f);
        SoftBall(root, 0.12f, new Vector3(-0.26f, headY + 0.1f, lean + 0.02f), HairPink, new Vector3(1.15f, 1.05f, 1.1f), 0.85f);
        SoftBall(root, 0.05f, new Vector3(-0.34f, headY + 0.16f, lean), BowPurple, new Vector3(1.4f, 0.7f, 0.7f), 0.7f);
        SoftBall(root, 0.05f, new Vector3(-0.3f, headY + 0.2f, lean + 0.04f), BowPurple, new Vector3(0.7f, 1.3f, 0.7f), 0.7f);
        SoftBall(root, 0.03f, new Vector3(-0.32f, headY + 0.18f, lean + 0.02f), new Color("6a4ca0"), Vector3.One, 0.65f);

        LavenderEye(root, new Vector3(-0.09f, headY + 0.02f, lean - 0.24f));
        LavenderEye(root, new Vector3(0.09f, headY + 0.02f, lean - 0.24f));
        SoftBall(root, 0.04f, new Vector3(-0.16f, headY - 0.06f, lean - 0.22f), Blush, new Vector3(1.3f, 0.85f, 0.5f), 0.95f);
        SoftBall(root, 0.04f, new Vector3(0.16f, headY - 0.06f, lean - 0.22f), Blush, new Vector3(1.3f, 0.85f, 0.5f), 0.95f);

        // Open mouth + pink tongue.
        SoftBall(root, 0.045f, new Vector3(0f, headY - 0.1f, lean - 0.26f), new Color("3a2a2a"), new Vector3(1.1f, 0.7f, 0.55f), 0.7f);
        SoftBall(root, 0.025f, new Vector3(0f, headY - 0.11f, lean - 0.28f), new Color("f08aa0"), new Vector3(1.2f, 0.55f, 0.6f), 0.85f);

        // Orange scarf.
        SoftBall(root, 0.14f, new Vector3(0f, bodyY + 0.22f, lean - 0.02f), ScarfOrange, new Vector3(1.35f, 0.45f, 1.15f), 0.7f);
        MeshUtil.Box(root, new Vector3(0.16f, 0.14f, 0.05f), new Vector3(0.1f, bodyY + 0.12f, lean - 0.2f), ScarfOrange);

        // Delivery box: orange body, brown frame, purple lid + purple harness.
        MeshUtil.Box(root, new Vector3(0.36f, 0.32f, 0.26f), new Vector3(0f, bodyY + 0.08f, lean + 0.3f), ScarfOrange);
        MeshUtil.Box(root, new Vector3(0.4f, 0.04f, 0.3f), new Vector3(0f, bodyY - 0.08f, lean + 0.3f), BoxBrown);
        MeshUtil.Box(root, new Vector3(0.4f, 0.04f, 0.3f), new Vector3(0f, bodyY + 0.24f, lean + 0.3f), LidPurple);
        MeshUtil.Box(root, new Vector3(0.04f, 0.32f, 0.04f), new Vector3(-0.18f, bodyY + 0.08f, lean + 0.42f), BoxBrown);
        MeshUtil.Box(root, new Vector3(0.04f, 0.32f, 0.04f), new Vector3(0.18f, bodyY + 0.08f, lean + 0.42f), BoxBrown);
        MeshUtil.Box(root, new Vector3(0.06f, 0.28f, 0.05f), new Vector3(-0.2f, bodyY + 0.04f, lean + 0.06f), StrapPurple);
        MeshUtil.Box(root, new Vector3(0.06f, 0.28f, 0.05f), new Vector3(0.2f, bodyY + 0.04f, lean + 0.06f), StrapPurple);
        MeshUtil.Box(root, new Vector3(0.42f, 0.05f, 0.05f), new Vector3(0f, bodyY - 0.02f, lean - 0.02f), StrapPurple);
    }

    private static void GlossEye(Node parent, Vector3 position, float scale)
    {
        SoftBall(parent, 0.048f * scale, position, new Color("f8f8f8"), new Vector3(1f, 1.25f, 0.7f), 0.35f);
        SoftBall(parent, 0.028f * scale, position + new Vector3(0f, 0f, -0.025f), Black, Vector3.One, 0.25f);
        SoftBall(parent, 0.01f * scale, position + new Vector3(0.012f, 0.012f, -0.04f), Colors.White, Vector3.One, 0.2f);
    }

    private static void PatchEye(Node parent, Vector3 position)
    {
        SoftBall(parent, 0.055f, position, White, new Vector3(1.05f, 1.35f, 0.55f), 0.9f);
        SoftBall(parent, 0.022f, position + new Vector3(0f, -0.01f, -0.03f), Black, Vector3.One, 0.25f);
    }

    private static void LavenderEye(Node parent, Vector3 position)
    {
        SoftBall(parent, 0.07f, position, Colors.White, new Vector3(1f, 1.15f, 0.65f), 0.4f);
        SoftBall(parent, 0.045f, position + new Vector3(0f, 0f, -0.02f), EyeLavender, new Vector3(1f, 1.1f, 0.7f), 0.35f);
        SoftBall(parent, 0.02f, position + new Vector3(0f, 0f, -0.04f), new Color("2a1a3a"), Vector3.One, 0.25f);
        SoftBall(parent, 0.012f, position + new Vector3(0.015f, 0.015f, -0.05f), Colors.White, Vector3.One, 0.2f);
    }

    private static void BeakTri(Node parent, Vector3 position, Color color, float scale = 1f)
    {
        MeshInstance3D beak = MeshUtil.Box(
            parent,
            new Vector3(0.1f, 0.05f, 0.14f) * scale,
            position,
            color);
        beak.RotationDegrees = new Vector3(12f, 0f, 0f);
    }

    private static void ToedFoot(Node parent, float x, float z, Color color, float scale = 1f)
    {
        MeshUtil.Box(parent, new Vector3(0.12f, 0.05f, 0.16f) * scale, new Vector3(x, 0.03f, z), color);
        MeshUtil.Box(parent, new Vector3(0.03f, 0.03f, 0.06f) * scale, new Vector3(x - 0.035f * scale, 0.03f, z - 0.1f * scale), color);
        MeshUtil.Box(parent, new Vector3(0.03f, 0.03f, 0.06f) * scale, new Vector3(x, 0.03f, z - 0.11f * scale), color);
        MeshUtil.Box(parent, new Vector3(0.03f, 0.03f, 0.06f) * scale, new Vector3(x + 0.035f * scale, 0.03f, z - 0.1f * scale), color);
    }

    private static void RoundFlipper(Node parent, float side, float y, bool sitting, Color color, float scale = 1f)
    {
        Vector3 pos = sitting
            ? new Vector3(side * 0.3f, y + 0.02f, -0.22f)
            : new Vector3(side * 0.42f, y, 0.02f);
        MeshInstance3D flipper = SoftBall(
            parent,
            0.11f * scale,
            pos,
            color,
            sitting ? new Vector3(0.7f, 0.55f, 1.35f) : new Vector3(0.55f, 1.15f, 0.85f),
            0.8f);
        flipper.RotationDegrees = sitting
            ? new Vector3(70f, side * 10f, 0f)
            : new Vector3(0f, 0f, side * 12f);
    }

    private static MeshInstance3D SoftBall(Node parent, float radius, Vector3 position, Color color, Vector3 scale, float roughness)
    {
        MeshInstance3D mesh = MeshUtil.Sphere(parent, radius, position, MeshUtil.Shared(color, roughness), 18, 12);
        mesh.Scale = scale;
        return mesh;
    }
}
