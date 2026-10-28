using Godot;

namespace DeliveryTemperatureWar;

public partial class ChaseCamera : Camera3D
{
    private Node3D? _target;
    private bool _snapped;

    public override void _Ready()
    {
        Current = true;
        Fov = 68f;
        Near = 0.08f;
        Far = 280f;
        _target = GetNode<Node3D>("../Bike");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_target == null)
        {
            return;
        }

        float dt = (float)delta;
        Vector3 forward = -_target.GlobalTransform.Basis.Z;
        forward.Y = 0f;
        if (forward.LengthSquared() < 0.0001f)
        {
            forward = Vector3.Forward;
        }

        forward = forward.Normalized();
        Vector3 focus = _target.GlobalPosition + Vector3.Up * 1.15f + forward * 1.4f;
        Vector3 from = _target.GlobalPosition + Vector3.Up * 1.5f;
        Vector3 want = _target.GlobalPosition - forward * 5.6f + Vector3.Up * 2.55f;
        if (GetWorld3D() != null)
        {
            var query = PhysicsRayQueryParameters3D.Create(from, want, Layers.World);
            query.CollideWithAreas = false;
            Godot.Collections.Dictionary hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
            if (hit.Count > 0)
            {
                Vector3 point = hit["position"].AsVector3();
                want = point + (from - point).Normalized() * 0.4f;
            }
        }

        if (!_snapped)
        {
            GlobalPosition = want;
            _snapped = true;
        }
        else
        {
            float blend = 1f - Mathf.Exp(-4.2f * dt);
            GlobalPosition = GlobalPosition.Lerp(want, blend);
        }

        if (GlobalPosition.DistanceSquaredTo(focus) > 0.04f)
        {
            LookAt(focus, Vector3.Up);
        }
    }
}
