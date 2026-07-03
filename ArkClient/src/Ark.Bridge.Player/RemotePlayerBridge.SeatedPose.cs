using Ark.Shared.Data;
using Ark.Ecs.Components;
using Friflo.Engine.ECS;
using Godot;

namespace Ark.Bridge.Player;

public sealed partial class RemotePlayerBridge
{
    private static Node ResolveSeatPresentationParent(Node3D vehicleNode, int seatIndex, SeatType seatType, bool hasMountedWeapon)
    {
        if (seatType == SeatType.Gunner && hasMountedWeapon)
        {
            if (vehicleNode.GetNodeOrNull<Node3D>($"Visual/TurretPivot_Seat{seatIndex}") is { } seatPivot)
                return seatPivot;
            if (vehicleNode.GetNodeOrNull<Node3D>("Visual/TurretPivot") is { } turretPivot)
                return turretPivot;
        }

        return vehicleNode;
    }

    private static EntityType GetVehiclePresentationType(Node3D vehicleNode)
    {
        if (vehicleNode.HasMeta("entity_type") && vehicleNode.GetMeta("entity_type").VariantType == Variant.Type.String)
        {
            var typeName = vehicleNode.GetMeta("entity_type").AsString();
            if (System.Enum.TryParse<EntityType>(typeName, out var entityType))
                return entityType;
        }

        return EntityType.Vehicle;
    }

    private static Vector3 GetSeatAimRotationDegrees(Entity occupantEntity, Node seatParent)
    {
        if (!occupantEntity.IsNull && occupantEntity.TryGetComponent<TurretState>(out var turret))
            return new Vector3(Mathf.RadToDeg(turret.Pitch), Mathf.RadToDeg(turret.Yaw), 0f);

        return seatParent is Node3D node3D
            ? node3D.RotationDegrees
            : Vector3.Zero;
    }

    private static void ApplyStandingPresentationPose(Node3D node, double delta)
    {
        if (node.GetNodeOrNull<Node3D>("Visual") is { } visual)
        {
            visual.Position = visual.Position.Lerp(Vector3.Zero, (float)(10.0 * delta));
            visual.RotationDegrees = visual.RotationDegrees.Lerp(Vector3.Zero, (float)(10.0 * delta));
            visual.Scale = visual.Scale.Lerp(Vector3.One, (float)(10.0 * delta));

            if (visual.GetNodeOrNull<Node3D>("UpperBodyPivot") is { } upperBody)
                upperBody.RotationDegrees = upperBody.RotationDegrees.Lerp(Vector3.Zero, (float)(10.0 * delta));
            if (visual.GetNodeOrNull<Node3D>("UpperBodyPivot/HeadPivot") is { } head)
                head.RotationDegrees = head.RotationDegrees.Lerp(Vector3.Zero, (float)(10.0 * delta));
        }

        if (node.GetNodeOrNull<Label3D>("NameLabel") is { } label)
            label.Position = label.Position.Lerp(new Vector3(0, 2.1f, 0), (float)(10.0 * delta));

        if (node.GetNodeOrNull<StaticBody3D>("Collider") is { } collider
            && collider.GetChildCount() > 0
            && collider.GetChild(0) is CollisionShape3D collisionShape)
            collisionShape.Position = collisionShape.Position.Lerp(new Vector3(0, 0.95f, 0), (float)(10.0 * delta));
    }

