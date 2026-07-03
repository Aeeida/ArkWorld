using Godot;

namespace Ark.Bridge.Features.BaseBuilding;

public partial class BuildingVisualManager
{
    private static void ApplyUpgradeVisual(BuildingVisual visual, byte level, byte constructionProgress)
    {
        visual.Level = level;

        float completion = constructionProgress / 100f;
        float upgradeBlend = Mathf.Clamp((level - 1) / 4.0f, 0f, 1f);
        bool allowUpgradeFx = completion >= 0.999f && level > 1;

        var def = BuildingDef.Get(visual.TypeId);
        if (def is null)
            return;

        var bodyColor = def.Value.BodyColor.Lerp(def.Value.RoofColor.Lightened(0.18f), upgradeBlend * 0.55f);
        var roofColor = def.Value.RoofColor.Lerp(Colors.White, upgradeBlend * 0.18f);
        visual.BodyMat.AlbedoColor = bodyColor;
        visual.RoofMat.AlbedoColor = roofColor;
        if (visual.Foundation.Mesh is BoxMesh foundationMesh && foundationMesh.Material is StandardMaterial3D foundationMat)
            foundationMat.AlbedoColor = BuildingDef.FoundationColor.Lerp(def.Value.BodyColor.Lightened(0.1f), upgradeBlend * 0.35f);

        visual.UpgradeBand.Visible = allowUpgradeFx;
        if (allowUpgradeFx)
        {
            visual.UpgradeBand.Scale = new Vector3(1f + upgradeBlend * 0.08f, 1f + upgradeBlend * 0.45f, 1f + upgradeBlend * 0.08f);
            visual.UpgradeBand.Position = new Vector3(0, def.Value.Size.Y * (0.48f + upgradeBlend * 0.18f), 0);
            visual.UpgradeBandMat.AlbedoColor = def.Value.RoofColor.Lightened(0.10f + upgradeBlend * 0.35f) with { A = 0.55f + upgradeBlend * 0.25f };
            visual.UpgradeBandMat.EmissionEnabled = level >= 3;
            visual.UpgradeBandMat.Emission = def.Value.RoofColor.Lightened(0.40f);
            visual.UpgradeBandMat.EmissionEnergyMultiplier = level >= 3 ? 0.8f + upgradeBlend * 1.2f : 0f;
        }

        visual.UpgradeBeacon.Visible = allowUpgradeFx && level >= 3;
        if (visual.UpgradeBeacon.Visible)
        {
            float beaconPulse = 0.9f + upgradeBlend * 0.3f;
            visual.UpgradeBeacon.Scale = new Vector3(beaconPulse, 1f + upgradeBlend * 0.5f, beaconPulse);
            visual.UpgradeBeaconMat.AlbedoColor = def.Value.RoofColor.Lightened(0.2f + upgradeBlend * 0.45f) with { A = 0.35f + upgradeBlend * 0.35f };
            visual.UpgradeBeaconMat.Emission = def.Value.RoofColor.Lightened(0.45f + upgradeBlend * 0.35f);
            visual.UpgradeBeaconMat.EmissionEnergyMultiplier = 1.2f + upgradeBlend * 2.5f;
        }

        ApplyTypeSpecificUpgradePieces(visual, def.Value, level, allowUpgradeFx, upgradeBlend);
    }

    private static void CreateTypeSpecificUpgradePieces(BuildingVisual visual, BuildingDef.Def def)
    {
        switch (def.TypeId)
        {
            case 1:
                CreateWallUpgradePieces(visual, def);
                break;
            case 2:
                CreateTowerUpgradePieces(visual, def);
                break;
            case 3:
                CreateStorageUpgradePieces(visual, def);
                break;
            case 4:
                CreateBarracksUpgradePieces(visual, def);
                break;
            case 5:
                CreateRocketPadUpgradePieces(visual, def);
                break;
            case 6:
                CreateTankFactoryUpgradePieces(visual, def);
                break;
        }
    }

    private static void ApplyTypeSpecificUpgradePieces(BuildingVisual visual, BuildingDef.Def def, byte level, bool allowUpgradeFx, float upgradeBlend)
    {
        foreach (Node child in visual.UpgradePiecesRoot.GetChildren())
        {
            if (child is not Node3D piece)
                continue;

            int unlockLevel = piece.HasMeta("unlock_level") ? (int)piece.GetMeta("unlock_level") : 2;
            bool visible = allowUpgradeFx && level >= unlockLevel;
            piece.Visible = visible;
            if (!visible)
                continue;

            float localBlend = Mathf.Clamp((level - unlockLevel + 1) / 3.0f, 0f, 1f);
            piece.Scale = piece.Scale.Lerp(Vector3.One * (0.85f + localBlend * 0.25f), 0.18f);
            piece.RotationDegrees = piece.RotationDegrees.Lerp(new Vector3(0f, unlockLevel * 12f, 0f), 0.12f);

            if (piece is MeshInstance3D mesh && mesh.Mesh is PrimitiveMesh primitive && primitive.Material is StandardMaterial3D mat)
            {
                var baseColor = def.RoofColor.Lightened(0.08f + upgradeBlend * 0.28f + localBlend * 0.12f);
                mat.AlbedoColor = baseColor with { A = 0.78f + localBlend * 0.18f };
                mat.EmissionEnabled = unlockLevel >= 3;
                mat.Emission = def.RoofColor.Lightened(0.35f + upgradeBlend * 0.25f);
                mat.EmissionEnergyMultiplier = unlockLevel >= 3 ? 1.1f + localBlend * 1.4f : 0f;
            }
        }
    }

