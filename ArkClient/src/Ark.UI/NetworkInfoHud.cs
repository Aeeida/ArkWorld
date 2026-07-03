using System.Collections.Generic;
using Ark.Ecs.Components;
using Friflo.Engine.ECS;
using Godot;
using Ark.Networking;
using Ark.Services;
using Game.Shared.Core;

namespace Ark.UI;

/// <summary>
/// 网络信息 HUD — 显示 SignalR / TCP 连接状态、统计数据和连接控制。
/// 默认显示，可按 \ 键切换显示/隐藏。
/// </summary>
public partial class NetworkInfoHud : CanvasLayer
{
    private ColorRect? _loginBackground;
    private PanelContainer? _panel;
    private VBoxContainer? _vbox;
    private bool _startupMode = true;

    // ── 状态标签 ──
    private Label? _lblTitle;
    private Label? _lblConnectionState;
    private Label? _lblSignalR;
    private Label? _lblTcp;
    private Label? _lblUptime;
    private Label? _lblStats;
    private Label? _lblTraffic;

    // ── 连接控制 ──
    private HBoxContainer? _connectBox;
    private LineEdit? _txtHost;
    private LineEdit? _txtPort;
    private Button? _btnConnect;
    private Button? _btnDisconnect;
    private Label? _lblStatus;
    private Label? _lblPreloadState;

    // ── 登录 / 角色流程 ──
    private LineEdit? _txtAccount;
    private LineEdit? _txtPassword;
    private Button? _btnLogin;
    private Button? _btnRefreshCharacters;
    private ItemList? _characterList;
    private LineEdit? _txtCharacterName;
    private LineEdit? _txtFaction;
    private LineEdit? _txtClass;
    private LineEdit? _txtStartingZone;
    private Button? _btnCreateCharacter;
    private Button? _btnEnterWorld;
    private Label? _lblAuthState;
    private Label? _lblCharacterState;
    private readonly NetworkInfoWorkflowFacade _workflowFacade = new();
    private readonly List<CharacterSlotViewModel> _characterSlots = [];
    private EntityStore? _store;

    private double _updateAccum;
    private const double UpdateInterval = 0.5; // 每 0.5 秒刷新一次

    public event Action? OnWorldEntryCompleted;

    public override void _Ready()
    {
        Layer = 11;
        BuildUI();
        GameServices.WorldPreparationStatusChanged += OnWorldPreparationStatusChanged;
        EnterStartupMode();
    }

    public override void _ExitTree()
    {
        GameServices.WorldPreparationStatusChanged -= OnWorldPreparationStatusChanged;
    }

    public void SetEntityStore(EntityStore store)
    {
        _store = store;
    }

    /// <summary>每帧调用（由 GameBootstrap 驱动）。</summary>
    public void Update(double delta)
    {
        _updateAccum += delta;
        if (_updateAccum < UpdateInterval) return;
        _updateAccum = 0;

        RefreshDisplay();
    }

    /// <summary>切换面板可见性。</summary>
    public void Toggle()
    {
        if (_startupMode)
        {
            if (_panel != null)
                _panel.Visible = true;
            return;
        }

        if (_panel != null)
            _panel.Visible = !_panel.Visible;
    }

    public void EnterStartupMode()
    {
        _startupMode = true;
        ApplyPanelLayout();
        if (_loginBackground != null)
            _loginBackground.Visible = true;
        if (_panel != null)
            _panel.Visible = true;
    }

    public void ExitStartupMode()
    {
        _startupMode = false;
        ApplyPanelLayout();
        if (_loginBackground != null)
            _loginBackground.Visible = false;
        if (_panel != null)
            _panel.Visible = false;
    }

    // ══════════════════════════════════════════════════════════════════
    // UI 构建
    // ══════════════════════════════════════════════════════════════════

