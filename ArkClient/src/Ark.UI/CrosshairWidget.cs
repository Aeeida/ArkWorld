using Godot;

namespace Ark.UI;

/// <summary>
/// 十字准心控件 — 固定在屏幕上方偏前位置的 + 形准心。
///
/// 功能：
///   • 4 条短线组成十字形
///   • 默认白色，瞄准敌人时变红
///   • 固定位置在屏幕水平中央、垂直 35% 处（偏上），确保准心始终指向角色前方
/// </summary>
public partial class CrosshairWidget : Control
{
    private static readonly Color NormalColor = new(1f, 1f, 1f, 0.85f);
    private static readonly Color EnemyColor  = new(1f, 0.15f, 0.1f, 0.95f);

    // 十字臂参数
    private const float ArmLength  = 10f;
    private const float ArmWidth   = 2f;
    private const float GapRadius  = 3f;

    /// <summary>准心在屏幕垂直方向的位置比例（0=顶部, 0.5=中央, 1=底部）。</summary>
    private const float VerticalAnchor = 0.35f;

    private Color _currentColor = NormalColor;
    private Label? _targetLabel;

    public override void _Ready()
    {
        // 锚定到屏幕水平中央、垂直 35%
        AnchorLeft   = 0.5f;
        AnchorTop    = VerticalAnchor;
        AnchorRight  = 0.5f;
        AnchorBottom = VerticalAnchor;

        float totalSize = (GapRadius + ArmLength) * 2 + 4;
        OffsetLeft   = -totalSize * 0.5f;
        OffsetTop    = -totalSize * 0.5f;
        OffsetRight  =  totalSize * 0.5f;
        OffsetBottom =  totalSize * 0.5f;

        MouseFilter = MouseFilterEnum.Ignore;

        _targetLabel = new Label
        {
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            Position = new Vector2(-120, 22),
            Size = new Vector2(240, 24),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _targetLabel.AddThemeFontSizeOverride("font_size", 14);
        _targetLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.9f));
        AddChild(_targetLabel);
    }

    /// <summary>设置当前相机缩放距离（保留接口兼容性）。</summary>
    public void SetZoomDistance(float zoom) { }

    /// <summary>设置是否正在瞄准敌人（改变颜色）。</summary>
    public void SetEnemyHover(bool isEnemy)
    {
        var newColor = isEnemy ? EnemyColor : NormalColor;
        if (_currentColor != newColor)
        {
            _currentColor = newColor;
            QueueRedraw();
        }
    }

    public void SetTargetInfo(string? text, bool isEnemy)
    {
        if (_targetLabel == null)
            return;

        if (string.IsNullOrWhiteSpace(text))
        {
            _targetLabel.Visible = false;
            _targetLabel.Text = string.Empty;
            return;
        }

        _targetLabel.Visible = true;
        _targetLabel.Text = text;
        _targetLabel.AddThemeColorOverride("font_color", isEnemy ? EnemyColor : NormalColor);
    }

    public override void _Draw()
    {
        var center = Size * 0.5f;

        // 上臂
        DrawRect(new Rect2(
            center.X - ArmWidth * 0.5f,
            center.Y - GapRadius - ArmLength,
            ArmWidth, ArmLength), _currentColor);

        // 下臂
        DrawRect(new Rect2(
            center.X - ArmWidth * 0.5f,
            center.Y + GapRadius,
            ArmWidth, ArmLength), _currentColor);

        // 左臂
        DrawRect(new Rect2(
            center.X - GapRadius - ArmLength,
            center.Y - ArmWidth * 0.5f,
            ArmLength, ArmWidth), _currentColor);

        // 右臂
        DrawRect(new Rect2(
            center.X + GapRadius,
            center.Y - ArmWidth * 0.5f,
            ArmLength, ArmWidth), _currentColor);
    }
}