    private static void ApplySeatedPresentationPose(Node3D node, SeatType seatType, bool hasMountedWeapon, EntityType vehicleType, Vector3 aimRotationDegrees, double delta)
    {
        if (node.GetNodeOrNull<Node3D>("Visual") is { } visual)
        {
            Vector3 targetPos;
            Vector3 targetRot;
            Vector3 targetScale;

            switch (seatType)
            {
                case SeatType.Driver:
                    targetPos = vehicleType == EntityType.Spacecraft
                        ? new Vector3(0f, -0.42f, 0.08f)
                        : hasMountedWeapon ? new Vector3(0f, -0.72f, 0.15f) : new Vector3(0.02f, -0.66f, 0.12f);
                    targetRot = vehicleType == EntityType.Spacecraft
                        ? new Vector3(18f, aimRotationDegrees.Y * 0.12f, -6f)
                        : new Vector3(78f, aimRotationDegrees.Y * 0.08f, 0f);
                    targetScale = new Vector3(1.0f, 0.68f, 1.08f);
                    break;
                case SeatType.Gunner:
                    targetPos = vehicleType == EntityType.Spacecraft
                        ? new Vector3(0f, -0.2f, -0.18f)
                        : hasMountedWeapon ? new Vector3(0f, -0.38f, -0.08f) : new Vector3(0f, -0.54f, 0.05f);
                    targetRot = vehicleType == EntityType.Spacecraft
                        ? new Vector3(20f - aimRotationDegrees.X * 0.18f, aimRotationDegrees.Y * 0.18f, -8f)
                        : hasMountedWeapon
                            ? new Vector3(52f - aimRotationDegrees.X * 0.22f, aimRotationDegrees.Y * 0.15f, 0f)
                            : new Vector3(70f - aimRotationDegrees.X * 0.12f, aimRotationDegrees.Y * 0.08f, 0f);
                    targetScale = hasMountedWeapon ? new Vector3(1.05f, 0.88f, 1.15f) : new Vector3(0.98f, 0.72f, 1.05f);
                    break;
                default:
                    targetPos = vehicleType == EntityType.Spacecraft
                        ? new Vector3(0.04f, -0.28f, 0.18f)
                        : new Vector3(0f, -0.62f, 0.08f);
                    targetRot = vehicleType == EntityType.Spacecraft
                        ? new Vector3(12f, aimRotationDegrees.Y * 0.08f, 4f)
                        : new Vector3(82f, 0f, 0f);
                    targetScale = new Vector3(0.95f, 0.66f, 1.02f);
                    break;
            }

            visual.Position = visual.Position.Lerp(targetPos, (float)(12.0 * delta));
            visual.RotationDegrees = visual.RotationDegrees.Lerp(targetRot, (float)(12.0 * delta));
            visual.Scale = visual.Scale.Lerp(targetScale, (float)(12.0 * delta));

            ApplySegmentedAimPose(visual, seatType, aimRotationDegrees, vehicleType, delta);
        }

        if (node.GetNodeOrNull<Label3D>("NameLabel") is { } label)
        {
            var targetLabelPos = seatType == SeatType.Gunner
                ? new Vector3(0, 1.35f, 0)
                : new Vector3(0, 1.15f, 0);
            label.Position = label.Position.Lerp(targetLabelPos, (float)(10.0 * delta));
        }

        if (node.GetNodeOrNull<StaticBody3D>("Collider") is { } collider
            && collider.GetChildCount() > 0
            && collider.GetChild(0) is CollisionShape3D collisionShape)
        {
            var targetColliderPos = seatType == SeatType.Gunner
                ? new Vector3(0, 0.55f, 0)
                : new Vector3(0, 0.45f, 0);
            collisionShape.Position = collisionShape.Position.Lerp(targetColliderPos, (float)(12.0 * delta));
        }
    }

    private static void ApplySegmentedAimPose(Node3D visual, SeatType seatType, Vector3 aimRotationDegrees, EntityType vehicleType, double delta)
    {
        if (visual.GetNodeOrNull<Node3D>("UpperBodyPivot") is not { } upperBody)
            return;

        var upperTarget = seatType switch
        {
            SeatType.Gunner => new Vector3(-aimRotationDegrees.X * 0.35f, aimRotationDegrees.Y * 0.4f, 0f),
            SeatType.Driver => new Vector3(-aimRotationDegrees.X * 0.12f, aimRotationDegrees.Y * 0.18f, 0f),
            _ => new Vector3(-aimRotationDegrees.X * 0.08f, aimRotationDegrees.Y * 0.1f, 0f),
        };

        if (vehicleType == EntityType.Spacecraft)
            upperTarget += new Vector3(-4f, 0f, 0f);

        upperBody.RotationDegrees = upperBody.RotationDegrees.Lerp(upperTarget, (float)(12.0 * delta));

        if (visual.GetNodeOrNull<Node3D>("UpperBodyPivot/HeadPivot") is { } head)
        {
            var headTarget = new Vector3(-aimRotationDegrees.X * 0.28f, aimRotationDegrees.Y * 0.32f, 0f);
            head.RotationDegrees = head.RotationDegrees.Lerp(headTarget, (float)(14.0 * delta));
        }
    }

