using System.Collections.Generic;
using Godot;

namespace DeliveryTemperatureWar;

public partial class Streetscape : Node3D
{
    private readonly List<TrafficSignal> _signals = new();
    private readonly List<(DeliveryEvent Kind, Area3D Area)> _zones = new();
    private Area3D? _delivery;

    public Vector3 DeliveryPoint { get; private set; }
    public bool MainIsRed => _signals.Count > 0 && _signals[0].MainIsRed;

    public override void _EnterTree()
    {
        AddToGroup("street");
    }

    public override void _Ready()
    {
        BuildGround();
        BuildBuildings();
        BuildProps();
        BuildSignals();
        BuildZones();
        DeliveryPoint = new Vector3(Track.DeliveryX, 0.05f, Track.DeliveryZ);
    }

    public void Hook(EventDirector events)
    {
        foreach ((DeliveryEvent kind, Area3D area) in _zones)
        {
            DeliveryEvent captured = kind;
            area.BodyEntered += body =>
            {
                if (body.IsInGroup("bike"))
                {
                    events.OnZone(captured);
                }
            };
        }

        if (_delivery != null)
        {
            _delivery.BodyEntered += body =>
            {
                if (body.IsInGroup("bike"))
                {
                    events.RequestArrival();
                }
            };
        }
    }

    public void ForceMainRed(float seconds)
    {
        foreach (TrafficSignal signal in _signals)
        {
            signal.ForceRed(seconds);
        }
    }

    private void BuildGround()
    {
        AddStatic(new Vector3(80f, 1f, 190f), new Vector3(0f, -0.5f, -48f), new Color("3c424a"));
        AddSidewalk(16f, Track.IntersectionZ + 12f, true);
        Decal(new Vector3(8.6f, 0.02f, Track.ShopZ), new Vector3(7f, 0.02f, 6f), new Color("c9c1b2"));
        AddSidewalk(Track.IntersectionZ - 12f, -114f);
        Decal(new Vector3(-22f, 0.045f, Track.IntersectionZ), new Vector3(28f, 0.02f, 2.4f), new Color("c9c1b2"));
        Decal(new Vector3(22f, 0.045f, Track.IntersectionZ), new Vector3(28f, 0.02f, 2.4f), new Color("c9c1b2"));

        Decal(new Vector3(0f, 0.012f, -48f), new Vector3(70f, 0.02f, 16f), new Color("4a515a"));
        Decal(new Vector3(18f, 0.02f, -48f), new Vector3(28f, 0.02f, 160f), new Color("7d9a68"));
        Decal(new Vector3(-18f, 0.02f, -48f), new Vector3(28f, 0.02f, 160f), new Color("7d9a68"));
        Decal(new Vector3(4.55f, 0.03f, -48f), new Vector3(0.1f, 0.02f, 150f), new Color("d8d2c4"));
        Decal(new Vector3(-4.55f, 0.03f, -48f), new Vector3(0.1f, 0.02f, 150f), new Color("d8d2c4"));
        for (float z = 14f; z > -112f; z -= 4.2f)
        {
            if (z < Track.IntersectionZ + 8f && z > Track.IntersectionZ - 8f)
            {
                continue;
            }

            Decal(new Vector3(0f, 0.03f, z), new Vector3(0.12f, 0.02f, 1.5f), new Color("e6c84a"));
        }

        Decal(new Vector3(0f, 0.035f, Track.IntersectionZ + 6.2f), new Vector3(8.2f, 0.02f, 0.18f), Colors.White);
        for (int i = 0; i < 6; i++)
        {
            Decal(new Vector3(-3.2f + i * 1.3f, 0.035f, Track.IntersectionZ + 2.8f), new Vector3(0.7f, 0.02f, 2.4f), Colors.White);
        }

        AddStatic(new Vector3(14f, 1.2f, 0.4f), new Vector3(0f, 0.6f, 14f), new Color("5c656e"));
        AddStatic(new Vector3(14f, 1.6f, 0.5f), new Vector3(0f, 0.8f, -116f), new Color("5c656e"));
    }

