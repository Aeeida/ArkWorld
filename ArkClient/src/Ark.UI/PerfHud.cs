using Godot;

namespace Ark.UI;

/// <summary>
/// 性能 HUD — 显示 FPS、实体数等调试信息。
/// </summary>
public partial class PerfHud : CanvasLayer
{
    private Label? _label;
    private int _frameCount;
    private double _fpsAccum;

    /// <summary>外部每帧注入的附加信息（如小队状态、GPU 状态）。</summary>
    public string ExtraInfo { get; set; } = "";

    /// <summary>外部注入的实体计数。</summary>
    public int EntityCount { get; set; }

    public override void _Ready()
    {
        Layer = 10;

        _label = new Label
        {
            Position = new Vector2(10, 40),
            Text = "",
        };
        _label.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(12));
        _label.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.2f));
        AddChild(_label);
    }

    public void Update(double delta)
    {
        _fpsAccum += delta;
        _frameCount++;
        if (_fpsAccum < 0.5) return;

        double avgFps = _frameCount / _fpsAccum;
        _fpsAccum   = 0;
        _frameCount = 0;

        if (_label != null)
        {
            _label.Text =
                $"FPS: {avgFps:F0}  |  " +
                $"Entities: {EntityCount}  |  " +
                ExtraInfo;
        }
    }
}
