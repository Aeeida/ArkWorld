using Ark.Ecs.Components;
using Friflo.Engine.ECS;
using Godot;

namespace Ark.Bridge.Features.BaseBuilding;

public partial class BuildingVisualManager
{
    private static void CreateDamageVisuals(BuildingVisual visual, BuildingDef.Def def)
    {
        var damageMesh = new BoxMesh { Size = def.Size * 1.02f };
        visual.DamageShellMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.18f, 0.16f, 0.16f, 0f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
            Metallic = 0.05f,
            Roughness = 0.95f,
        };
        damageMesh.Material = visual.DamageShellMat;
        visual.DamageShell = new MeshInstance3D { Mesh = damageMesh, Visible = false };
        visual.DamageShell.Position = new Vector3(0, def.Size.Y * 0.5f, 0);
        visual.Root.AddChild(visual.DamageShell);

        var repairMesh = new BoxMesh { Size = new Vector3(def.Size.X * 1.08f, def.Size.Y * 1.04f, def.Size.Z * 1.08f) };
        visual.RepairAuraMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.28f, 0.95f, 0.55f, 0f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            EmissionEnabled = true,
            Emission = new Color(0.22f, 0.85f, 0.5f),
            EmissionEnergyMultiplier = 0f,
        };
        repairMesh.Material = visual.RepairAuraMat;
        visual.RepairAura = new MeshInstance3D { Mesh = repairMesh, Visible = false };
        visual.RepairAura.Position = new Vector3(0, def.Size.Y * 0.5f, 0);
        visual.Root.AddChild(visual.RepairAura);

        visual.DamageZonesRoot = new Node3D { Name = "DamageZones" };
        visual.Root.AddChild(visual.DamageZonesRoot);
        CreateDamageZones(visual, def);

        visual.DamageInstancesRoot = new Node3D { Name = "DamageInstances" };
        visual.Root.AddChild(visual.DamageInstancesRoot);
        CreateDamageInstances(visual);
    }

    private static void ApplyDamageVisual(Entity entity, BuildingVisual visual, Health health, byte constructionProgress, float deltaTime, BuildingDamageState? persistentDamage, BuildingDamageInstanceState? damageInstances, BuildingDamageFeedbackState? directionalFeedback, BuildingDamageEcsAuthority? damageAuth)
    {
        float healthRatio = health.Max > 0f
            ? Mathf.Clamp(health.Current / health.Max, 0f, 1f)
            : 1f;
        bool isComplete = constructionProgress >= 100;

        if (healthRatio > visual.LastHealthRatio + 0.02f)
            visual.RepairPulse = Mathf.Max(visual.RepairPulse, 0.65f);

        visual.LastHealthRatio = healthRatio;
        visual.RepairPulse = Mathf.Max(0f, visual.RepairPulse - deltaTime);

        float damageBlend = isComplete ? 1f - healthRatio : 0f;
        bool showDamage = damageBlend > 0.08f;
        float severeDamage = Mathf.Clamp((damageBlend - 0.4f) / 0.6f, 0f, 1f);

        visual.DamageShell.Visible = showDamage;
        if (showDamage)
        {
            visual.DamageShell.Scale = new Vector3(1f + damageBlend * 0.025f, 1f, 1f + damageBlend * 0.025f);
            visual.DamageShellMat.AlbedoColor = new Color(0.20f + severeDamage * 0.08f, 0.12f, 0.10f, 0.18f + damageBlend * 0.52f);
            visual.DamageShellMat.EmissionEnabled = severeDamage > 0.2f;
            visual.DamageShellMat.Emission = new Color(0.35f + severeDamage * 0.3f, 0.14f, 0.08f);
            visual.DamageShellMat.EmissionEnergyMultiplier = severeDamage * 1.8f;
            visual.BodyMat.Roughness = 0.45f + damageBlend * 0.35f;
            visual.RoofMat.Roughness = 0.35f + damageBlend * 0.45f;
        }
        else
        {
            visual.BodyMat.Roughness = 0.35f;
            visual.RoofMat.Roughness = 0.25f;
        }

        bool showRepair = isComplete && visual.RepairPulse > 0f && healthRatio < 0.995f;
        visual.RepairAura.Visible = showRepair;
        if (showRepair)
        {
            float repairAlpha = Mathf.Clamp(visual.RepairPulse / 0.65f, 0f, 1f);
            visual.RepairAura.Scale = Vector3.One * (1.0f + repairAlpha * 0.06f);
            visual.RepairAuraMat.AlbedoColor = new Color(0.28f, 0.95f, 0.55f, repairAlpha * 0.24f);
            visual.RepairAuraMat.EmissionEnergyMultiplier = 0.8f + repairAlpha * 1.8f;
        }
        else
        {
            visual.RepairAuraMat.EmissionEnergyMultiplier = 0f;
        }

        ApplyDamageZones(entity, visual, damageBlend, severeDamage, deltaTime, persistentDamage, directionalFeedback, damageAuth);
        ApplyDamageInstances(visual, damageInstances, severeDamage, deltaTime);
    }

    private static void CreateDamageZones(BuildingVisual visual, BuildingDef.Def def)
    {
        AddDamageZone(visual, 0, new Vector3(0, def.Size.Y * 0.48f, def.Size.Z * 0.48f), new Vector3(def.Size.X * 0.88f, def.Size.Y * 0.92f, 0.08f), 0.22f, Vector3.Forward);
        AddDamageZone(visual, 1, new Vector3(0, def.Size.Y * 0.48f, -def.Size.Z * 0.48f), new Vector3(def.Size.X * 0.88f, def.Size.Y * 0.92f, 0.08f), 0.42f, Vector3.Back);
        AddDamageZone(visual, 2, new Vector3(def.Size.X * 0.48f, def.Size.Y * 0.48f, 0), new Vector3(0.08f, def.Size.Y * 0.92f, def.Size.Z * 0.88f), 0.62f, Vector3.Right);
        AddDamageZone(visual, 3, new Vector3(-def.Size.X * 0.48f, def.Size.Y * 0.48f, 0), new Vector3(0.08f, def.Size.Y * 0.92f, def.Size.Z * 0.88f), 0.78f, Vector3.Left);
    }

    private static void AddDamageZone(BuildingVisual visual, int zoneIndex, Vector3 localPosition, Vector3 size, float threshold, Vector3 normal)
    {
        var mesh = new BoxMesh { Size = size };
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.22f, 0.12f, 0.10f, 0f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            EmissionEnabled = true,
            Emission = new Color(0.45f, 0.16f, 0.08f),
            EmissionEnergyMultiplier = 0f,
        };
        mesh.Material = material;

        var node = new MeshInstance3D
        {
            Mesh = mesh,
            Position = localPosition,
            Visible = false,
        };
        node.SetMeta("damage_threshold", threshold);
        node.SetMeta("zone_index", zoneIndex);
        node.SetMeta("zone_normal", normal);
        visual.DamageZones.Add(node);
        visual.DamageZonesRoot.AddChild(node);
    }

    private static void CreateDamageInstances(BuildingVisual visual)
    {
        for (int i = 0; i < 3; i++)
        {
            var root = new Node3D { Name = $"DamageCluster_{i}", Visible = false };

            var crack = new MeshInstance3D
            {
                Name = "CrackDecal",
                Mesh = new QuadMesh { Size = new Vector2(0.8f, 0.8f) },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.08f, 0.08f, 0.08f, 0.65f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    BillboardMode = BaseMaterial3D.BillboardModeEnum.Disabled,
                },
            };
            root.AddChild(crack);

            var chip = new MeshInstance3D
            {
                Name = "DamageChip",
                Mesh = new BoxMesh { Size = new Vector3(0.18f, 0.12f, 0.08f) },
                Position = new Vector3(0, 0, -0.04f),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.28f, 0.18f, 0.14f),
                    Metallic = 0.08f,
                    Roughness = 0.92f,
                },
            };
            root.AddChild(chip);

            for (int shardIndex = 0; shardIndex < 3; shardIndex++)
            {
                var shard = new MeshInstance3D
                {
                    Name = $"DebrisShard_{shardIndex}",
                    Mesh = new BoxMesh { Size = new Vector3(0.05f + shardIndex * 0.015f, 0.03f + shardIndex * 0.01f, 0.04f) },
                    MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoColor = new Color(0.24f, 0.16f, 0.12f),
                        Metallic = 0.05f,
                        Roughness = 0.94f,
                    },
                };
                root.AddChild(shard);
            }

            visual.DamageInstanceRoots.Add(root);
            visual.DamageInstancesRoot.AddChild(root);
        }
    }

    private static void ApplyDamageInstances(BuildingVisual visual, BuildingDamageInstanceState? damageInstances, float severeDamage, float deltaTime)
    {
        for (int i = 0; i < visual.DamageInstanceRoots.Count; i++)
        {
            var root = visual.DamageInstanceRoots[i];
            if (root.GetChildCount() < 2)
                continue;

            Vector4 cluster = i switch
            {
                0 when damageInstances is { } di => new Vector4(di.Cluster0X, di.Cluster0Y, di.Cluster0Z, di.Cluster0Strength),
                1 when damageInstances is { } di => new Vector4(di.Cluster1X, di.Cluster1Y, di.Cluster1Z, di.Cluster1Strength),
                2 when damageInstances is { } di => new Vector4(di.Cluster2X, di.Cluster2Y, di.Cluster2Z, di.Cluster2Strength),
                _ => Vector4.Zero,
            };
            float clusterAge = i switch
            {
                0 when damageInstances is { } di => di.Cluster0Age,
                1 when damageInstances is { } di => di.Cluster1Age,
                2 when damageInstances is { } di => di.Cluster2Age,
                _ => 0f,
            };
            float repairFill = i switch
            {
                0 when damageInstances is { } di => di.Cluster0RepairFill,
                1 when damageInstances is { } di => di.Cluster1RepairFill,
                2 when damageInstances is { } di => di.Cluster2RepairFill,
                _ => 0f,
            };
            uint packedLayers = damageInstances?.PackedLayerState ?? 0u;
            float damageLayer = (packedLayers >> (i * 2)) & 0x3u;

            bool visible = cluster.W > 0.03f;
            root.Visible = visible;
            if (!visible)
                continue;

            var localPos = new Vector3(cluster.X, cluster.Y, cluster.Z);
            var normal = ResolveDamageNormal(localPos);
            root.Position = localPos + normal * 0.04f;
            root.RotationDegrees = NormalToRotationDegrees(normal);
            root.Scale = Vector3.One * (0.75f + cluster.W * 0.8f);
            float ageFade = 1f - Mathf.Clamp(clusterAge / 12f, 0f, 0.75f);

            if (root.GetNodeOrNull<MeshInstance3D>("CrackDecal") is { } crack
                && crack.MaterialOverride is StandardMaterial3D crackMat)
            {
                crackMat.AlbedoColor = new Color(
                    0.08f + severeDamage * 0.04f + repairFill * 0.08f,
                    0.08f + repairFill * 0.12f,
                    0.08f + repairFill * 0.08f,
                    (0.18f + cluster.W * 0.55f) * ageFade * (1f - repairFill * 0.55f));
                crack.Scale = new Vector3(1f + cluster.W + damageLayer * 0.12f - repairFill * 0.25f, 1f + cluster.W + damageLayer * 0.12f - repairFill * 0.25f, 1f);
            }

            if (root.GetNodeOrNull<MeshInstance3D>("DamageChip") is { } chip)
            {
                chip.Position = chip.Position.Lerp(new Vector3(0, 0, -0.04f - cluster.W * 0.06f), deltaTime * 6f);
                chip.RotationDegrees = new Vector3(cluster.W * 28f, i * 37f, cluster.W * 14f);
                chip.Scale = Vector3.One * (0.8f + cluster.W * 0.9f);
            }

            for (int shardIndex = 0; shardIndex < 3; shardIndex++)
            {
                if (root.GetNodeOrNull<MeshInstance3D>($"DebrisShard_{shardIndex}") is not { } shard)
                    continue;

                float spread = 0.08f + shardIndex * 0.04f + cluster.W * 0.12f;
                Vector3 tangent = Mathf.Abs(normal.Y) > 0.8f ? Vector3.Right : Vector3.Up;
                Vector3 bitangent = normal.Cross(tangent).Normalized();
                tangent = bitangent.Cross(normal).Normalized();
                Vector3 shardOffset = tangent * ((shardIndex - 1) * spread) + bitangent * (0.04f + shardIndex * 0.02f);
                Vector3 targetShardPos = shardOffset * ageFade - normal * (0.02f + cluster.W * 0.04f) * (1f - repairFill * 0.8f);
                shard.Position = shard.Position.Lerp(targetShardPos, deltaTime * 8f);
                shard.RotationDegrees = new Vector3(18f + cluster.W * 35f + shardIndex * 12f, shardIndex * 33f, cluster.W * 24f);
                shard.Scale = Vector3.One * ((0.65f + cluster.W * 0.75f + damageLayer * 0.08f) * ageFade * (1f - repairFill * 0.7f));
                shard.Visible = repairFill < 0.9f && ageFade > 0.12f;
            }
        }
    }

    private static Vector3 ResolveDamageNormal(Vector3 localPos)
    {
        float ax = Mathf.Abs(localPos.X);
        float ay = Mathf.Abs(localPos.Y - 1.0f);
        float az = Mathf.Abs(localPos.Z);

        if (ax > az && ax > ay)
            return localPos.X >= 0f ? Vector3.Right : Vector3.Left;
        if (ay > ax && ay > az)
            return localPos.Y >= 1.0f ? Vector3.Up : Vector3.Down;
        return localPos.Z >= 0f ? Vector3.Forward : Vector3.Back;
    }

    private static Vector3 NormalToRotationDegrees(Vector3 normal)
    {
        if (normal == Vector3.Right) return new Vector3(0f, -90f, 0f);
        if (normal == Vector3.Left) return new Vector3(0f, 90f, 0f);
        if (normal == Vector3.Back) return new Vector3(0f, 180f, 0f);
        if (normal == Vector3.Up) return new Vector3(-90f, 0f, 0f);
        if (normal == Vector3.Down) return new Vector3(90f, 0f, 0f);
        return Vector3.Zero;
    }

    private static void ApplyDamageZones(Entity entity, BuildingVisual visual, float damageBlend, float severeDamage, float deltaTime, BuildingDamageState? persistentDamage, BuildingDamageFeedbackState? directionalFeedback, BuildingDamageEcsAuthority? damageAuth)
    {
        float directionalTimer = 0f;
        Vector3 hitDir = Vector3.Zero;
        float directionalStrength = 0f;
        if (directionalFeedback is { } feedback)
        {
            directionalTimer = Mathf.Max(0f, feedback.PulseTimer - deltaTime);
            hitDir = new Vector3(feedback.HitDirX, feedback.HitDirY, feedback.HitDirZ);
            directionalStrength = feedback.Strength;
            feedback.PulseTimer = directionalTimer;
            feedback.Strength = directionalTimer > 0f ? feedback.Strength : 0f;
            damageAuth?.WriteFeedback(entity, feedback);
        }

        foreach (var zone in visual.DamageZones)
        {
            float threshold = zone.HasMeta("damage_threshold") ? zone.GetMeta("damage_threshold").AsSingle() : 0.3f;
            int zoneIndex = zone.HasMeta("zone_index") ? zone.GetMeta("zone_index").AsInt32() : 0;
            float persistentZoneDamage = persistentDamage is { } persistent
                ? zoneIndex switch
                {
                    0 => persistent.FrontDamage / 100f,
                    1 => persistent.BackDamage / 100f,
                    2 => persistent.RightDamage / 100f,
                    _ => persistent.LeftDamage / 100f,
                }
                : 0f;
            bool visible = damageBlend >= threshold || persistentZoneDamage > 0.05f;
            zone.Visible = visible;
            if (!visible || zone.Mesh is not PrimitiveMesh primitive || primitive.Material is not StandardMaterial3D mat)
                continue;

            float localDamage = Mathf.Max(
                Mathf.Clamp((damageBlend - threshold) / 0.25f, 0f, 1f),
                persistentZoneDamage);
            float directionalBoost = 0f;
            if (directionalTimer > 0f && zone.GetMeta("zone_normal").VariantType == Variant.Type.Vector3)
            {
                var zoneNormal = zone.GetMeta("zone_normal").AsVector3();
                directionalBoost = Mathf.Clamp(zoneNormal.Dot(hitDir), 0f, 1f) * directionalStrength * (directionalTimer / 0.75f);
            }

            mat.AlbedoColor = new Color(0.20f + severeDamage * 0.1f + directionalBoost * 0.08f, 0.10f, 0.08f, 0.18f + localDamage * 0.5f + directionalBoost * 0.18f);
            mat.EmissionEnergyMultiplier = 0.3f + localDamage * 1.8f + directionalBoost * 2.2f;
            zone.Scale = Vector3.One * (1.0f + localDamage * 0.03f + directionalBoost * 0.04f);
        }
    }
}
