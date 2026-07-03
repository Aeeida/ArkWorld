namespace Ark.UI;

/// <summary>
/// 全局 UI 主题工具 — 集中管理字体缩放、通用样式生成。
/// 所有 UI 面板通过此类获取字体大小，确保全局一致。
/// </summary>
public static class ArkTheme
{
    /// <summary>全局字体缩放倍数（1.0 = 默认，3.0 = 3倍放大）。</summary>
    public static float FontScale { get; set; } = 2.0f;

    /// <summary>将设计时字号乘以全局缩放倍数。</summary>
    public static int ScaledFontSize(int designSize)
        => (int)(designSize * FontScale);

    /// <summary>通用标签 — 创建带缩放字号和颜色的 Label。</summary>
    public static Godot.Label MakeLabel(string text, int designFontSize, Godot.Color color)
    {
        var lbl = new Godot.Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", ScaledFontSize(designFontSize));
        lbl.AddThemeColorOverride("font_color", color);
        return lbl;
    }

    /// <summary>为已有 Label 应用缩放字号。</summary>
    public static void ApplyFontSize(Godot.Label label, int designFontSize)
        => label.AddThemeFontSizeOverride("font_size", ScaledFontSize(designFontSize));

    /// <summary>为已有 Button 应用缩放字号。</summary>
    public static void ApplyFontSize(Godot.Button button, int designFontSize)
        => button.AddThemeFontSizeOverride("font_size", ScaledFontSize(designFontSize));

    /// <summary>为已有 LineEdit 应用缩放字号。</summary>
    public static void ApplyFontSize(Godot.LineEdit lineEdit, int designFontSize)
        => lineEdit.AddThemeFontSizeOverride("font_size", ScaledFontSize(designFontSize));
}