    private void BuildBuildings()
    {
        var rng = new RandomNumberGenerator { Seed = 20260328 };
        Color[] palette =
        {
            new("f2d2b3"),
            new("d7e2ea"),
            new("f0c36a"),
            new("c9d6c2"),
            new("e7b7b0"),
            new("cfd3dc"),
            new("f7e7c6"),
        };
        float z = 18f;
        while (z > -122f)
        {
            float depth = rng.RandfRange(7f, 12f);
            float center = z - depth * 0.5f;
            z -= depth + rng.RandfRange(0.4f, 1.5f);
            if (Overlaps(center, depth, Track.IntersectionZ, 18f) || Overlaps(center, depth, Track.DeliveryZ, 16f))
            {
                continue;
            }

            if (!Overlaps(center, depth, Track.ShopZ, 12f))
            {
                SpawnBuilding(1, center, depth, rng, palette);
            }

            SpawnBuilding(-1, center, depth, rng, palette);
        }

        SpawnDestination();
        MeshUtil.Sign(this, "面馆", new Vector3(7.6f, 3.4f, -10f));
        MeshUtil.Sign(this, "奶茶", new Vector3(-7.6f, 3.2f, -16f));
        MeshUtil.Sign(this, "生鲜", new Vector3(7.8f, 3.6f, -64f));
        MeshUtil.Sign(this, "热食楼", new Vector3(9.2f, 8.5f, Track.DeliveryZ), 0.012f);
    }

    private void SpawnBuilding(int side, float center, float depth, RandomNumberGenerator rng, Color[] palette)
    {
        float width = rng.RandfRange(6.2f, 9.5f);
        float height = rng.RandfRange(6.5f, 16f);
        float x = side * (7.5f + width * 0.5f);
        Color color = palette[rng.RandiRange(0, palette.Length - 1)];
        StaticBody3D body = AddStatic(new Vector3(width, height, depth), new Vector3(x, height * 0.5f, center), color);
        AddWindows(body, side, width, height, depth);
    }

