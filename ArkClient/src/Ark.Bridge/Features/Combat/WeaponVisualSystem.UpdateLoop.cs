using Godot;
using Ark.Ecs.Components;
using Ark.Shared.Data;
using Friflo.Engine.ECS;

namespace Ark.Bridge.Features.Combat;

public partial class WeaponVisualSystem
{
    public override void _Process(double delta)
    {
        float dt = (float)delta;
        DrainEcsNetworkEffects();
        DrainPendingEffects();
        UpdateWeaponAnims(dt);
        UpdateHitFlashes(dt);
        UpdateExplosions(dt);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        UpdateVehicleTransforms();
        UpdateBullets(dt);
    }

    /// <summary>
    /// 在主线程上处理从后台线程入队的视觉效果。
    /// </summary>
    private void DrainPendingEffects()
    {
        // 处理命中效果
        while (_pendingHits.TryDequeue(out var hitPos))
            NotifyHit(hitPos);

        // 处理爆炸效果
        while (_pendingExplosions.TryDequeue(out var exp))
            SpawnExplosionEffect(exp.Pos, exp.Radius);

        // 处理开火效果
        while (_pendingFires.TryDequeue(out var fire))
            HandleWeaponFired(fire.EntityId, fire.WeaponDefId);
    }

    private void DrainEcsNetworkEffects()
    {
        if (_store == null)
            return;

        _drainedEventEntities.Clear();
        _pendingPresentationFeedbackWrites.Clear();
        _pendingBuildingFeedbackWrites.Clear();

        var fireQuery = _store.Query<WeaponFireVisualEvent>();
        foreach (var chunk in fireQuery.Chunks)
        {
            var fires = chunk.Chunk1;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var fire = ref fires.Span[i];
                if (fire.HasExplicitTrajectory != 0)
                {
                    OnWeaponFired(
                        fire.ShooterEntityId,
                        new Vector3(fire.OriginX, fire.OriginY, fire.OriginZ),
                        new Vector3(fire.DirX, fire.DirY, fire.DirZ));
                }
                else
                {
                    HandleWeaponFired(fire.ShooterEntityId, fire.WeaponDefId);
                }

                TriggerPresentationRecoil(fire.ShooterEntityId, fire.WeaponDefId);
                _drainedEventEntities.Add(chunk.Entities[i]);
            }
        }

