using Godot;

namespace Ark.UI;

public partial class NpcInteractionPanel : CanvasLayer
{
    private PanelContainer? _panel;
    private Label? _title;
    private Label? _detail;

    public override void _Ready()
    {
        Layer = 9;

        _panel = new PanelContainer
        {
            Visible = false,
            AnchorLeft = 1,
            AnchorTop = 0.5f,
            AnchorRight = 1,
            AnchorBottom = 0.5f,
            OffsetLeft = -320,
            OffsetTop = -120,
            OffsetRight = -16,
            OffsetBottom = 120,
        };
        AddChild(_panel);

        var box = new VBoxContainer();
        _panel.AddChild(box);

        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", 20);
        box.AddChild(_title);

        _detail = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _detail.AddThemeFontSizeOverride("font_size", 14);
        box.AddChild(_detail);

        var actions = new HBoxContainer();
        box.AddChild(actions);

        foreach (var text in new[] { "对话", "观察", "关闭" })
        {
            var button = new Button { Text = text, CustomMinimumSize = new Vector2(80, 32) };
            if (text == "关闭")
                button.Pressed += HidePanel;
            actions.AddChild(button);
        }
    }

    public void ShowFor(string npcName, int entityId, string detail)
    {
        if (_panel == null || _title == null || _detail == null)
            return;

        _title.Text = npcName;
        _detail.Text = $"Entity: {entityId}\n{detail}";
        _panel.Visible = true;
    }

    public void HidePanel()
    {
        if (_panel != null)
            _panel.Visible = false;
    }
}
