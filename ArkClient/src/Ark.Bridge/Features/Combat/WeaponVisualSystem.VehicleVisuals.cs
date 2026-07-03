using Godot;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;

namespace Ark.Bridge.Features.Combat;

public partial class WeaponVisualSystem
{
    public void SpawnVehicleVisual(int entityId, Vector3 position, int vehicleDefId)
    {
        if (_vehicles.ContainsKey(entityId)) return;

        var v = new VehicleVisual();
        v.Root = new Node3D { Name = $"Vehicle_{entityId}" };
        v.Root.Position = position;
        AddChild(v.Root);

        var bodyMesh = new BoxMesh { Size = new Vector3(3.2f, 1.0f, 5.5f) };
        bodyMesh.Material = _tankBodyMat;
        v.Body = new MeshInstance3D { Mesh = bodyMesh };
        v.Body.Position = new Vector3(0, 0.5f, 0);
        v.Root.AddChild(v.Body);

        var trackMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.15f, 0.15f, 0.12f),
            Metallic = 0.3f,
            Roughness = 0.8f,
        };
        for (int side = -1; side <= 1; side += 2)
        {
            var trackMesh = new BoxMesh { Size = new Vector3(0.4f, 0.6f, 5.6f) };
            trackMesh.Material = trackMat;
            var track = new MeshInstance3D { Mesh = trackMesh };
            track.Position = new Vector3(side * 1.8f, 0.3f, 0);
            v.Root.AddChild(track);
        }

        CreateVehicleStations(v, vehicleDefId);

        var ps = PhysicsServer3D.Singleton;
        v.PhysShape = ps.BoxShapeCreate();
        ps.ShapeSetData(v.PhysShape, new Vector3(3.2f, 1.6f, 5.5f) * 0.5f);

        v.PhysBody = ps.BodyCreate();
        ps.BodySetMode(v.PhysBody, PhysicsServer3D.BodyMode.Static);
        ps.BodyAddShape(v.PhysBody, v.PhysShape);
        ps.BodySetCollisionLayer(v.PhysBody, 1);
        ps.BodySetCollisionMask(v.PhysBody, 0);

        var initXform = new Transform3D(Basis.Identity, position + new Vector3(0, 0.8f, 0));
        ps.BodySetState(v.PhysBody, PhysicsServer3D.BodyState.Transform, initXform);
        var spaceRid = GetViewport().World3D.Space;
        ps.BodySetSpace(v.PhysBody, spaceRid);

        _vehicles[entityId] = v;
    }

    public void RemoveVehicleVisual(int entityId)
    {
        if (!_vehicles.Remove(entityId, out var v)) return;
        var ps = PhysicsServer3D.Singleton;
        if (v.PhysBody.IsValid) ps.FreeRid(v.PhysBody);
        if (v.PhysShape.IsValid) ps.FreeRid(v.PhysShape);
        v.Root.QueueFree();
    }

    private void UpdateVehicleTransforms()
    {
        if (_store == null) return;
        var ps = PhysicsServer3D.Singleton;

        foreach (var (entityId, v) in _vehicles)
        {
            var entity = _store.GetEntityById(entityId);
            if (entity.IsNull) { RemoveVehicleVisual(entityId); break; }

            Vector3 worldPos = Vector3.Zero;
            Quaternion worldQuat = Quaternion.Identity;

            if (entity.TryGetComponent<WorldPosition>(out var pos))
            {
                worldPos = new Vector3(pos.X, pos.Y, pos.Z);
                v.Root.GlobalPosition = worldPos;
            }

            if (entity.TryGetComponent<WorldRotation>(out var rot))
            {
                worldQuat = new Quaternion(rot.X, rot.Y, rot.Z, rot.W);
                v.Root.Quaternion = worldQuat;
            }

            if (v.PhysBody.IsValid)
            {
                var basis = new Basis(worldQuat);
                var xform = new Transform3D(basis, worldPos + basis * new Vector3(0, 0.8f, 0));
                ps.BodySetState(v.PhysBody, PhysicsServer3D.BodyState.Transform, xform);
            }

            foreach (var station in v.Stations)
            {
                if (TryResolveStationTurret(entityId, entity, station.SeatIndex, out var yawDeg, out var pitchDeg))
                    station.Pivot.RotationDegrees = new Vector3(pitchDeg, yawDeg, 0);
            }
        }
    }

    private void CreateVehicleStations(VehicleVisual visual, int vehicleDefId)
    {
        switch (vehicleDefId)
        {
            case 2:
                visual.Stations.Add(AddVehicleStation(visual, 1, new Vector3(0, 1.0f, -0.3f), new BoxMesh { Size = new Vector3(2.0f, 0.7f, 2.4f) }, new Vector3(0, 0.35f, 0), CreateBarrelMesh(3.5f, 0.08f, 0.10f), new Vector3(0, 0.35f, -2.9f)));
                visual.Stations.Add(AddVehicleStation(visual, 2, new Vector3(0, 1.7f, 0.5f), new BoxMesh { Size = new Vector3(0.65f, 0.38f, 0.65f) }, new Vector3(0, 0.18f, 0), CreateBarrelMesh(1.8f, 0.04f, 0.05f), new Vector3(0, 0.18f, -1.0f)));
                break;
            case 3:
                visual.Stations.Add(AddVehicleStation(visual, 0, new Vector3(0, 1.1f, -0.1f), new BoxMesh { Size = new Vector3(1.5f, 0.55f, 1.5f) }, new Vector3(0, 0.22f, 0), CreateBarrelMesh(2.4f, 0.06f, 0.08f), new Vector3(-0.35f, 0.32f, -1.3f), new Vector3(0.35f, 0.32f, -1.3f)));
                break;
            case 4:
                visual.Stations.Add(AddVehicleStation(visual, 0, new Vector3(0, 1.0f, -1.2f), new SphereMesh { Radius = 0.12f, Height = 0.24f }, new Vector3(0, 0.02f, 0), CreateBarrelMesh(1.1f, 0.03f, 0.04f), new Vector3(0, 0.0f, -0.7f)));
                break;
            case 5:
                visual.Stations.Add(AddVehicleStation(visual, 1, new Vector3(0, 1.65f, 0.6f), new BoxMesh { Size = new Vector3(0.85f, 0.4f, 0.9f) }, new Vector3(0, 0.12f, 0), CreateBarrelMesh(2.0f, 0.07f, 0.08f), new Vector3(0, 0.18f, -1.3f)));
                break;
            default:
                visual.Stations.Add(AddVehicleStation(visual, 1, new Vector3(0, 1.0f, -0.3f), new BoxMesh { Size = new Vector3(2.0f, 0.7f, 2.4f) }, new Vector3(0, 0.35f, 0), CreateBarrelMesh(3.5f, 0.08f, 0.10f), new Vector3(0, 0.35f, -2.9f)));
                break;
        }

        if (visual.Stations.Count > 0)
        {
            var primary = visual.Stations[0];
            visual.TurretPivot = primary.Pivot;
            visual.Turret = primary.Turret;
            visual.Barrel = primary.Barrel;
        }
    }

    private VehicleWeaponStation AddVehicleStation(VehicleVisual visual, int seatIndex, Vector3 pivotPos, Mesh turretMesh, Vector3 turretLocalPos, Mesh barrelMesh, Vector3 barrelLocalPos, Vector3? mirrorBarrelLocalPos = null)
    {
        var pivot = new Node3D { Name = seatIndex == 1 ? "TurretPivot" : $"TurretPivot_Seat{seatIndex}", Position = pivotPos };
        visual.Root.AddChild(pivot);

        var turret = new MeshInstance3D { Mesh = turretMesh, Position = turretLocalPos, MaterialOverride = _tankTurretMat };
        pivot.AddChild(turret);

        var barrel = new MeshInstance3D { Mesh = barrelMesh, Position = barrelLocalPos, RotationDegrees = new Vector3(90, 0, 0), MaterialOverride = _tankTurretMat };
        pivot.AddChild(barrel);

        MeshInstance3D? barrelMirror = null;
        if (mirrorBarrelLocalPos.HasValue)
        {
            var mirrorMesh = CreateBarrelMesh(((CylinderMesh)barrelMesh).Height, ((CylinderMesh)barrelMesh).TopRadius, ((CylinderMesh)barrelMesh).BottomRadius);
            barrelMirror = new MeshInstance3D { Mesh = mirrorMesh, Position = mirrorBarrelLocalPos.Value, RotationDegrees = new Vector3(90, 0, 0), MaterialOverride = _tankTurretMat };
            pivot.AddChild(barrelMirror);
        }

        return new VehicleWeaponStation
        {
            SeatIndex = seatIndex,
            Pivot = pivot,
            Turret = turret,
            Barrel = barrel,
            BarrelMirror = barrelMirror,
        };
    }

    private static CylinderMesh CreateBarrelMesh(float height, float topRadius, float bottomRadius)
    {
        return new CylinderMesh
        {
            TopRadius = topRadius,
            BottomRadius = bottomRadius,
            Height = height,
        };
    }

    private bool TryResolveStationTurret(int vehicleEntityId, Entity vehicleEntity, int seatIndex, out float yawDeg, out float pitchDeg)
    {
        yawDeg = 0f;
        pitchDeg = 0f;

        var mountedQuery = _store!.Query<VehicleSeat, TurretState>();
        foreach (var chunk in mountedQuery.Chunks)
        {
            var seats = chunk.Chunk1;
            var turrets = chunk.Chunk2;
            for (int i = 0; i < chunk.Length; i++)
            {
                ref readonly var seat = ref seats.Span[i];
                if (seat.VehicleEntityId != vehicleEntityId || seat.SeatIndex != seatIndex)
                    continue;

                ref readonly var turret = ref turrets.Span[i];
                yawDeg = Mathf.RadToDeg(turret.Yaw);
                pitchDeg = Mathf.RadToDeg(turret.Pitch);
                return true;
            }
        }

        if (seatIndex == 1 && vehicleEntity.TryGetComponent<TurretState>(out var vehicleTurret))
        {
            yawDeg = Mathf.RadToDeg(vehicleTurret.Yaw);
            pitchDeg = Mathf.RadToDeg(vehicleTurret.Pitch);
            return true;
        }

        return false;
    }
}
