using Godot;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;

namespace Ark.Bridge.Features.Combat;

public partial class WeaponVisualSystem
{
    public void AttachWeaponToCharacter(int entityId, Node3D parentNode, byte weaponCategory)
    {
        if (_weapons.ContainsKey(entityId)) DetachWeapon(entityId);

        var att = new WeaponAttachment();
        att.EntityId = entityId;
        att.WeaponCategory = weaponCategory;
        att.Root = new Node3D { Name = $"WeaponMount_{entityId}" };
        att.Root.Position = new Vector3(0.35f, 1.1f, -0.5f);
        parentNode.AddChild(att.Root);

        var (gunSize, barrelLen) = GetGunDimensions(weaponCategory);
        var gunMesh = new BoxMesh { Size = gunSize };
        gunMesh.Material = _gunMetalMat;
        att.Gun = new MeshInstance3D { Mesh = gunMesh };
        att.GunRestPos = Vector3.Zero;
        att.Root.AddChild(att.Gun);

        var barrelMesh = new CylinderMesh
        {
            TopRadius = 0.015f,
            BottomRadius = 0.02f,
            Height = barrelLen,
        };
        barrelMesh.Material = _gunMetalMat;
        att.Barrel = new MeshInstance3D { Mesh = barrelMesh };
        att.Barrel.Position = new Vector3(0, 0, -(gunSize.Z * 0.5f + barrelLen * 0.5f));
        att.Barrel.RotationDegrees = new Vector3(90, 0, 0);
        att.Root.AddChild(att.Barrel);

        var muzzleMesh = new SphereMesh { Radius = 0.08f, Height = 0.16f };
        muzzleMesh.Material = _muzzleMat;
        att.Muzzle = new MeshInstance3D { Mesh = muzzleMesh, Visible = false };
        att.Muzzle.Position = new Vector3(0, 0, -(gunSize.Z * 0.5f + barrelLen));
        att.Root.AddChild(att.Muzzle);

        att.MaintenanceAudio = new AudioStreamPlayer3D
        {
            Name = $"WeaponMaintenanceAudio_{entityId}",
            UnitSize = 6f,
            MaxDistance = 24f,
            VolumeDb = -16f,
            Stream = new AudioStreamGenerator
            {
                MixRate = 22050,
                BufferLength = 0.15f,
            }
        };
        att.Root.AddChild(att.MaintenanceAudio);

        _weapons[entityId] = att;
    }

    public void DetachWeapon(int entityId)
    {
        if (!_weapons.Remove(entityId, out var att)) return;
        att.Root.QueueFree();
    }

    private static (Vector3 gunSize, float barrelLen) GetGunDimensions(byte category)
    {
        return category switch
        {
            1 => (new Vector3(0.06f, 0.12f, 0.20f), 0.15f),
            2 => (new Vector3(0.07f, 0.14f, 0.50f), 0.35f),
            3 => (new Vector3(0.08f, 0.13f, 0.45f), 0.30f),
            4 => (new Vector3(0.07f, 0.14f, 0.65f), 0.50f),
            5 => (new Vector3(0.10f, 0.12f, 0.70f), 0.20f),
            _ => (new Vector3(0.06f, 0.10f, 0.30f), 0.20f),
        };
    }

    public void OnReloadStarted(int entityId)
    {
        if (_weapons.TryGetValue(entityId, out var att))
            att.ReloadTimer = ReloadAnimDuration;
    }

    private void UpdateWeaponAnims(float dt)
    {
        foreach (var (_, att) in _weapons)
        {
            UpdateAttachmentPose(att);

            if (att.MuzzleTimer > 0)
            {
                att.MuzzleTimer -= dt;
                if (att.MuzzleTimer <= 0)
                    att.Muzzle.Visible = false;
            }

            if (att.ReloadTimer > 0)
            {
                att.ReloadTimer -= dt;
                float t = 1f - (att.ReloadTimer / ReloadAnimDuration);
                float offset = t < 0.5f
                    ? Mathf.Lerp(0, -0.25f, t * 2f)
                    : Mathf.Lerp(-0.25f, 0, (t - 0.5f) * 2f);

                att.Gun.Position = att.GunRestPos + new Vector3(0, offset, 0);

                if (att.ReloadTimer <= 0)
                    att.Gun.Position = att.GunRestPos;
            }

            if (att.MaintenanceTimer > 0)
            {
                att.MaintenanceTimer -= dt;
                float t = 1f - (att.MaintenanceTimer / MaintenanceAnimDuration);
                float wobble = Mathf.Sin(t * Mathf.Pi * 4f) * 0.05f;
                att.Gun.RotationDegrees = new Vector3(0f, wobble * 120f, wobble * 55f);
                PumpMaintenanceAudio(att, dt, 240f + Mathf.Sin(t * Mathf.Pi * 3f) * 60f);

                if (att.MaintenanceTimer <= 0f)
                {
                    att.Gun.RotationDegrees = Vector3.Zero;
                    att.MaintenanceAudio?.Stop();
                }
            }
            else
            {
                att.Gun.RotationDegrees = att.Gun.RotationDegrees.Lerp(Vector3.Zero, 0.18f);
            }
        }
    }

    private static void PumpMaintenanceAudio(WeaponAttachment att, float dt, float frequency)
    {
        if (att.MaintenanceAudio is null)
            return;

        if (!att.MaintenanceAudio.Playing)
            att.MaintenanceAudio.Play();

        if (att.MaintenanceAudio.GetStreamPlayback() is not AudioStreamGeneratorPlayback playback)
            return;

        int frames = Mathf.Clamp((int)(22050 * dt), 48, 512);
        for (int i = 0; i < frames && playback.CanPushBuffer(1); i++)
        {
            att.MaintenanceAudioPhase += frequency / 22050f;
            if (att.MaintenanceAudioPhase >= 1f)
                att.MaintenanceAudioPhase -= 1f;

            float sample = Mathf.Sin(att.MaintenanceAudioPhase * Mathf.Tau) * 0.08f;
            playback.PushFrame(new Vector2(sample, sample));
        }
    }

    public void OnMaintenanceStarted(int entityId)
    {
        if (_weapons.TryGetValue(entityId, out var att))
            att.MaintenanceTimer = MaintenanceAnimDuration;
    }
}
