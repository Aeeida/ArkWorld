using System;
using System.Collections.Generic;
using Godot;
using Friflo.Engine.ECS;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;
using Ark.Abstractions;

namespace Ark.Bridge.Features.Squad;

public partial class SquadModule
{
    public const int MaxMembers = 5;

    public static readonly Color[] MemberColors =
    [
        new Color(0.2f, 0.6f, 1.0f),
        new Color(0.2f, 0.8f, 0.3f),
        new Color(1.0f, 0.8f, 0.2f),
        new Color(0.9f, 0.4f, 0.2f),
        new Color(0.8f, 0.3f, 0.8f),
    ];

    public Func<EntityStore, Entity, int, Color, ISquadMemberController>? MemberFactory { get; set; }

    private EntityStore? _store;
    private SquadEcsAuthority? _squadAuth;
    private Entity _leaderEntity;
    private IPlayerController? _leaderController;
    private Node? _leaderNode;

    private readonly List<ISquadMemberController?> _members = new(MaxMembers);
    private readonly List<Entity> _memberEntities = new(MaxMembers);

    private int _activeSlot;
    private byte _formationType;
    private bool _buildModeActive;

    public event Action<int>? OnActiveChanged;
    public event Action<Entity>? OnLeaderChanged;
    public event Action<bool>? OnBuildModeChanged;
    public event Action<byte>? OnFormationChanged;

    public int ActiveSlot => _activeSlot;
    public int MemberCount => _members.Count;
    public byte FormationType => _formationType;
    public bool IsBuildModeActive => _buildModeActive;
    public Entity LeaderEntity => _leaderEntity;
    public Entity ActiveEntity => _activeSlot == 0
        ? _leaderEntity
        : (_activeSlot <= _memberEntities.Count ? _memberEntities[_activeSlot - 1] : default);
    public IPlayerController? LeaderController => _leaderController;

    public IControllable? ActiveControllable
    {
        get
        {
            return _leaderController;
        }
    }

    public void Initialize(EntityStore store, Entity leaderEntity, IPlayerController leaderController, Node leaderNode)
    {
        _store = store;
        _squadAuth = new SquadEcsAuthority(store);
        _leaderEntity = leaderEntity;
        _leaderController = leaderController;
        _leaderNode = leaderNode;
        _activeSlot = 0;
        _formationType = 0;

        _leaderController.SetEntity(store, leaderEntity);

        _squadAuth.InitializeLeader(_leaderEntity);

        GD.Print("[SquadModule] Initialized");
    }

    public override void _Ready()
    {
        RegisterInputActions();
    }

    private static void RegisterInputActions()
    {
        for (int i = 1; i <= MaxMembers; i++)
        {
            string action = $"squad_select_{i}";
            RegisterInputAction(action, Key.F1 + (i - 1));
        }

        RegisterInputAction("squad_select_leader", Key.F6);
        RegisterInputAction("squad_formation", Key.Tab);
    }

    private static void RegisterInputAction(string action, Key key)
    {
        if (!InputMap.HasAction(action))
            InputMap.AddAction(action);

        var ev = new InputEventKey { PhysicalKeycode = key };
        bool exists = false;
        foreach (var existing in InputMap.ActionGetEvents(action))
        {
            if (existing is InputEventKey keyEvent && keyEvent.PhysicalKeycode == key)
            {
                exists = true;
                break;
            }
        }

        if (!exists)
            InputMap.ActionAddEvent(action, ev);

        GD.Print($"[InputMap] Action '{action}' bound to {key}");
    }

    public ISquadMemberController? GetMember(int slotIndex)
    {
        if (slotIndex < 1 || slotIndex > _members.Count) return null;
        return _members[slotIndex - 1];
    }

    public Entity GetMemberEntity(int slotIndex)
    {
        if (slotIndex < 1 || slotIndex > _memberEntities.Count) return default;
        return _memberEntities[slotIndex - 1];
    }

    public Vector3 GetActivePosition()
    {
        if (_activeSlot == 0 && _leaderController != null)
            return _leaderController.GlobalPosition;

        if (_activeSlot > 0 && _activeSlot <= _members.Count && _members[_activeSlot - 1] != null)
            return _members[_activeSlot - 1]!.GlobalPosition;

        return Vector3.Zero;
    }
}
