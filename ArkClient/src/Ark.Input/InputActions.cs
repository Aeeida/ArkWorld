using Godot;

namespace Ark.GameInput;

/// <summary>
/// 输入动作注册 — 在 InputMap 中注册所有游戏动作。
/// 仅在动作不存在时注册，避免重复。
/// </summary>
public static class InputActions
{
    private static readonly HashSet<string> LoggedActions = [];

    public static void RegisterAll()
    {
        RegisterAction("move_left", Key.A);
        RegisterAction("move_right", Key.D);
        RegisterAction("move_forward", Key.W);
        RegisterAction("move_backward", Key.S);
        RegisterAction("jump", Key.Space);
        RegisterAction("sprint", Key.Shift);
        RegisterAction("ui_cancel", Key.Escape);

        RegisterAction("build_mode",  Key.B);
        RegisterAction("build_rotate", Key.R);
        RegisterAction("interact",    Key.F);
        RegisterAction("combat_mode", Key.C);
        RegisterAction("reload",      Key.R);

        // 战斗机飞行控制
        RegisterAction("aircraft_ascend",      Key.I);
        RegisterAction("aircraft_descend",     Key.K);
        RegisterAction("aircraft_strafe_left", Key.J);
        RegisterAction("aircraft_strafe_right", Key.L);

        // 火箭/飞船控制
        RegisterAction("spacecraft_throttle_up", Key.Shift);
        RegisterAction("spacecraft_throttle_down", Key.Ctrl);
        RegisterAction("spacecraft_roll_left", Key.Q);
        RegisterAction("spacecraft_roll_right", Key.E);
        RegisterAction("spacecraft_hover", Key.H);
        RegisterAction("spacecraft_engine_cutoff", Key.X);

        // 调试/环境
        RegisterAction("toggle_network_info_hud", Key.Backslash);
        RegisterAction("env_preset_beautiful_wild", Key.F7);
        RegisterAction("env_preset_dark_forest", Key.F8);
        RegisterAction("env_preset_horror_dungeon", Key.F9);
        RegisterAction("env_preset_modern_city", Key.F9, shift: true);
        RegisterAction("env_preset_ruin_archaeology", Key.F10);
        RegisterAction("env_preset_mystic_sky", Key.F11);
        RegisterAction("env_preset_natural", Key.F12);
        RegisterAction("env_preset_space_universe", Key.F12, shift: true);
    }

    public static void RegisterAction(string action, Key key, bool shift = false, bool ctrl = false, bool alt = false)
    {
        if (!InputMap.HasAction(action))
            InputMap.AddAction(action);

        var ev = new InputEventKey
        {
            PhysicalKeycode = key,
            ShiftPressed = shift,
            CtrlPressed = ctrl,
            AltPressed = alt,
        };

        if (!HasMatchingEvent(action, ev))
            InputMap.ActionAddEvent(action, ev);

        if (LoggedActions.Add(action))
            GD.Print($"[InputMap] Action '{action}' bound to {Describe(ev)}");
    }

    private static bool HasMatchingEvent(string action, InputEventKey candidate)
    {
        foreach (var existing in InputMap.ActionGetEvents(action))
        {
            if (existing is not InputEventKey keyEvent)
                continue;

            if (keyEvent.PhysicalKeycode == candidate.PhysicalKeycode
                && keyEvent.ShiftPressed == candidate.ShiftPressed
                && keyEvent.CtrlPressed == candidate.CtrlPressed
                && keyEvent.AltPressed == candidate.AltPressed)
            {
                return true;
            }
        }

        return false;
    }

    private static string Describe(InputEventKey ev)
    {
        var parts = new List<string>();
        if (ev.CtrlPressed) parts.Add("Ctrl");
        if (ev.ShiftPressed) parts.Add("Shift");
        if (ev.AltPressed) parts.Add("Alt");
        parts.Add(ev.PhysicalKeycode.ToString());
        return string.Join("+", parts);
    }
}
