using Godot;
using Ark.Abstractions;
using Ark.Bridge.Features.BaseBuilding;
using Ark.Ecs.Components;
using Ark.UI;
using Friflo.Engine.ECS;

namespace Ark.Player;

/// <summary>
/// 建造放置控制器 — 全局建造功能节点。
///
/// 职责：
///   • 响应 SquadModule 的 OnBuildModeChanged 事件
///   • 建造模式中：将鼠标射线投射到地面，显示半透明 Ghost 预览
///   • Ghost 跟随鼠标、颜色指示可放置性（绿色/红色）
///   • R 键旋转 Ghost 45°
///   • 左键点击：放置建筑
///   • 地形拟合：放置时记录四角高度，生成基础板使地面看起来被平整
///
/// 注意：B 键由 SquadModule 统一处理，本控制器只响应事件。
/// </summary>
public partial class BuildPlacementController : Node
{
    // ─── 外部依赖 ───
    private IBaseBuildingService?  _module;
    private BuildingVisualManager? _visualManager;
    private BuildPanelUI?          _panel;
    private Camera3D?              _camera;
    private EntityStore?           _commandStore;
    private PlayerEcsAuthority?    _ecsAuth;

    // ─── Ghost 预览 ───
    private Node3D?             _ghostRoot;
    private StandardMaterial3D? _ghostMat;
    private int                 _currentTypeId;
    private float               _ghostRotY;
    private bool                _buildModeActive;
    private bool                _canPlace;
    private Vector3             _ghostWorldPos;

    // ─── 物理帧缓存 ───
    private Vector3 _physicsHitPos;
    private bool    _physicsHitValid;

    // ─── 地形查询（由 GameBootstrap 注入）───
    private Func<float, float, float>? _sampleTerrainHeight;
    private Action<float, float, float, float, float>? _flattenTerrain;

    private const uint GroundLayer = 1;

    // ─── 悬停文字 ───
    private Label? _hoverLabel;

    // ═══════════════════════════════════════════════════════════════════════
    //                          初始化
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 初始化（不绑定相机，由 SetCamera 动态设置）。
    /// </summary>
    public void Initialize(
        IBaseBuildingService module,
        BuildingVisualManager visualManager,
        BuildPanelUI panel)
    {
        _module        = module;
        _visualManager = visualManager;
        _panel         = panel;

        _panel.OnBuildingSelected += OnBuildingSelectedFromPanel;
        _panel.OnPanelClosed      += OnPanelClosed;
    }

    /// <summary>
    /// 设置地形查询回调（由 GameBootstrap 调用）。
    /// </summary>
    public void SetTerrainQuery(
        Func<float, float, float>? sampleHeight,
        Action<float, float, float, float, float>? flattenArea)
    {
        _sampleTerrainHeight = sampleHeight;
        _flattenTerrain = flattenArea;
    }

    /// <summary>
    /// 设置当前使用的相机（角色切换时调用）。
    /// </summary>
    public void SetCamera(Camera3D? camera)
    {
        _camera = camera;
    }

    public void SetCommandStore(EntityStore? store)
    {
        _commandStore = store;
        _ecsAuth = store is null ? null : new PlayerEcsAuthority(store);
    }

