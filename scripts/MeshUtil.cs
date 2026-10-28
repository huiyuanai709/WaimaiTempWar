using System.Collections.Generic;
using Godot;

namespace DeliveryTemperatureWar;

public static class GameFonts
{
    private static FontFile? _ui;
    private static bool _tried;

    public static FontFile? Ui
    {
        get
        {
            if (_tried)
            {
                return _ui;
            }

            _tried = true;
            var font = new FontFile();
            string path = ProjectSettings.GlobalizePath("res://assets/fonts/WenQuanYiMicroHei.ttf");
            Error err = font.LoadDynamicFont(path);
            if (err != Error.Ok)
            {
                GD.PrintErr($"CJK font failed to load ({err}) from {path}");
                return null;
            }

            _ui = font;
            return _ui;
        }
    }
}

public static class MeshUtil
{
    private static readonly Dictionary<int, StandardMaterial3D> Cache = new();
    private static readonly Dictionary<string, Texture2D> ImageCache = new();

    public static StandardMaterial3D Shared(Color color, float roughness = 0.72f)
    {
        int key = System.HashCode.Combine(color.R, color.G, color.B, roughness);
        if (Cache.TryGetValue(key, out StandardMaterial3D? found))
        {
            return found;
        }

        var mat = Fresh(color, roughness);
        Cache[key] = mat;
        return mat;
    }

    public static StandardMaterial3D Fresh(Color color, float roughness = 0.6f)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = roughness,
            Metallic = 0.04f,
        };
    }

    /// <summary>
    /// Loads a still image as an ImageTexture without needing a Godot .import file.
    /// </summary>
    public static Texture2D? LoadImageTexture(string resPath)
    {
        if (ImageCache.TryGetValue(resPath, out Texture2D? cached))
        {
            return cached;
        }

        string path = ProjectSettings.GlobalizePath(resPath);
        var image = new Image();
        Error err = image.Load(path);
        if (err != Error.Ok)
        {
            GD.PrintErr($"Rider art failed to load ({err}) from {path}");
            return null;
        }

        Texture2D texture = ImageTexture.CreateFromImage(image);
        ImageCache[resPath] = texture;
        return texture;
    }

    public static MeshInstance3D Box(Node parent, Vector3 size, Vector3 position, Color color, bool shadow = true)
    {
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            Position = position,
            MaterialOverride = Shared(color),
            CastShadow = shadow
                ? GeometryInstance3D.ShadowCastingSetting.On
                : GeometryInstance3D.ShadowCastingSetting.Off,
        };
        parent.AddChild(mesh);
        return mesh;
    }

    public static MeshInstance3D Cylinder(Node parent, float radius, float height, Vector3 position, Color color, bool axleAlongX)
    {
        var mesh = new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = radius,
                BottomRadius = radius,
                Height = height,
                RadialSegments = 12,
            },
            Position = position,
            MaterialOverride = Shared(color, 0.55f),
        };
        if (axleAlongX)
        {
            mesh.RotationDegrees = new Vector3(0f, 0f, 90f);
        }

        parent.AddChild(mesh);
        return mesh;
    }

    public static MeshInstance3D Sphere(
        Node parent,
        float radius,
        Vector3 position,
        Material material,
        int radialSegments = 12,
        int rings = 8)
    {
        var mesh = new MeshInstance3D
        {
            Mesh = new SphereMesh
            {
                Radius = radius,
                Height = radius * 2f,
                RadialSegments = radialSegments,
                Rings = rings,
            },
            Position = position,
            MaterialOverride = material,
        };
        parent.AddChild(mesh);
        return mesh;
    }

    public static StaticBody3D Solid(Node parent, Vector3 size, Vector3 position, Color color, Vector3 rotationDegrees = default)
    {
        var body = new StaticBody3D
        {
            Position = position,
            RotationDegrees = rotationDegrees,
            CollisionLayer = Layers.World,
            CollisionMask = 0,
        };
        body.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = Shared(color, 0.84f),
        });
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        parent.AddChild(body);
        return body;
    }

    public static Label3D Sign(Node parent, string text, Vector3 position, float pixelSize = 0.008f)
    {
        var label = new Label3D
        {
            Text = text,
            Position = position,
            FontSize = 56,
            PixelSize = pixelSize,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Shaded = false,
            DoubleSided = true,
            Modulate = Colors.White,
            OutlineSize = 10,
            OutlineModulate = new Color(0f, 0f, 0f, 0.88f),
            NoDepthTest = false,
        };
        FontFile? font = GameFonts.Ui;
        if (font != null)
        {
            label.Font = font;
        }

        parent.AddChild(label);
        return label;
    }
}