        var hitQuery = _store.Query<ProjectileHitVisualEvent>();
        foreach (var chunk in hitQuery.Chunks)
        {
            var hits = chunk.Chunk1;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var hit = ref hits.Span[i];
                var hitPos = new Vector3(hit.X, hit.Y, hit.Z);
                NotifyHit(hitPos);
                TrackDirectionalRiderHit(hitPos, 0.55f);
                TrackDirectionalBuildingHit(hitPos, 0.65f);
                _drainedEventEntities.Add(chunk.Entities[i]);
            }
        }

        var explosionQuery = _store.Query<ExplosionVisualEvent>();
        foreach (var chunk in explosionQuery.Chunks)
        {
            var explosions = chunk.Chunk1;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var explosion = ref explosions.Span[i];
                var hitPos = new Vector3(explosion.X, explosion.Y, explosion.Z);
                SpawnExplosionEffect(hitPos, explosion.Radius);
                TrackDirectionalRiderHit(hitPos, Mathf.Clamp(explosion.Radius * 0.12f, 0.2f, 0.8f));
                TrackDirectionalBuildingHit(hitPos, Mathf.Clamp(explosion.Radius * 0.18f, 0.25f, 1f));
                _drainedEventEntities.Add(chunk.Entities[i]);
            }
        }

        FlushDeferredFeedbackWrites();

        _ecsAuth?.DeleteEntities(_drainedEventEntities);
    }

    private void FlushDeferredFeedbackWrites()
    {
        _ecsAuth?.FlushFeedback(_pendingPresentationFeedbackWrites, _pendingBuildingFeedbackWrites);
    }

    private void TriggerPresentationRecoil(int shooterEntityId, int weaponDefId)
    {
        var entity = ResolveEntity(shooterEntityId);
        if (entity.IsNull)
            return;

        RemotePresentationFeedbackState feedback = _pendingPresentationFeedbackWrites.TryGetValue(entity.Id, out var pending)
            ? pending
            : entity.TryGetComponent<RemotePresentationFeedbackState>(out var existing)
                ? existing
                : new RemotePresentationFeedbackState();

        float recoilStrength = 0.14f;
        if (entity.TryGetComponent<VehicleSeat>(out var seat))
        {
            recoilStrength = seat.SeatType == (byte)SeatType.Gunner ? 0.34f : 0.18f;
            if (entity.TryGetComponent<RemoteVehicleOccupantState>(out var runtime) && runtime.HasMountedWeapon != 0)
                recoilStrength += 0.08f;
        }
        else if (entity.TryGetComponent<WeaponState>(out var weaponState) && weaponState.Category >= 4)
        {
            recoilStrength = 0.22f;
        }

        feedback.RecoilTimer = MathF.Max(feedback.RecoilTimer, 0.16f);
        feedback.RecoilStrength = MathF.Max(feedback.RecoilStrength, recoilStrength);
        _pendingPresentationFeedbackWrites[entity.Id] = feedback;
    }

    private void TrackDirectionalBuildingHit(Vector3 hitPos, float strength)
    {
        if (_store is null)
            return;

        Entity? bestEntity = null;
        float bestDistSq = 16f;
        var query = _store.Query<Building, WorldPosition>();
        foreach (var chunk in query.Chunks)
        {
            var buildings = chunk.Chunk1;
            var positions = chunk.Chunk2;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var building = ref buildings.Span[i];
                ref readonly var pos = ref positions.Span[i];
                var delta = hitPos - new Vector3(pos.X, pos.Y + 1.0f, pos.Z);
                float distSq = delta.LengthSquared();
                float radius = Mathf.Max(2.0f, building.BuildingTypeId == 5 ? 8.0f : 4.5f);
                if (distSq > radius * radius || distSq >= bestDistSq)
                    continue;

                bestDistSq = distSq;
                bestEntity = _store.GetEntityById(chunk.Entities[i]);
            }
        }

        if (bestEntity is null || bestEntity.Value.IsNull)
            return;

        var entity = bestEntity.Value;
        if (!entity.TryGetComponent<WorldPosition>(out var buildingPos))
            return;

        var hitDir = hitPos - new Vector3(buildingPos.X, buildingPos.Y + 1.0f, buildingPos.Z);
        if (hitDir.LengthSquared() <= 1e-6f)
            hitDir = Vector3.Forward;
        hitDir = hitDir.Normalized();

        BuildingDamageFeedbackState feedback = _pendingBuildingFeedbackWrites.TryGetValue(entity.Id, out var pending)
            ? pending
            : entity.TryGetComponent<BuildingDamageFeedbackState>(out var existing)
                ? existing
                : new BuildingDamageFeedbackState();
        feedback.HitDirX = hitDir.X;
        feedback.HitDirY = hitDir.Y;
        feedback.HitDirZ = hitDir.Z;
        feedback.PulseTimer = MathF.Max(feedback.PulseTimer, 0.75f);
        feedback.Strength = Mathf.Max(feedback.Strength, strength);
        _pendingBuildingFeedbackWrites[entity.Id] = feedback;
    }

    private void TrackDirectionalRiderHit(Vector3 hitPos, float strength)
    {
        if (_store is null)
            return;

        Entity? bestEntity = null;
        Vector3 bestDelta = Vector3.Zero;
        float bestDistSq = 4f;

        var query = _store.Query<RemoteEntityState, WorldPosition>();
        foreach (var chunk in query.Chunks)
        {
            var states = chunk.Chunk1;
            var positions = chunk.Chunk2;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var state = ref states.Span[i];
                if ((EntityType)state.EntityType is not (EntityType.Player or EntityType.RemotePlayer or EntityType.Npc or EntityType.Monster))
                    continue;

                ref readonly var pos = ref positions.Span[i];
                var delta = hitPos - new Vector3(pos.X, pos.Y + 0.9f, pos.Z);
                float distSq = delta.LengthSquared();
                if (distSq >= bestDistSq)
                    continue;

                bestDistSq = distSq;
                bestDelta = delta;
                bestEntity = _store.GetEntityById(chunk.Entities[i]);
            }
        }

        if (bestEntity is null || bestEntity.Value.IsNull)
            return;

        var entity = bestEntity.Value;
        RemotePresentationFeedbackState feedback = _pendingPresentationFeedbackWrites.TryGetValue(entity.Id, out var pending)
            ? pending
            : entity.TryGetComponent<RemotePresentationFeedbackState>(out var existing)
                ? existing
                : new RemotePresentationFeedbackState();

        var hitDir = bestDelta.LengthSquared() > 1e-6f ? bestDelta.Normalized() : Vector3.Forward;
        byte hitZone = bestDelta.Y > 0.8f ? (byte)3 : bestDelta.Y > 0.1f ? (byte)2 : (byte)1;
        feedback.HitDirX = hitDir.X;
        feedback.HitDirY = hitDir.Y;
        feedback.HitDirZ = hitDir.Z;
        feedback.HitZone = hitZone;
        feedback.HitReactionTimer = Mathf.Max(feedback.HitReactionTimer, 0.32f);
        feedback.HitReactionStrength = Mathf.Max(feedback.HitReactionStrength, strength);
        _pendingPresentationFeedbackWrites[entity.Id] = feedback;
    }
}