    private void BuildUI()
    {
        // ── 登录背景（启动模式下全屏遮罩，代替本地预生成地形） ──
        _loginBackground = new ColorRect
        {
            Name = "LoginBackground",
            Color = new Color(0.06f, 0.06f, 0.10f, 1f),
            AnchorLeft = 0f,
            AnchorTop = 0f,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetLeft = 0,
            OffsetTop = 0,
            OffsetRight = 0,
            OffsetBottom = 0,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        AddChild(_loginBackground);

        _panel = new PanelContainer { Name = "NetworkInfoPanel" };
        _panel.AddThemeStyleboxOverride("panel", MakePanelStyle());
        _panel.MouseFilter = Control.MouseFilterEnum.Stop;
        AddChild(_panel);
        ApplyPanelLayout();

        _vbox = new VBoxContainer();
        _vbox.AddThemeConstantOverride("separation", 4);
        _panel.AddChild(_vbox);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        _vbox.AddChild(margin);

        var innerVbox = new VBoxContainer();
        innerVbox.AddThemeConstantOverride("separation", 3);
        margin.AddChild(innerVbox);

        // 标题
        _lblTitle = MakeLabel("🌐 网络信息 [\\]", 12, new Color(0.3f, 0.8f, 1f));
        innerVbox.AddChild(_lblTitle);

        // 分隔
        innerVbox.AddChild(MakeSeparator());

        // 连接状态
        _lblConnectionState = MakeLabel("状态: 未启用", 11, new Color(0.6f, 0.6f, 0.6f));
        innerVbox.AddChild(_lblConnectionState);

        _lblSignalR = MakeLabel("SignalR: —", 10, new Color(0.7f, 0.7f, 0.7f));
        innerVbox.AddChild(_lblSignalR);

        _lblTcp = MakeLabel("TCP: —", 10, new Color(0.7f, 0.7f, 0.7f));
        innerVbox.AddChild(_lblTcp);

        _lblUptime = MakeLabel("连接时长: —", 10, new Color(0.7f, 0.7f, 0.7f));
        innerVbox.AddChild(_lblUptime);

        // 分隔
        innerVbox.AddChild(MakeSeparator());

        // 统计
        _lblStats = MakeLabel("📊 包: ↑0 ↓0  |  RPC: 0", 10, new Color(0.8f, 0.8f, 0.4f));
        innerVbox.AddChild(_lblStats);

        _lblTraffic = MakeLabel("📡 流量: ↑0 B  ↓0 B", 10, new Color(0.8f, 0.8f, 0.4f));
        innerVbox.AddChild(_lblTraffic);

        // 分隔
        innerVbox.AddChild(MakeSeparator());

        // 连接控制区
        var hostBox = new HBoxContainer();
        hostBox.AddThemeConstantOverride("separation", 4);
        innerVbox.AddChild(hostBox);

        var lblHost = MakeLabel("地址:", 10, new Color(0.7f, 0.7f, 0.7f));
        hostBox.AddChild(lblHost);

        _txtHost = new LineEdit
        {
            Text = "<SERVER_HOST>",
            CustomMinimumSize = new Vector2(120, 0),
            PlaceholderText = "服务器地址"
        };
        ArkTheme.ApplyFontSize(_txtHost, 10);
        hostBox.AddChild(_txtHost);

        var lblPort = MakeLabel("端口:", 10, new Color(0.7f, 0.7f, 0.7f));
        hostBox.AddChild(lblPort);

        _txtPort = new LineEdit
        {
            Text = "4000",
            CustomMinimumSize = new Vector2(60, 0),
            PlaceholderText = "端口"
        };
        ArkTheme.ApplyFontSize(_txtPort, 10);
        hostBox.AddChild(_txtPort);

        _connectBox = new HBoxContainer();
        _connectBox.AddThemeConstantOverride("separation", 6);
        innerVbox.AddChild(_connectBox);

        _btnConnect = new Button
        {
            Text = "🔗 连接",
            CustomMinimumSize = new Vector2(80, 28),
            FocusMode = Control.FocusModeEnum.None
        };
        ArkTheme.ApplyFontSize(_btnConnect, 10);
        _btnConnect.Pressed += OnConnectPressed;
        _connectBox.AddChild(_btnConnect);

        _btnDisconnect = new Button
        {
            Text = "❌ 断开",
            CustomMinimumSize = new Vector2(80, 28),
            FocusMode = Control.FocusModeEnum.None,
            Disabled = true
        };
        ArkTheme.ApplyFontSize(_btnDisconnect, 10);
        _btnDisconnect.Pressed += OnDisconnectPressed;
        _connectBox.AddChild(_btnDisconnect);

        _lblStatus = MakeLabel("", 9, new Color(0.5f, 0.5f, 0.5f));
        innerVbox.AddChild(_lblStatus);

        _lblPreloadState = MakeLabel("世界预载: 等待登录", 10, new Color(0.7f, 0.7f, 0.7f));
        innerVbox.AddChild(_lblPreloadState);

        innerVbox.AddChild(MakeSeparator());

        _lblAuthState = MakeLabel("账号: 未登录", 10, new Color(0.8f, 0.8f, 0.8f));
        innerVbox.AddChild(_lblAuthState);

        _lblCharacterState = MakeLabel("角色: 0 / 会话: —", 10, new Color(0.7f, 0.7f, 0.7f));
        innerVbox.AddChild(_lblCharacterState);

        var accountBox = new HBoxContainer();
        accountBox.AddThemeConstantOverride("separation", 4);
        innerVbox.AddChild(accountBox);

        _txtAccount = new LineEdit
        {
            PlaceholderText = "账号 / 凭证",
            CustomMinimumSize = new Vector2(120, 0)
        };
        ArkTheme.ApplyFontSize(_txtAccount, 10);
        accountBox.AddChild(_txtAccount);

        _txtPassword = new LineEdit
        {
            PlaceholderText = "密码 Hash / 凭证",
            Secret = true,
            CustomMinimumSize = new Vector2(120, 0)
        };
        ArkTheme.ApplyFontSize(_txtPassword, 10);
        accountBox.AddChild(_txtPassword);

        _btnLogin = new Button
        {
            Text = "登录",
            CustomMinimumSize = new Vector2(64, 28),
            FocusMode = Control.FocusModeEnum.None
        };
        ArkTheme.ApplyFontSize(_btnLogin, 10);
        _btnLogin.Pressed += OnLoginPressed;
        accountBox.AddChild(_btnLogin);

        innerVbox.AddChild(MakeSeparator());

        var createBox = new HBoxContainer();
        createBox.AddThemeConstantOverride("separation", 4);
        innerVbox.AddChild(createBox);

        _txtCharacterName = new LineEdit
        {
            PlaceholderText = "角色名",
            CustomMinimumSize = new Vector2(90, 0)
        };
        ArkTheme.ApplyFontSize(_txtCharacterName, 10);
        createBox.AddChild(_txtCharacterName);

        _txtFaction = new LineEdit
        {
            Text = "Nomad",
            PlaceholderText = "阵营",
            CustomMinimumSize = new Vector2(70, 0)
        };
        ArkTheme.ApplyFontSize(_txtFaction, 10);
        createBox.AddChild(_txtFaction);

        _txtClass = new LineEdit
        {
            Text = "Ranger",
            PlaceholderText = "职业",
            CustomMinimumSize = new Vector2(70, 0)
        };
        ArkTheme.ApplyFontSize(_txtClass, 10);
        createBox.AddChild(_txtClass);

        _btnCreateCharacter = new Button
        {
            Text = "创角",
            CustomMinimumSize = new Vector2(64, 28),
            FocusMode = Control.FocusModeEnum.None
        };
        ArkTheme.ApplyFontSize(_btnCreateCharacter, 10);
        _btnCreateCharacter.Pressed += OnCreateCharacterPressed;
        createBox.AddChild(_btnCreateCharacter);

        _txtStartingZone = new LineEdit
        {
            Text = "default",
            PlaceholderText = "世界 / 出生区"
        };
        ArkTheme.ApplyFontSize(_txtStartingZone, 10);
        innerVbox.AddChild(_txtStartingZone);

        _btnRefreshCharacters = new Button
        {
            Text = "刷新角色列表",
            CustomMinimumSize = new Vector2(0, 28),
            FocusMode = Control.FocusModeEnum.None
        };
        ArkTheme.ApplyFontSize(_btnRefreshCharacters, 10);
        _btnRefreshCharacters.Pressed += OnRefreshCharactersPressed;
        innerVbox.AddChild(_btnRefreshCharacters);

        _characterList = new ItemList
        {
            SelectMode = ItemList.SelectModeEnum.Single,
            CustomMinimumSize = new Vector2(0, 160)
        };
        _characterList.AllowReselect = true;
        innerVbox.AddChild(_characterList);

        _btnEnterWorld = new Button
        {
            Text = "选择角色并进入世界",
            CustomMinimumSize = new Vector2(0, 32),
            FocusMode = Control.FocusModeEnum.None
        };
        ArkTheme.ApplyFontSize(_btnEnterWorld, 10);
        _btnEnterWorld.Pressed += OnEnterWorldPressed;
        innerVbox.AddChild(_btnEnterWorld);

        _panel.Visible = true;
    }

    // ══════════════════════════════════════════════════════════════════
    // 刷新显示
    // ══════════════════════════════════════════════════════════════════

    private void RefreshDisplay()
    {
        var mgr = GameServices.NetworkManager;
        RefreshPreloadDisplay();

        if (!GameServices.IsNetworkEnabled || mgr is null)
        {
            SetText(_lblConnectionState, "状态: 网络未启用", new Color(0.5f, 0.5f, 0.5f));
            SetText(_lblSignalR, "SignalR: —");
            SetText(_lblTcp, "TCP: —");
            SetText(_lblUptime, "连接时长: —");
            SetText(_lblStats, "📊 包: ↑0 ↓0  |  RPC: 0");
            SetText(_lblTraffic, "📡 流量: ↑0 B  ↓0 B");
            UpdateButtons(false, false);
            return;
        }

        // 连接状态
        var state = mgr.ConnectionState;
        var (stateText, stateColor) = state switch
        {
            NetworkConnectionState.Connected    => ("● 已连接", new Color(0.2f, 0.9f, 0.3f)),
            NetworkConnectionState.Connecting   => ("◌ 连接中...", new Color(1f, 0.8f, 0.2f)),
            NetworkConnectionState.Reconnecting => ("◌ 重连中...", new Color(1f, 0.6f, 0.2f)),
            NetworkConnectionState.Disconnected => ("○ 未连接", new Color(0.9f, 0.3f, 0.3f)),
            _ => ("? 未知", new Color(0.5f, 0.5f, 0.5f))
        };
        SetText(_lblConnectionState, $"状态: {stateText}", stateColor);

        // SignalR
        var signalRState = mgr.SignalR.IsConnected ? "已连接" : "未连接";
        var signalRColor = mgr.SignalR.IsConnected
            ? new Color(0.2f, 0.9f, 0.3f)
            : new Color(0.9f, 0.3f, 0.3f);
        SetText(_lblSignalR, $"SignalR: {signalRState} ({mgr.SignalR.State})", signalRColor);

        // TCP
        var tcpConnected = mgr.Tcp.IsConnected;
        var tcpColor = tcpConnected
            ? new Color(0.2f, 0.9f, 0.3f)
            : new Color(0.9f, 0.3f, 0.3f);
        SetText(_lblTcp, $"TCP: {(tcpConnected ? "已连接" : "未连接")}", tcpColor);

        // 连接时长
        var stats = mgr.Stats;
        var uptime = stats.Uptime;
        SetText(_lblUptime, uptime > TimeSpan.Zero
            ? $"连接时长: {uptime:hh\\:mm\\:ss}"
            : "连接时长: —");

        // 统计
        SetText(_lblStats,
            $"📊 包: ↑{stats.PacketsSent} ↓{stats.PacketsReceived}  |  RPC: {stats.RpcCallCount}");

        SetText(_lblTraffic,
            $"📡 流量: ↑{NetworkStats.FormatBytes(stats.BytesSent)}  ↓{NetworkStats.FormatBytes(stats.BytesReceived)}");

        // 按钮状态
        bool isConnected = state == NetworkConnectionState.Connected;
        bool isConnecting = state == NetworkConnectionState.Connecting || state == NetworkConnectionState.Reconnecting;
        UpdateButtons(!isConnected && !isConnecting, isConnected);

        var sessionText = string.IsNullOrWhiteSpace(GameServices.RemoteSessionToken)
            ? "—"
            : $"{GameServices.RemoteSessionToken![..Math.Min(8, GameServices.RemoteSessionToken.Length)]}...";
        SetText(_lblCharacterState, $"角色: {_characterSlots.Count} / 会话: {sessionText}", new Color(0.7f, 0.7f, 0.7f));
    }

    private void RefreshPreloadDisplay()
    {
        var worldState = ResolveLocalWorldServiceState();
        var environmentReady = worldState?.HasWorldEnvironment != 0;
        var partyCount = worldState?.PartyMemberCount ?? 0;
        var nearbyCount = worldState?.NearbyEntityCount ?? 0;
        var snapshotReady = GameServices.RemoteWorldEcsCache?.IsSnapshotReady == true;

        var readyCount = (environmentReady ? 1 : 0) + (partyCount > 0 ? 1 : 0) + (nearbyCount >= 0 ? 1 : 0) + (snapshotReady ? 1 : 0);
        var color = snapshotReady && environmentReady
            ? new Color(0.2f, 0.9f, 0.3f)
            : new Color(1f, 0.8f, 0.2f);

        SetText(
            _lblPreloadState,
            $"世界预载: 环境{(environmentReady ? "✓" : "…")} / 队伍 {partyCount} / 周边 {nearbyCount} / 快照{(snapshotReady ? "✓" : "…")} [{readyCount}/4]",
            color);
    }

    private RemoteWorldServiceState? ResolveLocalWorldServiceState()
    {
        int localEntityId = GameServices.RemoteWorldEcsCache?.LocalPresentationEntityId ?? 0;
        if (_store is null || localEntityId <= 0)
            return null;

        var entity = _store.GetEntityById(localEntityId);
        return !entity.IsNull && entity.TryGetComponent<RemoteWorldServiceState>(out var worldState)
            ? worldState
            : null;
    }

    // ══════════════════════════════════════════════════════════════════
    // 连接控制
    // ══════════════════════════════════════════════════════════════════

    private async void OnConnectPressed()
    {
        var mgr = GameServices.NetworkManager;
        if (mgr is null)
        {
            SetText(_lblStatus, "网络未初始化", new Color(0.9f, 0.3f, 0.3f));
            return;
        }

        var host = _txtHost?.Text ?? "<SERVER_HOST>";
        if (!int.TryParse(_txtPort?.Text ?? "4000", out var port))
            port = 5000;

        SetText(_lblStatus, "正在连接...", new Color(1f, 0.8f, 0.2f));
        UpdateButtons(false, false);

        try
        {
            var signalRUrl = $"https://{host}:{port}/gamehub";
            var tcpPort = port + 2; // TCP 传输端口 = HTTPS端口 + 2（避免与 Kestrel HTTP 冲突）
            await mgr.ConnectAsync(signalRUrl, host, tcpPort);
            SetText(_lblStatus, "连接成功", new Color(0.2f, 0.9f, 0.3f));
        }
        catch (System.Exception ex)
        {
            SetText(_lblStatus, $"连接失败: {ex.Message}", new Color(0.9f, 0.3f, 0.3f));
            GD.PrintErr($"[NetworkInfoHud] Connect failed: {ex.Message}");
        }
    }

    private async void OnLoginPressed()
    {
        var host = _txtHost?.Text ?? "<SERVER_HOST>";
        if (!int.TryParse(_txtPort?.Text ?? "4000", out var port))
            port = 4000;

        var account = _txtAccount?.Text?.Trim() ?? string.Empty;
        var password = _txtPassword?.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(password))
        {
            SetText(_lblAuthState, "账号: 请填写登录凭证", new Color(0.9f, 0.3f, 0.3f));
            return;
        }

        SetText(_lblAuthState, "账号: 登录中...", new Color(1f, 0.8f, 0.2f));
        var result = await _workflowFacade.LoginAsync(host, port, account, password);
        if (result.Success)
        {
            SetText(_lblAuthState, $"账号: {result.PlayerName} ({GameServices.RemoteAccountId})", new Color(0.2f, 0.9f, 0.3f));
            PopulateCharacterList(_workflowFacade.ReadCachedCharacterList());
            SetText(_lblStatus, "登录成功，可创建或选择角色", new Color(0.2f, 0.9f, 0.3f));
            return;
        }

        SetText(_lblAuthState, $"账号: 登录失败 - {result.ErrorMessage}", new Color(0.9f, 0.3f, 0.3f));
    }