    private void ApplyPresentationFeedback(int ecsEntityId, Node3D node, double delta)
    {
        if (_store is null)
            return;

        var entity = _store.GetEntityById(ecsEntityId);
        if (entity.IsNull || !entity.TryGetComponent<RemotePresentationFeedbackState>(out var feedback))
            return;

        float dt = (float)delta;
        feedback.RecoilTimer = Mathf.Max(0f, feedback.RecoilTimer - dt);
        feedback.HitReactionTimer = Mathf.Max(0f, feedback.HitReactionTimer - dt);

        if (node.GetNodeOrNull<Node3D>("Visual") is { } visual)
        {
            float recoilT = feedback.RecoilTimer > 0f ? feedback.RecoilTimer / 0.16f : 0f;
            float hitT = feedback.HitReactionTimer > 0f ? feedback.HitReactionTimer / 0.32f : 0f;
            var hitDir = new Vector3(feedback.HitDirX, feedback.HitDirY, feedback.HitDirZ);

            var recoilOffset = new Vector3(0f, feedback.RecoilStrength * 0.04f * recoilT, feedback.RecoilStrength * 0.14f * recoilT);
            var hitOffset = new Vector3(Mathf.Sin(feedback.HitReactionTimer * 45f) * feedback.HitReactionStrength * 0.06f, feedback.HitReactionStrength * 0.03f * hitT, 0f);
            var hitRot = new Vector3(0f, 0f, Mathf.Sin(feedback.HitReactionTimer * 38f) * feedback.HitReactionStrength * 10f * hitT);

            visual.Position += recoilOffset + hitOffset;
            visual.RotationDegrees += hitRot;

            switch (feedback.HitZone)
            {
                case 3:
                    if (visual.GetNodeOrNull<Node3D>("UpperBodyPivot/HeadPivot") is { } headPivot)
                    {
                        headPivot.RotationDegrees += new Vector3(-feedback.HitReactionStrength * 14f * hitT, hitDir.X * 9f * hitT, hitDir.Z * 6f * hitT);
                        headPivot.Position += new Vector3(hitDir.X, 0f, hitDir.Z) * (feedback.HitReactionStrength * 0.03f * hitT);
                    }
                    break;
                case 1:
                    if (visual.GetNodeOrNull<Node3D>("LowerBody") is { } lowerBody)
                    {
                        lowerBody.Position += new Vector3(hitDir.X, 0f, hitDir.Z) * (feedback.HitReactionStrength * 0.04f * hitT);
                        lowerBody.RotationDegrees += new Vector3(0f, hitDir.X * 4f * hitT, hitDir.Z * 5f * hitT);
                    }
                    break;
                default:
                    if (visual.GetNodeOrNull<Node3D>("UpperBodyPivot") is { } upperBody)
                    {
                        upperBody.RotationDegrees += new Vector3(-feedback.HitReactionStrength * 10f * hitT, hitDir.X * 8f * hitT, 0f);
                        upperBody.Position += new Vector3(hitDir.X, 0f, hitDir.Z) * (feedback.HitReactionStrength * 0.03f * hitT);
                    }
                    break;
            }
        }

        if (node.GetNodeOrNull<Label3D>("NameLabel") is { } label)
        {
            float hitT = feedback.HitReactionTimer > 0f ? feedback.HitReactionTimer / 0.32f : 0f;
            label.Modulate = hitT > 0f
                ? new Color(1f, 0.55f + hitT * 0.2f, 0.55f + hitT * 0.2f)
                : new Color(0.8f, 0.9f, 1.0f);
        }

        feedback.RecoilStrength = feedback.RecoilTimer > 0f ? feedback.RecoilStrength : 0f;
        feedback.HitReactionStrength = feedback.HitReactionTimer > 0f ? feedback.HitReactionStrength : 0f;
        QueuePresentationFeedbackWrite(ecsEntityId, feedback);
    }

