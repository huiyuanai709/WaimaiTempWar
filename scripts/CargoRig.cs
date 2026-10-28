using Godot;

namespace DeliveryTemperatureWar;

/// <summary>
/// Spring-linked cooler on the rack, plus a hinged lid and a meal box that can tip.
/// Jolt is the physics engine. A SoftBody3D bag is available with WAIMAI_SOFT=1, but
/// in Godot 4.7 its vertices stay in world space and pinned points do not follow a
/// moving rack, so the playable cooler is two rigid panels on soft 6DOF springs.
/// The belly sags and bends; a hard hit crushes it. The meal box can still tip.
/// </summary>
public partial class CargoRig : Node3D
{
    private BikeController _bike = null!;
    private Marker3D _rack = null!;
    private SoftBody3D? _bag;
    private RigidBody3D? _shell;
    private RigidBody3D? _belly;
    private Vector3 _bellyRest;
    private bool _bellyHaveRest;
    private RigidBody3D? _food;
    private RigidBody3D? _lid;
    private Generic6DofJoint3D? _foodJoint;
    private Generic6DofJoint3D? _lidJoint;
    private StandardMaterial3D? _lidMat;
    private bool _softReady;
    private bool[] _pinned = System.Array.Empty<bool>();
    private float[] _restY = System.Array.Empty<float>();
    private Vector3[] _pinVerts = System.Array.Empty<Vector3>();
    private float _pinTop;
    private int _pinDelay = 3;
    private Vector3[] _rest = System.Array.Empty<Vector3>();
    private Vector3[] _rackRest = System.Array.Empty<Vector3>();
    private bool _haveRackRest;
    private int _bagSample;
    private float _impactArm;
    private float _bagSettle;

    private Vector3 BagPointWorld(int index)
    {
        Vector3 raw = _bag!.GetPointTransform(index);
        return raw.Length() < 3f ? _bag.ToGlobal(raw) : raw;
    }
    private int _points;
    private Vector3 _prevVel;
    private bool _hasPrev;
    private float _lidCalib;
    private bool _lidCalibrated;
    private float _openSign = 1f;
    private float _lidHold;
    private bool _shutQueued;
    private float _baseWidth = -1f;
    private float _widthDelay;

    public float Spill01 { get; private set; }
    public float Crush01 { get; private set; }
    public float LidOpen01 { get; private set; }
    public float FoodTilt { get; private set; }
    public float BagSag { get; private set; }
    public int SoftPoints { get; private set; }
    public bool SoftBodyActive { get; private set; }
    public bool CoolerLive { get; private set; }

    public override void _EnterTree()
    {
        AddToGroup("cargo");
    }