    private static void CreateWallUpgradePieces(BuildingVisual visual, BuildingDef.Def def)
    {
        AddUpgradePiece(visual, 2, new BoxMesh { Size = new Vector3(def.Size.X * 0.95f, 0.22f, 0.18f) }, new Vector3(0, def.Size.Y * 0.55f, 0));
        AddUpgradePiece(visual, 4, new BoxMesh { Size = new Vector3(def.Size.X * 0.45f, 0.32f, 0.2f) }, new Vector3(0, def.Size.Y * 0.92f, 0));
    }

    private static void CreateTowerUpgradePieces(BuildingVisual visual, BuildingDef.Def def)
    {
        AddUpgradePiece(visual, 2, new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.16f, Height = 1.2f }, new Vector3(0, def.Size.Y + 0.65f, 0));
        AddUpgradePiece(visual, 3, new BoxMesh { Size = new Vector3(def.Size.X * 1.05f, 0.18f, def.Size.Z * 1.05f) }, new Vector3(0, def.Size.Y * 0.78f, 0));
        AddUpgradePiece(visual, 4, new SphereMesh { Radius = 0.28f, Height = 0.56f }, new Vector3(0, def.Size.Y + 1.35f, 0));
    }

    private static void CreateStorageUpgradePieces(BuildingVisual visual, BuildingDef.Def def)
    {
        AddUpgradePiece(visual, 2, new BoxMesh { Size = new Vector3(def.Size.X * 0.18f, def.Size.Y * 0.75f, def.Size.Z * 0.18f) }, new Vector3(-def.Size.X * 0.32f, def.Size.Y * 0.45f, -def.Size.Z * 0.28f));
        AddUpgradePiece(visual, 3, new BoxMesh { Size = new Vector3(def.Size.X * 0.18f, def.Size.Y * 0.75f, def.Size.Z * 0.18f) }, new Vector3(def.Size.X * 0.32f, def.Size.Y * 0.45f, -def.Size.Z * 0.28f));
        AddUpgradePiece(visual, 4, new BoxMesh { Size = new Vector3(def.Size.X * 0.7f, 0.14f, def.Size.Z * 0.16f) }, new Vector3(0, def.Size.Y * 0.78f, def.Size.Z * 0.34f));
    }

    private static void CreateBarracksUpgradePieces(BuildingVisual visual, BuildingDef.Def def)
    {
        AddUpgradePiece(visual, 2, new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.14f, Height = 1.0f }, new Vector3(-def.Size.X * 0.22f, def.Size.Y + 0.4f, 0));
        AddUpgradePiece(visual, 3, new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.14f, Height = 1.0f }, new Vector3(def.Size.X * 0.22f, def.Size.Y + 0.4f, 0));
        AddUpgradePiece(visual, 4, new BoxMesh { Size = new Vector3(def.Size.X * 0.45f, 0.2f, 0.25f) }, new Vector3(0, def.Size.Y * 0.88f, def.Size.Z * 0.36f));
    }

    private static void CreateRocketPadUpgradePieces(BuildingVisual visual, BuildingDef.Def def)
    {
        AddUpgradePiece(visual, 2, new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.12f, Height = 1.4f }, new Vector3(-def.Size.X * 0.35f, 0.72f, -def.Size.Z * 0.35f));
        AddUpgradePiece(visual, 2, new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.12f, Height = 1.4f }, new Vector3(def.Size.X * 0.35f, 0.72f, -def.Size.Z * 0.35f));
        AddUpgradePiece(visual, 3, new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.12f, Height = 1.4f }, new Vector3(-def.Size.X * 0.35f, 0.72f, def.Size.Z * 0.35f));
        AddUpgradePiece(visual, 3, new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.12f, Height = 1.4f }, new Vector3(def.Size.X * 0.35f, 0.72f, def.Size.Z * 0.35f));
        AddUpgradePiece(visual, 4, new BoxMesh { Size = new Vector3(def.Size.X * 0.12f, 2.2f, def.Size.Z * 0.12f) }, new Vector3(0, 1.1f, 0));
    }

    private static void CreateTankFactoryUpgradePieces(BuildingVisual visual, BuildingDef.Def def)
    {
        AddUpgradePiece(visual, 2, new BoxMesh { Size = new Vector3(def.Size.X * 0.16f, def.Size.Y * 0.45f, def.Size.Z * 0.16f) }, new Vector3(-def.Size.X * 0.34f, def.Size.Y * 0.72f, 0));
        AddUpgradePiece(visual, 3, new BoxMesh { Size = new Vector3(def.Size.X * 0.16f, def.Size.Y * 0.45f, def.Size.Z * 0.16f) }, new Vector3(def.Size.X * 0.34f, def.Size.Y * 0.72f, 0));
        AddUpgradePiece(visual, 4, new CylinderMesh { TopRadius = 0.16f, BottomRadius = 0.2f, Height = 1.6f }, new Vector3(0, def.Size.Y + 0.8f, -def.Size.Z * 0.28f));
    }

    private static void AddUpgradePiece(BuildingVisual visual, int unlockLevel, PrimitiveMesh mesh, Vector3 localPosition)
    {
        var material = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel,
            Metallic = 0.35f,
            Roughness = 0.45f,
        };
        mesh.Material = material;

        var node = new MeshInstance3D
        {
            Mesh = mesh,
            Position = localPosition,
            Visible = false,
            Scale = Vector3.One * 0.85f,
        };
        node.SetMeta("unlock_level", unlockLevel);
        visual.UpgradePiecesRoot.AddChild(node);
    }
}