    private void ApplyAnimationStatePose(int ecsEntityId, Node3D node, double delta)
    {
        if (_store is null)
            return;

        var entity = _store.GetEntityById(ecsEntityId);
        if (entity.IsNull || !entity.TryGetComponent<RemoteAnimationState>(out var animation))
            return;

        if (node.GetNodeOrNull<Node3D>("Visual") is not { } visual)
            return;

        var blend = Mathf.Clamp(animation.Blend, 0f, 1f);
        var aimBlend = Mathf.Clamp(animation.AimBlend, 0f, 1f);
        var seatBlend = Mathf.Clamp(animation.SeatBlend, 0f, 1f);
        var state = animation.State;
        if (animation.PackedGraphState != 0)
        {
            state = (byte)(animation.PackedGraphState & 0xFF);
            animation.LocomotionState = (byte)((animation.PackedGraphState >> 8) & 0xFF);
            animation.TransitionState = (byte)((animation.PackedGraphState >> 16) & 0xFF);
        }
        if (animation.PackedBlendState != 0)
        {
            aimBlend = ((animation.PackedBlendState >> 0) & 0xFF) / 255f;
            seatBlend = ((animation.PackedBlendState >> 8) & 0xFF) / 255f;
            blend = ((animation.PackedBlendState >> 16) & 0xFF) / 255f;
        }
        var fragmentBinding = ResolveAnimationResourceBinding(animation.ResourceFragmentId);
        visual.SetMeta("anim_fragment_id", animation.ResourceFragmentId);
        visual.SetMeta("anim_fragment_name", fragmentBinding.Name);
        visual.SetMeta("anim_resource_path", fragmentBinding.ResourcePath);
        visual.SetMeta("anim_layer_mask", fragmentBinding.LayerMask);
        visual.SetMeta("anim_stream_policy", fragmentBinding.StreamPolicy);
        visual.SetMeta("anim_preload_priority", fragmentBinding.PreloadPriority);
        visual.SetMeta("anim_budget_bytes", animation.NetworkBudgetBytes);
        TrackAnimationStreaming(ecsEntityId, visual, fragmentBinding, ref animation);
        var cycle = animation.StateTime;
        float locomotionWave = animation.LocomotionState switch
        {
            2 => Mathf.Sin(cycle * 8f) * 1.2f,
            1 => Mathf.Sin(cycle * 4f) * 0.6f,
            _ => 0f,
        };

        if (visual.GetNodeOrNull<Node3D>("UpperBodyPivot") is { } upperBody)
        {
            Vector3 targetRot = state switch
            {
                1 => new Vector3(-6f, 0f, 0f),
                2 => new Vector3(8f * Mathf.Sin(cycle * 28f), 0f, 0f),
                3 => new Vector3(18f, -10f, 6f),
                4 => new Vector3(-12f, 12f * Mathf.Sin(cycle * 22f), 0f),
                9 => new Vector3(-6f + Mathf.Sin(cycle * 14f) * 8f, 6f, 10f),
                5 => new Vector3(14f * (1f - seatBlend), -18f * (1f - seatBlend), 6f),
                6 => new Vector3(-8f * seatBlend, 16f * seatBlend, 0f),
                7 => new Vector3(0f, locomotionWave * 1.6f, 0f),
                8 => new Vector3(-4f, locomotionWave * 2.6f, 0f),
                _ => Vector3.Zero,
            };
            targetRot += new Vector3(-4f * aimBlend, locomotionWave, 0f);
            upperBody.Position = upperBody.Position.Lerp(new Vector3(0, 1.1f - seatBlend * 0.04f, 0), (float)(10.0 * delta));
            upperBody.RotationDegrees = upperBody.RotationDegrees.Lerp(targetRot * blend, (float)(10.0 * delta));
        }

        if (visual.GetNodeOrNull<Node3D>("UpperBodyPivot/HeadPivot") is { } head)
        {
            Vector3 targetRot = state switch
            {
                1 => new Vector3(-3f, 0f, 0f),
                3 => new Vector3(10f, -14f, 0f),
                4 => new Vector3(8f * Mathf.Sin(cycle * 16f), 10f * Mathf.Sin(cycle * 20f), 0f),
                9 => new Vector3(-4f + Mathf.Sin(cycle * 12f) * 4f, 12f, 2f),
                5 => new Vector3(6f * (1f - seatBlend), -8f * (1f - seatBlend), 0f),
                6 => new Vector3(-6f * seatBlend, 8f * seatBlend, 0f),
                8 => new Vector3(-2f, locomotionWave, 0f),
                _ => Vector3.Zero,
            };
            targetRot += new Vector3(-2f * aimBlend, locomotionWave * 0.5f, 0f);
            head.RotationDegrees = head.RotationDegrees.Lerp(targetRot * blend, (float)(12.0 * delta));
        }

        if (visual.GetNodeOrNull<Node3D>("LowerBody") is { } lowerBody)
        {
            Vector3 targetOffset = state switch
            {
                2 => new Vector3(0f, 0f, 0.03f * Mathf.Sin(cycle * 24f)),
                3 => new Vector3(-0.02f, 0f, 0f),
                9 => new Vector3(0.03f * Mathf.Sin(cycle * 10f), -0.02f, 0.01f),
                5 => new Vector3(0f, -0.06f * (1f - seatBlend), 0.02f),
                6 => new Vector3(0f, 0.05f * seatBlend, -0.02f),
                _ when animation.LocomotionState == 2 => new Vector3(0f, Mathf.Sin(cycle * 8f) * 0.03f, 0f),
                _ when animation.LocomotionState == 1 => new Vector3(0f, Mathf.Sin(cycle * 4f) * 0.015f, 0f),
                _ => Vector3.Zero,
            };
            lowerBody.Position = lowerBody.Position.Lerp(new Vector3(0, 0.72f - seatBlend * 0.05f, 0) + targetOffset * blend, (float)(10.0 * delta));
        }
    }

}