    public override void _Ready()
    {
        _ghostRoot = new Node3D { Name = "BuildGhost", Visible = false };

        _hoverLabel = new Label
        {
            Position    = new Vector2(8, 8),
            Visible     = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _hoverLabel.AddThemeFontSizeOverride("font_size", 13);
        _hoverLabel.AddThemeColorOverride("font_color", Colors.White);

        var canvas = new CanvasLayer { Name = "BuildHoverCanvas", Layer = 4 };
        canvas.AddChild(_hoverLabel);

        GetTree().Root.CallDeferred(Node.MethodName.AddChild, _ghostRoot);
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, canvas);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          建造模式控制（由外部调用）
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 进入建造模式（由 SquadModule.OnBuildModeChanged 调用）。
    /// </summary>
    public void EnterBuildMode()
    {
        if (_buildModeActive) return;

        _buildModeActive = true;
        _ghostRotY       = 0f;
        UpdateRemoteBuildState(_currentTypeId, true);

        Input.MouseMode = Input.MouseModeEnum.Visible;
        _panel?.ShowPanel();

        GD.Print("[BuildPlacement] Entered build mode");
    }

    /// <summary>
    /// 退出建造模式（由 SquadModule.OnBuildModeChanged 调用）。
    /// </summary>
    public void ExitBuildMode()
    {
        if (!_buildModeActive) return;

        _buildModeActive = false;

        if (_ghostRoot != null)
            _ghostRoot.Visible = false;
        if (_hoverLabel != null)
            _hoverLabel.Visible = false;

        _panel?.HidePanel();
        if (Ark.Services.GameServices.IsNetworkMode)
            UpdateRemoteBuildState(0, false);
        else
            _module?.ExitBuildMode();

        Input.MouseMode = Input.MouseModeEnum.Captured;

        GD.Print("[BuildPlacement] Exited build mode");
    }

    /// <summary>
    /// 处理建造模式变更事件。
    /// </summary>
    public void OnBuildModeChanged(bool active)
    {
        if (active)
            EnterBuildMode();
        else
            ExitBuildMode();
    }

    private void OnPanelClosed()
    {
        // 面板关闭时通知 SquadModule 退出建造模式
        // 这里不直接调用 ExitBuildMode，而是让 SquadModule 统一管理
        // ExitBuildMode 会在 SquadModule.SetBuildMode(false) 时被调用
    }

    private void OnBuildingSelectedFromPanel(int typeId)
    {
        _currentTypeId = typeId;
        _ghostRotY     = 0f;
        if (Ark.Services.GameServices.IsNetworkMode)
            UpdateRemoteBuildState(typeId, true);
        else
            _module?.EnterBuildMode(typeId);
        UpdateGhostMesh(typeId);
    }

    private void UpdateRemoteBuildState(int buildingTypeId, bool isBuildMode)
    {
        if (!Ark.Services.GameServices.IsNetworkMode || _commandStore is null)
            return;

        int localEntityId = Ark.Services.GameServices.RemoteWorldEcsCache?.LocalPresentationEntityId ?? 0;
        if (localEntityId <= 0)
            return;

        var localEntity = _commandStore.GetEntityById(localEntityId);
        if (localEntity.IsNull)
            return;

        _ecsAuth?.Write(localEntity, new RemoteBuildState
        {
            SelectedBuildingTypeId = buildingTypeId,
            IsBuildMode = isBuildMode ? (byte)1 : (byte)0,
        });
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          输入处理
    // ═══════════════════════════════════════════════════════════════════════

    public override void _Input(InputEvent @event)
    {
        // B 键由 SquadModule 处理，这里不再响应

        if (!_buildModeActive) return;

        if (@event is InputEventMouse mouseEvent
            && _panel?.IsPointerOverPanel(mouseEvent.Position) == true)
        {
            return;
        }

        // R 键：旋转 Ghost 45°
        if (Input.IsActionJustPressed("build_rotate"))
        {
            _ghostRotY += Mathf.Pi * 0.25f;
            if (_ghostRoot != null)
                _ghostRoot.Rotation = new Vector3(0, _ghostRotY, 0);
            GetViewport().SetInputAsHandled();
        }

        if (@event is InputEventMouseButton lmb && lmb.Pressed
            && lmb.ButtonIndex == MouseButton.Left && _canPlace)
        {
            PlaceCurrentBuilding();
            GetViewport().SetInputAsHandled();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          物理帧更新
    // ═══════════════════════════════════════════════════════════════════════

    public override void _PhysicsProcess(double _delta)
    {
        if (!_buildModeActive || _camera == null)
        {
            _physicsHitValid = false;
            return;
        }

        var viewport   = _camera.GetViewport();
        var mousePos   = viewport.GetMousePosition();
        var spaceState = _camera.GetWorld3D().DirectSpaceState;

        if (_panel?.IsPointerOverPanel(mousePos) == true)
        {
            _physicsHitValid = false;
            return;
        }

        if (spaceState == null) { _physicsHitValid = false; return; }

        var rayOrigin = _camera.ProjectRayOrigin(mousePos);
        var rayEnd    = rayOrigin + _camera.ProjectRayNormal(mousePos) * 500f;

        var query = PhysicsRayQueryParameters3D.Create(rayOrigin, rayEnd, GroundLayer);
        query.HitBackFaces = false;

        var result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            _physicsHitPos   = SnapToGrid((Vector3)result["position"], 0.5f);
            _physicsHitValid = true;
        }
        else
        {
            _physicsHitValid = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          渲染帧更新
    // ═══════════════════════════════════════════════════════════════════════

    public override void _Process(double _delta)
    {
        if (!_buildModeActive || _ghostRoot == null || !_ghostRoot.IsInsideTree() || _module == null)
            return;

        if (_physicsHitValid)
        {
            _ghostWorldPos            = _physicsHitPos;
            _ghostRoot.GlobalPosition = _ghostWorldPos;
            _ghostRoot.Visible        = _currentTypeId != 0;

            _canPlace = Ark.Services.GameServices.IsNetworkMode
                ? _currentTypeId != 0
                : _module is BaseBuildingModule localModule && localModule.CanPlaceAtGodot(_ghostWorldPos);
            UpdateGhostColor(_canPlace);
            UpdateHoverLabel(_ghostWorldPos, _canPlace);
        }
        else
        {
            _ghostRoot.Visible = false;
            _canPlace          = false;
            if (_hoverLabel != null) _hoverLabel.Visible = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          放置建筑
    // ═══════════════════════════════════════════════════════════════════════

    private void PlaceCurrentBuilding()
    {
        if (_module == null || _visualManager == null) return;
        if (_currentTypeId == 0) return;

        var rot = Quaternion.FromEuler(new Vector3(0, _ghostRotY, 0));

        // 网络模式：通过 ServerAuthorityBridge 路由到服务端
        if (Ark.Services.GameServices.IsNetworkMode)
        {
            _ecsAuth?.CreateRequest(new NetworkBuildPlacementRequest
            {
                BuildingTypeId = _currentTypeId,
                PositionX = _ghostWorldPos.X,
                PositionY = _ghostWorldPos.Y,
                PositionZ = _ghostWorldPos.Z,
                RotationY = _ghostRotY,
            });
            GD.Print($"[Build] Requested {_currentTypeId} at {_ghostWorldPos} (awaiting server confirmation)");
            return;
        }

        var placed = _module.PlaceBuilding(
            new System.Numerics.Vector3(_ghostWorldPos.X, _ghostWorldPos.Y, _ghostWorldPos.Z),
            new System.Numerics.Quaternion(rot.X, rot.Y, rot.Z, rot.W));
        if (placed)
        {
            // 平整建筑下方地形 — 让建筑底面融合地表
            var def = BuildingDef.Get(_currentTypeId);
            if (def != null && _flattenTerrain != null)
            {
                float halfX = def.Value.Size.X * 0.5f + 0.5f; // 稍微扩大范围使过渡自然
                float halfZ = def.Value.Size.Z * 0.5f + 0.5f;
                _flattenTerrain(_ghostWorldPos.X, _ghostWorldPos.Z, halfX, halfZ, _ghostWorldPos.Y);
            }
            GD.Print($"[Build] Placed {_currentTypeId} at {_ghostWorldPos} + terrain flattened");
        }
        else
        {
            UpdateGhostColor(false);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          Ghost 辅助
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateGhostMesh(int typeId)
    {
        if (_ghostRoot == null) return;

        var def = BuildingDef.Get(typeId);
        if (def == null) return;

        foreach (var child in _ghostRoot.GetChildren())
            child.QueueFree();

        _ghostMat = new StandardMaterial3D
        {
            AlbedoColor  = BuildingDef.GhostValid,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode     = BaseMaterial3D.CullModeEnum.Disabled,
        };

        var mesh = new BoxMesh { Size = def.Value.Size };
        mesh.Material = _ghostMat;
        var body = new MeshInstance3D { Mesh = mesh };
        body.Position = new Vector3(0, def.Value.Size.Y * 0.5f, 0);
        _ghostRoot.AddChild(body);

        var footMat = new StandardMaterial3D
        {
            AlbedoColor  = new Color(1f, 1f, 0.2f, 0.3f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode  = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        var footMesh = new BoxMesh
        {
            Size = new Vector3(def.Value.Size.X + 0.2f, 0.08f, def.Value.Size.Z + 0.2f)
        };
        footMesh.Material = footMat;
        var foot = new MeshInstance3D { Mesh = footMesh };
        foot.Position = new Vector3(0, 0.04f, 0);
        _ghostRoot.AddChild(foot);

        _ghostRoot.Rotation = new Vector3(0, _ghostRotY, 0);
    }

    private void UpdateGhostColor(bool valid)
    {
        if (_ghostMat == null) return;
        _ghostMat.AlbedoColor = valid ? BuildingDef.GhostValid : BuildingDef.GhostInvalid;
    }

    private void UpdateHoverLabel(Vector3 pos, bool canPlace)
    {
        if (_hoverLabel == null || _camera == null) return;

        var screenPos = _camera.UnprojectPosition(pos + new Vector3(0, 3f, 0));

        _hoverLabel.Position = screenPos - new Vector2(60, 20);
        _hoverLabel.Text     = canPlace ? "✔ 可放置\n[LMB] 确认" : "✘ 无法放置";
        _hoverLabel.AddThemeColorOverride("font_color", canPlace
            ? new Color(0.4f, 1f, 0.4f)
            : new Color(1f, 0.35f, 0.35f));
        _hoverLabel.Visible = true;
    }

    private static Vector3 SnapToGrid(Vector3 pos, float gridSize)
    {
        return new Vector3(
            Mathf.Round(pos.X / gridSize) * gridSize,
            pos.Y,
            Mathf.Round(pos.Z / gridSize) * gridSize
        );
    }

    public override void _ExitTree()
    {
        _ghostRoot?.QueueFree();
    }
}
