using Godot;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;

namespace Ark.Bridge.Features.Combat;

public partial class WeaponVisualSystem
{
    public void OnWeaponFired(int entityId, Vector3 origin, Vector3 direction)
    {
        if (TryGetWeaponAttachment(entityId, out var att) && att != null)
        {
            att.Muzzle.Visible = true;
            att.MuzzleTimer = MuzzleFlashTime;
        }

        SpawnBulletTrail(origin, direction);
    }

    private void SpawnBulletTrail(Vector3 origin, Vector3 direction)
    {
        if (!IsInsideTree()) return;

        BulletTrail trail;
        if (_bulletPool.Count > 0)
        {
            trail = _bulletPool[^1];
            _bulletPool.RemoveAt(_bulletPool.Count - 1);
            trail.Node.Visible = true;
        }
        else
        {
            var mesh = new BoxMesh { Size = new Vector3(0.03f, 0.03f, 0.4f) };
            mesh.Material = _bulletMat;
            trail = new BulletTrail { Node = new MeshInstance3D { Mesh = mesh } };
            AddChild(trail.Node);
        }

        if (trail.Node.IsInsideTree())
        {
            trail.Node.GlobalPosition = origin;
            trail.Node.LookAtFromPosition(origin, origin + direction.Normalized(), Vector3.Up);
        }
        else
        {
            trail.Node.Position = origin;
        }
        trail.Direction = direction.Normalized();
        trail.Speed = BulletSpeed;
        trail.Lifetime = 0;
        trail.MaxLifetime = BulletMaxLifetime;
        _bullets.Add(trail);
    }

    private void SpawnShellTrail(Vector3 origin, Vector3 direction)
    {
        if (!IsInsideTree()) return;

        var mesh = new SphereMesh { Radius = 0.25f, Height = 0.5f };
        mesh.Material = _shellMat;
        var trail = new BulletTrail { Node = new MeshInstance3D { Mesh = mesh } };
        AddChild(trail.Node);

        if (trail.Node.IsInsideTree())
        {
            trail.Node.GlobalPosition = origin;
            trail.Node.LookAtFromPosition(origin, origin + direction.Normalized(), Vector3.Up);
        }
        else
        {
            trail.Node.Position = origin;
        }
        trail.Direction = direction.Normalized();
        trail.Speed = ShellSpeed;
        trail.Lifetime = 0;
        trail.MaxLifetime = ShellMaxLifetime;
        _bullets.Add(trail);
    }

    private void UpdateBullets(float dt)
    {
        var spaceState = IsInsideTree() ? GetViewport()?.World3D?.DirectSpaceState : null;

        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            var b = _bullets[i];
            b.Lifetime += dt;

            if (b.Lifetime >= b.MaxLifetime || !b.Node.IsInsideTree())
            {
                b.Node.Visible = false;
                _bulletPool.Add(b);
                _bullets.RemoveAt(i);
                continue;
            }

            var from = b.Node.GlobalPosition;
            var step = b.Direction * b.Speed * dt;
            var to = from + step;

            if (spaceState != null)
            {
                var rayParams = PhysicsRayQueryParameters3D.Create(from, to, 1);
                var result = spaceState.IntersectRay(rayParams);
                if (result.Count > 0)
                {
                    var hitPos = (Vector3)result["position"];
                    b.Node.GlobalPosition = hitPos;
                    b.Node.Visible = false;
                    _bulletPool.Add(b);
                    _bullets.RemoveAt(i);

                    if (b.Speed <= ShellSpeed + 1f)
                        SpawnExplosionEffect(hitPos, 3f);

                    SpawnHitFlash(hitPos);
                    continue;
                }
            }

            b.Node.GlobalPosition = to;
        }
    }
}
