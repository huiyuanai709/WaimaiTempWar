using Godot;

namespace DeliveryTemperatureWar;

public static class PropFactory
{
    public static StaticBody3D ParkedCar(Color color)
    {
        var body = new StaticBody3D
        {
            CollisionLayer = Layers.World,
            CollisionMask = Layers.Bike | Layers.Bag,
        };
        DressCar(body, color);
        return body;
    }

    public static AnimatableBody3D TrafficCar(Color color)
    {
        var body = new AnimatableBody3D
        {
            CollisionLayer = Layers.Traffic,
            CollisionMask = Layers.World | Layers.Bike | Layers.Bag,
            SyncToPhysics = true,
        };
        DressCar(body, color);
        return body;
    }

    public static Node3D Dog()
    {
        var dog = new Node3D { Name = "Dog" };
        MeshUtil.Box(dog, new Vector3(0.28f, 0.22f, 0.62f), new Vector3(0f, 0.28f, 0f), new Color("8a5a32"));
        MeshUtil.Box(dog, new Vector3(0.22f, 0.2f, 0.24f), new Vector3(0f, 0.42f, -0.34f), new Color("a56b3c"));
        MeshUtil.Cylinder(dog, 0.035f, 0.18f, new Vector3(-0.1f, 0.12f, -0.16f), new Color("6e4524"), false);
        MeshUtil.Cylinder(dog, 0.035f, 0.18f, new Vector3(0.1f, 0.12f, -0.16f), new Color("6e4524"), false);
        MeshUtil.Cylinder(dog, 0.035f, 0.18f, new Vector3(-0.1f, 0.12f, 0.18f), new Color("6e4524"), false);
        MeshUtil.Cylinder(dog, 0.035f, 0.18f, new Vector3(0.1f, 0.12f, 0.18f), new Color("6e4524"), false);
        MeshUtil.Box(dog, new Vector3(0.06f, 0.06f, 0.22f), new Vector3(0f, 0.36f, 0.4f), new Color("c4894e"));
        return dog;
    }

    private static void DressCar(Node3D body, Color color)
    {
        MeshUtil.Box(body, new Vector3(1.7f, 0.62f, 3.5f), new Vector3(0f, 0.58f, 0f), color);
        MeshUtil.Box(body, new Vector3(1.5f, 0.5f, 1.55f), new Vector3(0f, 1.05f, -0.15f), color.Darkened(0.28f));
        MeshUtil.Box(body, new Vector3(1.35f, 0.36f, 1.35f), new Vector3(0f, 1.08f, -0.15f), new Color("9fd4e8"));
        var lampMat = MeshUtil.Fresh(new Color("ffb4a8"), 0.4f);
        lampMat.EmissionEnabled = true;
        lampMat.Emission = new Color("ff4d4d");
        lampMat.EmissionEnergyMultiplier = 0.4f;
        MeshUtil.Sphere(body, 0.08f, new Vector3(-0.55f, 0.7f, 1.72f), lampMat);
        MeshUtil.Sphere(body, 0.08f, new Vector3(0.55f, 0.7f, 1.72f), lampMat);
        Color tire = new Color("1c1c1c");
        MeshUtil.Cylinder(body, 0.32f, 0.18f, new Vector3(-0.82f, 0.32f, 1.15f), tire, true);
        MeshUtil.Cylinder(body, 0.32f, 0.18f, new Vector3(0.82f, 0.32f, 1.15f), tire, true);
        MeshUtil.Cylinder(body, 0.32f, 0.18f, new Vector3(-0.82f, 0.32f, -1.15f), tire, true);
        MeshUtil.Cylinder(body, 0.32f, 0.18f, new Vector3(0.82f, 0.32f, -1.15f), tire, true);

        var shape = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(1.8f, 1.35f, 3.7f) },
            Position = new Vector3(0f, 0.7f, 0f),
        };
        body.AddChild(shape);
    }
}
