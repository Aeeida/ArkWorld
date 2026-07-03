using Godot;

namespace Ark.Bridge.Features.Combat;

public partial class WeaponVisualSystem
{
    public void NotifyHit(Vector3 hitPos)
    {
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            var b = _bullets[i];
            if (!b.Node.IsInsideTree()) continue;
            float distSq = (b.Node.GlobalPosition - hitPos).LengthSquared();
            if (distSq < 9f)
            {
                b.Node.Visible = false;
                _bulletPool.Add(b);
                _bullets.RemoveAt(i);
            }
        }

        SpawnHitFlash(hitPos);
    }

    private void SpawnHitFlash(Vector3 hitPos)
    {
        if (!IsInsideTree()) return;

        HitFlash flash;
        if (_hitFlashPool.Count > 0)
        {
            flash = _hitFlashPool[^1];
            _hitFlashPool.RemoveAt(_hitFlashPool.Count - 1);
            flash.Sphere.Visible = true;
        }
        else
        {
            var mesh = new SphereMesh { Radius = 0.3f, Height = 0.6f };
            mesh.Material = _hitFlashMat;
            flash = new HitFlash { Sphere = new MeshInstance3D { Mesh = mesh } };
            AddChild(flash.Sphere);
        }

        if (flash.Sphere.IsInsideTree())
            flash.Sphere.GlobalPosition = hitPos;
        else
            flash.Sphere.Position = hitPos;
        flash.Timer = 0.15f;
        _hitFlashes.Add(flash);
    }

    private void UpdateHitFlashes(float dt)
    {
        for (int i = _hitFlashes.Count - 1; i >= 0; i--)
        {
            var f = _hitFlashes[i];
            f.Timer -= dt;
            if (f.Timer <= 0)
            {
                f.Sphere.Visible = false;
                _hitFlashPool.Add(f);
                _hitFlashes.RemoveAt(i);
            }
            else
            {
                float scale = f.Timer / 0.15f;
                f.Sphere.Scale = Vector3.One * (0.5f + scale * 0.5f);
            }
        }
    }

    private void UpdateExplosions(float dt)
    {
        for (int i = _explosions.Count - 1; i >= 0; i--)
        {
            var e = _explosions[i];
            e.Timer += dt;

            if (e.Timer >= e.MaxTimer)
            {
                e.Sphere.QueueFree();
                _explosions.RemoveAt(i);
                continue;
            }

            float t = e.Timer / e.MaxTimer;
            float scaleT = 1f - (1f - t) * (1f - t);
            float scale = scaleT * e.MaxScale;
            e.Sphere.Scale = Vector3.One * Mathf.Max(scale, 0.1f);

            if (e.Sphere.Mesh is SphereMesh sm && sm.Material is StandardMaterial3D mat)
            {
                float alpha = 1f - t;
                mat.AlbedoColor = mat.AlbedoColor with { A = alpha * 0.85f };
            }
        }
    }
}