    public override void _Ready()
    {
        _bike = GetNode<BikeController>("../Bike");
        _rack = GetNode<Marker3D>("../Bike/Rack");
        if (OS.GetEnvironment("WAIMAI_SOFT") == "1")
        {
            BuildBag();
        }
        else if (OS.GetEnvironment("WAIMAI_NO_BAG") != "1")
        {
            BuildShell();
        }

        if (OS.GetEnvironment("WAIMAI_NO_RIGID") != "1")
        {
            BuildFood();
            BuildLid();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (dt <= 0.00001f)
        {
            return;
        }

        if (_bag != null && !_softReady && _pinVerts.Length > 0 && _pinDelay-- <= 0)
        {
            PinBag(_pinVerts, _pinTop);
            _pinVerts = System.Array.Empty<Vector3>();
        }

        UpdateBag(dt);
        UpdateShell(dt);
        UpdateFood(dt);
        UpdateLid(dt);
        float impulse = _bike.ConsumeImpact();
        if (_impactArm < 0.6f)
        {
            _impactArm += dt;
            impulse = 0f;
        }

        if (impulse > 90f)
        {
            float hit = Mathf.Clamp((impulse - 90f) / 320f, 0f, 0.45f);
            Crush01 = Mathf.Clamp(Crush01 + hit * 0.55f, 0f, 1f);
            AddSpill(hit * (0.04f + LidOpen01 * 0.2f));
            Vector3 kick = -_bike.LinearVelocity;
            if (kick.LengthSquared() > 0.01f && _bag != null && _softReady)
            {
                _bag.ApplyCentralImpulse(kick.Normalized() * hit * 1.6f);
            }

            if (kick.LengthSquared() > 0.01f && _belly != null)
            {
                _belly.ApplyImpulse(kick.Normalized() * hit * 0.8f, _belly.GlobalPosition + Vector3.Up * 0.1f);
            }
        }

        Vector3 velocity = _bike.LinearVelocity;
        if (_hasPrev)
        {
            Vector3 acc = (velocity - _prevVel) / dt;
            acc.Y = 0f;
            if (acc.Length() > 40f)
            {
                acc = acc.Normalized() * 40f;
            }

            if (acc.Length() > 4.5f)
            {
                if (_bag != null && _softReady)
                {
                    _bag.ApplyCentralForce(-acc * 2.1f);
                }

                if (_food != null)
                {
                    Vector3 at = _food.GlobalPosition + _food.GlobalTransform.Basis.Y * 0.11f;
                    _food.ApplyForce(-acc * _food.Mass * 0.48f, at);
                }

                if (_belly != null)
                {
                    Vector3 at = _belly.GlobalPosition + _belly.GlobalTransform.Basis.Y * 0.12f;
                    _belly.ApplyForce(-acc * _belly.Mass * 0.7f, at);
                }
            }
        }

        _prevVel = velocity;
        _hasPrev = true;
        RecoverFood();
    }

    public void AddSpill(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        Spill01 = Mathf.Clamp(Spill01 + amount, 0f, 1f);
    }

    public void NudgeContents(float signedTorque)
    {
        if (_food == null)
        {
            return;
        }

        _food.ApplyTorqueImpulse(_bike.GlobalTransform.Basis.X * signedTorque);
        if (_bag != null && _softReady)
        {
            _bag.ApplyCentralImpulse(_bike.GlobalTransform.Basis.X * signedTorque * 0.35f);
        }

        if (_belly != null)
        {
            _belly.ApplyImpulse(_bike.GlobalTransform.Basis.X * signedTorque * 0.15f, _belly.GlobalPosition + Vector3.Up * 0.08f);
        }
    }

    public void NudgeLid(float strength)
    {
        if (_lid == null || !_lidCalibrated)
        {
            return;
        }

        Vector3 axis = _bike.GlobalTransform.Basis.X;
        _lid.ApplyTorqueImpulse(axis * _openSign * strength);
    }

    public void TryShutLid()
    {
        if (_lid == null || LidOpen01 < 0.2f)
        {
            return;
        }

        if (_shutQueued)
        {
            return;
        }

        _shutQueued = true;
        _lidHold = 0.55f;
        AddSpill(0.004f);
        _lid.AngularVelocity *= 0.2f;
        if (_lidJoint != null)
        {
            _lidJoint.SetParamX(Generic6DofJoint3D.Param.AngularSpringStiffness, 70f);
        }
    }

    public void SetHeatVisual(float celsius)
    {
        if (_lidMat == null)
        {
            return;
        }

        float heat = Mathf.InverseLerp(42f, Track.StartCelsius, celsius);
        Color col = new Color(1f, Mathf.Lerp(0.28f, 0.62f, heat), Mathf.Lerp(0.12f, 0.18f, heat));
        _lidMat.AlbedoColor = col;
        _lidMat.EmissionEnabled = true;
        _lidMat.Emission = col;
        _lidMat.EmissionEnergyMultiplier = Mathf.Lerp(0.04f, 0.45f, heat);
    }

    private void BuildBag()
    {
        var primitive = new BoxMesh
        {
            Size = new Vector3(0.5f, 0.36f, 0.44f),
            SubdivideWidth = 2,
            SubdivideHeight = 2,
            SubdivideDepth = 2,
        };
        Godot.Collections.Array arrays = primitive.GetMeshArrays();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        Vector3[] verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();

        _bag = new SoftBody3D
        {
            Name = "CoolerBag",
            Mesh = mesh,
            TopLevel = false,
            TotalMass = 1.1f,
            LinearStiffness = 0.93f,
            PressureCoefficient = 2f,
            DampingCoefficient = 0.55f,
            SimulationPrecision = 6,
            CollisionLayer = Layers.Bag,
            // World is excluded so a sagging bag cannot pin the bike to the road.
            // Cars on the traffic layer still dent it.
            CollisionMask = Layers.Traffic,
            RayPickable = false,
        };
        StandardMaterial3D mat = MeshUtil.Fresh(new Color("149c90"), 0.5f);
        mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        _bag.MaterialOverride = mat;
        _rack.AddChild(_bag);
        _bag.Position = new Vector3(0f, -0.18f, 0f);
        _bag.ParentCollisionIgnore = _bag.GetPathTo(_bike);
        _bag.AddCollisionExceptionWith(_bike);
        _points = verts.Length;
        _restY = new float[_points];
        _rest = new Vector3[_points];
        _pinned = new bool[_points];
        float top = -999f;
        for (int i = 0; i < _points; i++)
        {
            if (verts[i].Y > top)
            {
                top = verts[i].Y;
            }
        }

        _pinVerts = verts;
        _pinTop = top;
    }

    private void PinBag(Vector3[] verts, float top)
    {
        if (_bag == null)
        {
            return;
        }

        int pinned = 0;
        for (int i = 0; i < verts.Length && i < _points; i++)
        {
            _restY[i] = verts[i].Y;
            _rest[i] = verts[i];
            if (verts[i].Y < top - 0.02f || Mathf.Abs(verts[i].X) > 0.12f || Mathf.Abs(verts[i].Z) > 0.1f)
            {
                continue;
            }

            // Pinned in the bag's own space. The bag node rides on the rack marker.
            _bag.SetPointPinned(i, true);
            _pinned[i] = true;
            pinned++;
        }

        int confirmed = 0;
        for (int i = 0; i < _points; i++)
        {
            if (_bag.IsPointPinned(i))
            {
                confirmed++;
            }
        }

        SoftPoints = _points;
        SoftBodyActive = confirmed > 2;
        _softReady = SoftBodyActive;
        _bagSettle = 0.35f;
        GD.Print($"Cooler soft body points={_points} pinned={pinned} confirmed={confirmed}");
    }

    private void BuildShell()
    {
        _shell = MakeDynamic("CoolerRim", 0.45f, new Vector3(0.5f, 0.07f, 0.44f), new Color("1f8a70"));
        AddChild(_shell);
        _shell.GlobalPosition = _rack.GlobalPosition + new Vector3(0f, 0.02f, 0f);
        _shell.GlobalBasis = _bike.GlobalBasis;
        _shell.LinearDamp = 0.45f;
        _shell.AngularDamp = 1.4f;
        _shell.AddCollisionExceptionWith(_bike);

        var rimJoint = new Generic6DofJoint3D { Name = "RimJoint" };
        AddChild(rimJoint);
        rimJoint.GlobalPosition = _shell.GlobalPosition;
        rimJoint.GlobalBasis = _bike.GlobalBasis;
        rimJoint.NodeA = rimJoint.GetPathTo(_bike);
        rimJoint.NodeB = rimJoint.GetPathTo(_shell);
        rimJoint.ExcludeNodesFromCollision = true;
        SpringLinear(rimJoint, 0, 0.03f, 420f, 26f);
        SpringLinear(rimJoint, 1, 0.03f, 420f, 26f);
        SpringLinear(rimJoint, 2, 0.03f, 420f, 26f);
        SpringAngular(rimJoint, 0, 0.18f, 34f, 2.2f);
        SpringAngular(rimJoint, 1, 0.18f, 20f, 1.6f);
        SpringAngular(rimJoint, 2, 0.18f, 34f, 2.2f);

        _belly = MakeDynamic("CoolerBelly", 0.95f, new Vector3(0.46f, 0.22f, 0.4f), new Color("14695a"));
        AddChild(_belly);
        _belly.GlobalPosition = _rack.GlobalPosition + new Vector3(0f, -0.16f, 0f);
        _belly.GlobalBasis = _bike.GlobalBasis;
        _belly.LinearDamp = 0.35f;
        _belly.AngularDamp = 0.9f;
        _belly.AddCollisionExceptionWith(_bike);
        _belly.AddCollisionExceptionWith(_shell);

        var bellyJoint = new Generic6DofJoint3D { Name = "BellyJoint" };
        AddChild(bellyJoint);
        bellyJoint.GlobalPosition = _belly.GlobalPosition;
        bellyJoint.GlobalBasis = _bike.GlobalBasis;
        bellyJoint.NodeA = bellyJoint.GetPathTo(_shell);
        bellyJoint.NodeB = bellyJoint.GetPathTo(_belly);
        bellyJoint.ExcludeNodesFromCollision = true;
        SpringLinear(bellyJoint, 0, 0.06f, 90f, 6f);
        SpringLinear(bellyJoint, 1, 0.12f, 48f, 4.5f);
        SpringLinear(bellyJoint, 2, 0.06f, 90f, 6f);
        SpringAngular(bellyJoint, 0, 0.5f, 5.5f, 0.7f);
        SpringAngular(bellyJoint, 1, 0.25f, 8f, 0.8f);
        SpringAngular(bellyJoint, 2, 0.5f, 5.5f, 0.7f);
        _bellyRest = _rack.GlobalTransform.AffineInverse() * _belly.GlobalPosition;
        _bellyHaveRest = true;
        CoolerLive = true;
    }

    private void UpdateShell(float dt)
    {
        if (_belly == null || _shell == null)
        {
            return;
        }

        Vector3 local = _rack.GlobalTransform.AffineInverse() * _belly.GlobalPosition;
        if (local.Length() > 0.85f || _belly.GlobalPosition.Y < 0.25f)
        {
            _belly.GlobalPosition = _rack.GlobalPosition + _bike.GlobalTransform.Basis * new Vector3(0f, -0.16f, 0f);
            _belly.LinearVelocity = _bike.LinearVelocity;
            _belly.AngularVelocity = Vector3.Zero;
            _bellyHaveRest = false;
            return;
        }

        float sag = _bellyRest.Y - local.Y;
        BagSag = Mathf.Max(sag, 0f);
    }

    private void BuildFood()
    {
        _food = MakeDynamic("FoodBox", 2.1f, new Vector3(0.28f, 0.16f, 0.24f), new Color("c47a4a"));
        MeshUtil.Cylinder(_food, 0.09f, 0.05f, new Vector3(0f, 0.07f, 0f), new Color("e25b2a"), false);
        AddChild(_food);
        _food.GlobalPosition = _rack.GlobalPosition + new Vector3(0f, -0.22f, 0f);
        _food.GlobalBasis = _bike.GlobalBasis;
        _food.AddCollisionExceptionWith(_bike);
        _food.LinearDamp = 0.7f;
        _food.AngularDamp = 1.15f;

        var joint = new Generic6DofJoint3D { Name = "FoodJoint" };
        AddChild(joint);
        joint.GlobalPosition = _food.GlobalPosition;
        joint.GlobalBasis = _bike.GlobalBasis;
        joint.NodeA = joint.GetPathTo(_bike);
        joint.NodeB = joint.GetPathTo(_food);
        joint.ExcludeNodesFromCollision = true;
        SpringLinear(joint, 0, 0.07f, 220f, 18f);
        SpringLinear(joint, 1, 0.045f, 260f, 20f);
        SpringLinear(joint, 2, 0.07f, 220f, 18f);
        SpringAngular(joint, 0, 0.62f, 16f, 1.8f);
        SpringAngular(joint, 1, 0.35f, 12f, 1.4f);
        SpringAngular(joint, 2, 0.62f, 16f, 1.8f);
        _foodJoint = joint;
    }

    private void BuildLid()
    {
        _lid = MakeDynamic("Lid", 0.42f, new Vector3(0.52f, 0.045f, 0.46f), new Color("f08a24"));
        _lidMat = (StandardMaterial3D)_lid.GetChild<MeshInstance3D>(0).MaterialOverride;
        MeshUtil.Box(_lid, new Vector3(0.12f, 0.04f, 0.08f), new Vector3(0f, 0.04f, -0.12f), new Color("ffd166"));
        AddChild(_lid);
        _lid.GlobalPosition = _rack.GlobalPosition + _bike.GlobalTransform.Basis.Y * 0.05f;
        _lid.GlobalBasis = _bike.GlobalBasis;
        _lid.AddCollisionExceptionWith(_bike);
        _lid.LinearDamp = 0.4f;
        _lid.AngularDamp = 1.4f;
        if (_food != null)
        {
            _lid.AddCollisionExceptionWith(_food);
        }

        var joint = new Generic6DofJoint3D { Name = "LidJoint" };
        AddChild(joint);
        Vector3 pivot = _lid.GlobalPosition + _bike.GlobalTransform.Basis.Z * 0.2f;
        joint.GlobalPosition = pivot;
        joint.GlobalBasis = _bike.GlobalBasis;
        joint.NodeA = joint.GetPathTo(_bike);
        joint.NodeB = joint.GetPathTo(_lid);
        joint.ExcludeNodesFromCollision = true;
        LockLinear(joint);
        SetLidLimits(-0.05f, 1.65f);
        LockAngular(joint, 1);
        LockAngular(joint, 2);
        _lidJoint = joint;
    }

    private RigidBody3D MakeDynamic(string name, float mass, Vector3 size, Color color)
    {
        var body = new RigidBody3D
        {
            Name = name,
            Mass = mass,
            CanSleep = false,
            CollisionLayer = Layers.Cargo,
            CollisionMask = Layers.World | Layers.Traffic,
            ContinuousCd = true,
        };
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = MeshUtil.Fresh(color, 0.48f),
        };
        body.AddChild(mesh);
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = size * 0.96f },
        });
        return body;
    }

    private void ReleaseBag()
    {
        if (_bag == null)
        {
            return;
        }

        for (int i = 0; i < _points; i++)
        {
            _bag.SetPointPinned(i, false);
        }

        _bag.QueueFree();
        _bag = null;
        _softReady = false;
        SoftBodyActive = false;
    }

    private void UpdateBag(float dt)
    {
        if (!_softReady || _bag == null)
        {
            return;
        }

        float sag = 0f;
        float minX = 999f;
        float maxX = -999f;
        float worst = 0f;
        if (_bagSample < 2)
        {
            Vector3 sample = BagPointWorld(0);
            GD.Print($"BAG sample world={sample} bag={_bag.GlobalPosition} rack={_rack.GlobalPosition}");
            _bagSample++;
        }

        if (_bagSettle > 0f)
        {
            _bagSettle -= dt;
            return;
        }

        if (!_haveRackRest)
        {
            _rackRest = new Vector3[_points];
            for (int i = 0; i < _points; i++)
            {
                Vector3 onRack = _rack.GlobalTransform.AffineInverse() * BagPointWorld(i);
                _rackRest[i] = onRack;
                minX = Mathf.Min(minX, onRack.X);
                maxX = Mathf.Max(maxX, onRack.X);
            }

            _haveRackRest = true;
            _baseWidth = -1f;
            GD.Print($"BAG rest span x={maxX - minX:0.00} bag={_bag.GlobalPosition} rack={_rack.GlobalPosition}");
            return;
        }

        for (int i = 0; i < _points; i++)
        {
            Vector3 onRack = _rack.GlobalTransform.AffineInverse() * BagPointWorld(i);
            worst = Mathf.Max(worst, onRack.DistanceTo(_rackRest[i]));
            if (!_pinned[i])
            {
                sag = Mathf.Max(sag, _rackRest[i].Y - onRack.Y);
            }

            minX = Mathf.Min(minX, onRack.X);
            maxX = Mathf.Max(maxX, onRack.X);
        }

        if (worst > 1.5f)
        {
            GD.PrintErr($"Soft cooler unstable (drift {worst:0.00}m).");
            ReleaseBag();
            return;
        }

        BagSag = Mathf.Max(sag, 0f);

        float width = maxX - minX;
        _widthDelay += dt;
        if (_widthDelay > 0.45f && width > 0.05f)
        {
            if (_baseWidth < 0f)
            {
                _baseWidth = width;
            }
            else
            {
                float lost = (_baseWidth - width) / _baseWidth;
                if (lost > 0.08f)
                {
                    Crush01 = Mathf.Max(Crush01, Mathf.Clamp(lost / 0.4f, 0f, 1f));
                    _bag.ShrinkingFactor = Mathf.Clamp(Crush01 * 0.1f, 0f, 0.12f);
                }
            }
        }
    }

    private void UpdateFood(float dt)
    {
        if (_food == null)
        {
            return;
        }

        if (_food.GlobalPosition.Y < 0.2f)
        {
            _food.GlobalPosition = _rack.GlobalPosition + new Vector3(0f, -0.22f, 0f);
            _food.LinearVelocity = Vector3.Zero;
            _food.AngularVelocity = Vector3.Zero;
            AddSpill(0.04f);
        }

        if (_food.AngularVelocity.Length() > 7f)
        {
            _food.AngularVelocity = _food.AngularVelocity.Normalized() * 7f;
        }

        float up = _food.GlobalTransform.Basis.Y.Dot(Vector3.Up);
        FoodTilt = Mathf.Acos(Mathf.Clamp(up, -1f, 1f));
        if (FoodTilt > 0.95f)
        {
            float rate = (FoodTilt - 0.5f) * (0.05f + LidOpen01 * 0.42f);
            AddSpill(rate * dt);
            if (_foodJoint != null)
            {
                _foodJoint.SetFlagX(Generic6DofJoint3D.Flag.EnableAngularSpring, false);
                _foodJoint.SetFlagZ(Generic6DofJoint3D.Flag.EnableAngularSpring, false);
                _food.AngularDamp = 3.5f;
            }
        }
    }

    private void UpdateLid(float dt)
    {
        if (_lid == null)
        {
            return;
        }

        float up = Mathf.Clamp(_lid.GlobalTransform.Basis.Y.Dot(_bike.GlobalTransform.Basis.Y), -1f, 1f);
        float angle = Mathf.Acos(up);
        if (!_lidCalibrated)
        {
            _lidCalib += dt;
            LidOpen01 = 0f;
            if (_lidCalib > 0.28f)
            {
                _lidCalibrated = true;
                if (angle > 0.18f)
                {
                    _openSign = -1f;
                    SetLidLimits(-1.65f, 0.05f);
                    _lid.AngularVelocity = Vector3.Zero;
                }
                else
                {
                    _openSign = 1f;
                    SetLidLimits(-0.05f, 1.65f);
                }
            }

            return;
        }

        LidOpen01 = Mathf.Clamp(angle / 1.05f, 0f, 1f);
        if (LidOpen01 < 0.18f)
        {
            _shutQueued = false;
            if (_lidJoint != null)
            {
                _lidJoint.SetParamX(Generic6DofJoint3D.Param.AngularSpringStiffness, 6f);
            }
        }

        if (_lidHold > 0f)
        {
            _lidHold -= dt;
            Vector3 align = _lid.GlobalTransform.Basis.Y.Cross(_bike.GlobalTransform.Basis.Y);
            _lid.ApplyTorque(align * 36f);
        }
    }

    private void RecoverFood()
    {
        if (_food == null || _foodJoint == null || FoodTilt > 0.5f)
        {
            return;
        }

        _foodJoint.SetFlagX(Generic6DofJoint3D.Flag.EnableAngularSpring, true);
        _foodJoint.SetFlagZ(Generic6DofJoint3D.Flag.EnableAngularSpring, true);
    }

    private void SetLidLimits(float lower, float upper)
    {
        if (_lidJoint == null)
        {
            return;
        }

        _lidJoint.SetFlagX(Generic6DofJoint3D.Flag.EnableAngularLimit, true);
        _lidJoint.SetFlagX(Generic6DofJoint3D.Flag.EnableAngularSpring, true);
        _lidJoint.SetParamX(Generic6DofJoint3D.Param.AngularLowerLimit, lower);
        _lidJoint.SetParamX(Generic6DofJoint3D.Param.AngularUpperLimit, upper);
        _lidJoint.SetParamX(Generic6DofJoint3D.Param.AngularSpringStiffness, 6f);
        _lidJoint.SetParamX(Generic6DofJoint3D.Param.AngularSpringDamping, 1.3f);
        _lidJoint.SetParamX(Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, 0f);
    }

    private static void LockLinear(Generic6DofJoint3D joint)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            Flag(joint, axis, Generic6DofJoint3D.Flag.EnableLinearLimit, true);
            Param(joint, axis, Generic6DofJoint3D.Param.LinearLowerLimit, 0f);
            Param(joint, axis, Generic6DofJoint3D.Param.LinearUpperLimit, 0f);
        }
    }

    private static void LockAngular(Generic6DofJoint3D joint, int axis)
    {
        Flag(joint, axis, Generic6DofJoint3D.Flag.EnableAngularLimit, true);
        Param(joint, axis, Generic6DofJoint3D.Param.AngularLowerLimit, 0f);
        Param(joint, axis, Generic6DofJoint3D.Param.AngularUpperLimit, 0f);
    }

    private static void SpringLinear(Generic6DofJoint3D joint, int axis, float limit, float stiff, float damp)
    {
        Flag(joint, axis, Generic6DofJoint3D.Flag.EnableLinearLimit, true);
        Flag(joint, axis, Generic6DofJoint3D.Flag.EnableLinearSpring, true);
        Param(joint, axis, Generic6DofJoint3D.Param.LinearLowerLimit, -limit);
        Param(joint, axis, Generic6DofJoint3D.Param.LinearUpperLimit, limit);
        Param(joint, axis, Generic6DofJoint3D.Param.LinearSpringStiffness, stiff);
        Param(joint, axis, Generic6DofJoint3D.Param.LinearSpringDamping, damp);
        Param(joint, axis, Generic6DofJoint3D.Param.LinearSpringEquilibriumPoint, 0f);
    }

    private static void SpringAngular(Generic6DofJoint3D joint, int axis, float limit, float stiff, float damp)
    {
        Flag(joint, axis, Generic6DofJoint3D.Flag.EnableAngularLimit, true);
        Flag(joint, axis, Generic6DofJoint3D.Flag.EnableAngularSpring, true);
        Param(joint, axis, Generic6DofJoint3D.Param.AngularLowerLimit, -limit);
        Param(joint, axis, Generic6DofJoint3D.Param.AngularUpperLimit, limit);
        Param(joint, axis, Generic6DofJoint3D.Param.AngularSpringStiffness, stiff);
        Param(joint, axis, Generic6DofJoint3D.Param.AngularSpringDamping, damp);
        Param(joint, axis, Generic6DofJoint3D.Param.AngularSpringEquilibriumPoint, 0f);
    }

    private static void Flag(Generic6DofJoint3D joint, int axis, Generic6DofJoint3D.Flag flag, bool enabled)
    {
        switch (axis)
        {
            case 0:
                joint.SetFlagX(flag, enabled);
                break;
            case 1:
                joint.SetFlagY(flag, enabled);
                break;
            default:
                joint.SetFlagZ(flag, enabled);
                break;
        }
    }

    private static void Param(Generic6DofJoint3D joint, int axis, Generic6DofJoint3D.Param param, float value)
    {
        switch (axis)
        {
            case 0:
                joint.SetParamX(param, value);
                break;
            case 1:
                joint.SetParamY(param, value);
                break;
            default:
                joint.SetParamZ(param, value);
                break;
        }
    }
}