    private void SpawnDestination()
    {
        const float width = 11f;
        const float height = 18f;
        const float depth = 12f;
        var body = AddStatic(
            new Vector3(width, height, depth),
            new Vector3(12.2f, height * 0.5f, Track.DeliveryZ),
            new Color("f6e2c4"));
        AddWindows(body, 1, width, height, depth);
        MeshUtil.Box(body, new Vector3(0.2f, 2.4f, 1.8f), new Vector3(-width * 0.5f - 0.02f, -height * 0.5f + 1.4f, 0f), new Color("6b4a32"));
        var pad = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 1.35f, BottomRadius = 1.35f, Height = 0.05f },
            Position = new Vector3(Track.DeliveryX, 0.04f, Track.DeliveryZ),
        };
        StandardMaterial3D padMat = MeshUtil.Fresh(new Color("3dd68c"), 0.4f);
        padMat.EmissionEnabled = true;
        padMat.Emission = new Color("39d98a");
        padMat.EmissionEnergyMultiplier = 0.8f;
        pad.MaterialOverride = padMat;
        AddChild(pad);
        MeshUtil.Sign(this, "送达", new Vector3(Track.DeliveryX, 1.6f, Track.DeliveryZ), 0.01f);
    }

    private static void AddWindows(Node3D building, int side, float width, float height, float depth)
    {
        int cols = Mathf.Max(1, Mathf.FloorToInt(depth / 1.7f));
        int rows = Mathf.Max(1, Mathf.FloorToInt((height - 2.2f) / 1.8f));
        int count = cols * rows;
        var multi = new MultiMeshInstance3D
        {
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        var mesh = new BoxMesh { Size = new Vector3(0.08f, 0.72f, 0.9f) };
        StandardMaterial3D mat = MeshUtil.Fresh(new Color("d5eef8"), 0.18f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color("ffd7a1");
        mat.EmissionEnergyMultiplier = 0.28f;
        mesh.Material = mat;
        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh,
            InstanceCount = count,
        };
        int index = 0;
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                float lz = -depth * 0.5f + 1.15f + col * 1.6f;
                float ly = -height * 0.5f + 2.3f + row * 1.75f;
                float lx = -side * (width * 0.5f + 0.05f);
                mm.SetInstanceTransform(index, new Transform3D(Basis.Identity, new Vector3(lx, ly, lz)));
                index++;
            }
        }

        multi.Multimesh = mm;
        building.AddChild(multi);
    }

    private void BuildProps()
    {
        Color[] colors =
        {
            new("d64545"),
            new("3d7ea6"),
            new("f4f1ea"),
            new("2f343a"),
            new("f2c14e"),
        };
        float[] parkedZ = { -8f, -18f, -63f, -84f, -94f };
        for (int i = 0; i < parkedZ.Length; i++)
        {
            StaticBody3D car = PropFactory.ParkedCar(colors[i % colors.Length]);
            car.Position = new Vector3(i % 2 == 0 ? 4.55f : -4.55f, 0f, parkedZ[i]);
            if (i % 2 == 1)
            {
                car.RotationDegrees = new Vector3(0f, 180f, 0f);
            }

            AddChild(car);
        }

        float[] trees = { 4f, -28f, -76f, -98f };
        for (int i = 0; i < trees.Length; i++)
        {
            float x = i % 2 == 0 ? 6.35f : -6.35f;
            var trunk = AddStatic(new Vector3(0.28f, 1.7f, 0.28f), new Vector3(x, 0.85f, trees[i]), new Color("6b4a2b"));
            MeshUtil.Sphere(trunk, 0.85f, new Vector3(0f, 1.35f, 0f), MeshUtil.Shared(new Color("3f7d46"), 0.85f));
        }
    }

    private void BuildSignals()
    {
        AddSignal(new Vector3(5.15f, 0f, Track.IntersectionZ + 6.4f), false, 180f);
        AddSignal(new Vector3(-5.15f, 0f, Track.IntersectionZ - 6.4f), false, 0f);
        AddSignal(new Vector3(-8.5f, 0f, Track.IntersectionZ + 3f), true, 90f);
        AddSignal(new Vector3(8.5f, 0f, Track.IntersectionZ - 3f), true, -90f);
    }

    private void AddSignal(Vector3 position, bool inverted, float yaw)
    {
        var signal = new TrafficSignal(inverted)
        {
            Position = position,
            RotationDegrees = new Vector3(0f, yaw, 0f),
        };
        AddChild(signal);
        _signals.Add(signal);
    }

    private void BuildZones()
    {
        _zones.Add((DeliveryEvent.DogChase, Zone(Track.DogZ)));
        _zones.Add((DeliveryEvent.RedLight, Zone(Track.RedZ)));
        _zones.Add((DeliveryEvent.RainGust, Zone(Track.RainZ)));
        _zones.Add((DeliveryEvent.SuddenBrake, Zone(Track.BrakeZ)));
        _delivery = Zone(Track.DeliveryZ, 12f);
    }

    private Area3D Zone(float z, float depth = 8f)
    {
        var area = new Area3D
        {
            Position = new Vector3(Track.LaneX, 1.3f, z),
            CollisionLayer = 0,
            CollisionMask = Layers.Bike,
            Monitoring = true,
            Monitorable = false,
        };
        area.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(9f, 2.6f, depth) },
        });
        AddChild(area);
        return area;
    }

    private void AddSidewalk(float fromZ, float toZ, bool shopGap = false)
    {
        float min = Mathf.Min(fromZ, toZ);
        float max = Mathf.Max(fromZ, toZ);
        float length = max - min;
        float center = (min + max) * 0.5f;
        AddStatic(new Vector3(2.3f, 0.08f, length), new Vector3(-6.15f, 0.04f, center), new Color("c9c1b2"));
        if (!shopGap)
        {
            AddStatic(new Vector3(2.3f, 0.08f, length), new Vector3(6.15f, 0.04f, center), new Color("c9c1b2"));
            return;
        }

        AddSideChunk(11f, max);
        AddSideChunk(min, 5f);
    }

    private void AddSideChunk(float fromZ, float toZ)
    {
        if (toZ - fromZ < 0.5f)
        {
            return;
        }

        float center = (fromZ + toZ) * 0.5f;
        AddStatic(new Vector3(2.3f, 0.08f, toZ - fromZ), new Vector3(6.15f, 0.04f, center), new Color("c9c1b2"));
    }

    private StaticBody3D AddStatic(Vector3 size, Vector3 position, Color color)
    {
        var body = new StaticBody3D
        {
            Position = position,
            CollisionLayer = Layers.World,
            CollisionMask = 0,
        };
        body.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = MeshUtil.Shared(color, 0.82f),
        });
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        AddChild(body);
        return body;
    }

    private void Decal(Vector3 position, Vector3 size, Color color)
    {
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            Position = position,
            MaterialOverride = MeshUtil.Shared(color, 0.9f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(mesh);
    }

    private static bool Overlaps(float center, float depth, float other, float gap)
    {
        return Mathf.Abs(center - other) < depth * 0.5f + gap * 0.5f;
    }
}