    private async void OnRefreshCharactersPressed()
    {
        if (GameServices.Character is null || GameServices.RemoteAccountId == System.Guid.Empty)
        {
            SetText(_lblStatus, "请先完成登录", new Color(0.9f, 0.3f, 0.3f));
            return;
        }

        var list = await _workflowFacade.RefreshCharacterListAsync();
        PopulateCharacterList(list);
        SetText(_lblStatus, list.IsAvailable
            ? $"已加载 {list.Characters.Count} 个角色"
            : "刷新角色失败", list.IsAvailable ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.9f, 0.3f, 0.3f));
    }

    private async void OnCreateCharacterPressed()
    {
        if (GameServices.Character is null || GameServices.RemoteAccountId == System.Guid.Empty)
        {
            SetText(_lblStatus, "请先完成登录", new Color(0.9f, 0.3f, 0.3f));
            return;
        }

        var name = _txtCharacterName?.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            SetText(_lblStatus, "请输入角色名", new Color(0.9f, 0.3f, 0.3f));
            return;
        }

        var faction = string.IsNullOrWhiteSpace(_txtFaction?.Text) ? "Nomad" : _txtFaction!.Text.Trim();
        var characterClass = string.IsNullOrWhiteSpace(_txtClass?.Text) ? "Ranger" : _txtClass!.Text.Trim();
        var startingZone = string.IsNullOrWhiteSpace(_txtStartingZone?.Text) ? "default" : _txtStartingZone!.Text.Trim();
        var result = await _workflowFacade.CreateCharacterAsync(GameServices.RemoteAccountId, name, faction, characterClass, startingZone);

        if (result.Success)
        {
            PopulateCharacterList(_workflowFacade.ReadCachedCharacterList());
            SetText(_lblStatus, $"角色创建成功: {name}", new Color(0.2f, 0.9f, 0.3f));
            if (_txtCharacterName is not null)
                _txtCharacterName.Text = string.Empty;
            return;
        }

        SetText(_lblStatus, $"角色创建失败: {result.ErrorMessage}", new Color(0.9f, 0.3f, 0.3f));
    }

    private async void OnEnterWorldPressed()
    {
        if (GameServices.Character is null)
        {
            SetText(_lblStatus, "角色服务未初始化", new Color(0.9f, 0.3f, 0.3f));
            return;
        }

        var selectedSlot = GetSelectedCharacterSlot();
        if (selectedSlot is null)
        {
            SetText(_lblStatus, "请选择角色", new Color(0.9f, 0.3f, 0.3f));
            return;
        }

        if (_btnEnterWorld != null)
            _btnEnterWorld.Disabled = true;

        try
        {
            SetText(_lblStatus, "正在锁定角色会话...", new Color(1f, 0.8f, 0.2f));
            var selectResult = await _workflowFacade.SelectCharacterAsync(selectedSlot.CharacterId);
            if (!selectResult.Success)
            {
                SetText(_lblStatus, $"选角失败: {selectResult.ErrorMessage}", new Color(0.9f, 0.3f, 0.3f));
                return;
            }

            var worldId = string.IsNullOrWhiteSpace(selectedSlot.LastZone)
                ? (_txtStartingZone?.Text?.Trim() ?? "default")
                : selectedSlot.LastZone;
            SetText(_lblStatus, "正在准备世界环境...", new Color(1f, 0.8f, 0.2f));
            var prepared = await GameServices.PrepareWorldAsync(selectResult.CharacterId, worldId);
            if (!prepared)
            {
                if (_lblStatus?.Text?.StartsWith("进入世界失败") != true)
                    SetText(_lblStatus, "进入世界失败", new Color(0.9f, 0.3f, 0.3f));
                return;
            }
            SetText(_lblStatus, $"已完成进入前准备: {worldId}", new Color(0.2f, 0.9f, 0.3f));

            SetText(_lblCharacterState, $"角色: {_characterSlots.Count} / 当前: {selectedSlot.Name}", new Color(0.2f, 0.9f, 0.3f));
            OnWorldEntryCompleted?.Invoke();
        }
        finally
        {
            if (_btnEnterWorld != null)
                _btnEnterWorld.Disabled = false;
        }
    }

    private void PopulateCharacterList(CharacterListWorkflowResult list)
    {
        _characterSlots.Clear();
        _characterList?.Clear();
        if (!list.IsAvailable || _characterList is null)
            return;

        foreach (var slot in list.Characters)
        {
            _characterSlots.Add(slot);
            _characterList.AddItem($"Lv.{slot.Level} {slot.Name} [{slot.Faction}/{slot.CharacterClass}] - {slot.LastZone}");
        }

        if (_characterSlots.Count > 0)
            _characterList.Select(0);

        SetText(_lblCharacterState,
            $"角色: {_characterSlots.Count}/{list.MaxSlots} / 会话: {(string.IsNullOrWhiteSpace(GameServices.RemoteSessionToken) ? "—" : "已建立")}",
            new Color(0.7f, 0.7f, 0.7f));
    }

    private CharacterSlotViewModel? GetSelectedCharacterSlot()
    {
        if (_characterList is null)
            return null;

        var selected = _characterList.GetSelectedItems();
        if (selected.Length == 0)
            return null;

        var index = selected[0];
        return index >= 0 && index < _characterSlots.Count ? _characterSlots[index] : null;
    }

    private async void OnDisconnectPressed()
    {
        var mgr = GameServices.NetworkManager;
        if (mgr is null) return;

        SetText(_lblStatus, "正在断开...", new Color(1f, 0.8f, 0.2f));
        UpdateButtons(false, false);

        try
        {
            await mgr.DisconnectAsync();
            SetText(_lblStatus, "已断开", new Color(0.7f, 0.7f, 0.7f));
        }
        catch (System.Exception ex)
        {
            SetText(_lblStatus, $"断开失败: {ex.Message}", new Color(0.9f, 0.3f, 0.3f));
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // 辅助
    // ══════════════════════════════════════════════════════════════════

    private void UpdateButtons(bool connectEnabled, bool disconnectEnabled)
    {
        if (_btnConnect != null) _btnConnect.Disabled = !connectEnabled;
        if (_btnDisconnect != null) _btnDisconnect.Disabled = !disconnectEnabled;
    }

    private static void SetText(Label? label, string text, Color? color = null)
    {
        if (label is null) return;
        label.Text = text;
        if (color.HasValue)
            label.AddThemeColorOverride("font_color", color.Value);
    }

    private static Label MakeLabel(string text, int designFontSize, Color color)
    {
        var lbl = ArkTheme.MakeLabel(text, designFontSize, color);
        return lbl;
    }

    private static HSeparator MakeSeparator()
    {
        var sep = new HSeparator();
        sep.AddThemeConstantOverride("separation", 4);
        return sep;
    }

    private void ApplyPanelLayout()
    {
        if (_panel is null)
            return;

        if (_startupMode)
        {
            const int panelWidth = 680;
            const int panelHeight = 720;
            _panel.AnchorLeft = 0.5f;
            _panel.AnchorRight = 0.5f;
            _panel.AnchorTop = 0.5f;
            _panel.AnchorBottom = 0.5f;
            _panel.GrowHorizontal = Control.GrowDirection.Both;
            _panel.GrowVertical = Control.GrowDirection.Both;
            _panel.OffsetLeft = -(panelWidth / 2);
            _panel.OffsetRight = panelWidth / 2;
            _panel.OffsetTop = -(panelHeight / 2);
            _panel.OffsetBottom = panelHeight / 2;
            SetText(_lblTitle, "🌐 登录 / 世界预加载", new Color(0.3f, 0.8f, 1f));
        }
        else
        {
            const int panelWidth = 320;
            const int panelHeight = 680;
            _panel.AnchorLeft = 1f;
            _panel.AnchorRight = 1f;
            _panel.AnchorTop = 0f;
            _panel.AnchorBottom = 0f;
            _panel.GrowHorizontal = Control.GrowDirection.Begin;
            _panel.GrowVertical = Control.GrowDirection.End;
            _panel.OffsetLeft = -panelWidth - 12;
            _panel.OffsetRight = -12;
            _panel.OffsetTop = 12;
            _panel.OffsetBottom = 12 + panelHeight;
            SetText(_lblTitle, "🌐 网络信息 [\\]", new Color(0.3f, 0.8f, 1f));
        }
    }

    private void OnWorldPreparationStatusChanged(string status)
    {
        SetText(_lblStatus, status, new Color(0.7f, 0.85f, 1f));
        RefreshPreloadDisplay();
    }

    private static StyleBoxFlat MakePanelStyle() => new()
    {
        BgColor = new Color(0.08f, 0.08f, 0.12f, 0.88f),
        BorderColor = new Color(0.3f, 0.6f, 1f, 0.6f),
        BorderWidthBottom = 1,
        BorderWidthTop = 1,
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        ContentMarginLeft = 4,
        ContentMarginRight = 4,
        ContentMarginTop = 4,
        ContentMarginBottom = 4
    };
}
