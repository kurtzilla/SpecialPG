#nullable enable
using Godot;

/// <summary>Round CheckButtons for each debug channel; updates <see cref="DebugGridOverlay"/>.</summary>
public partial class DebugChannelPanel : PanelContainer
{
    private DebugGridOverlay? _overlay;

    public override void _Ready()
    {
        var scene = GetTree().CurrentScene;
        var grid = scene?.GetNodeOrNull<GameRoot>("GridMap");
        _overlay = grid?.GetNodeOrNull<DebugGridOverlay>("DebugGridOverlay");
        WireCheck("WalkabilityToggle");
        WireCheck("VerticalLinksToggle");
        WireCheck("RayPickToggle");
        WireCheck("PathsToggle");
    }

    private void WireCheck(string nodeName)
    {
        var btn = GetNode<CheckButton>(nodeName);
        btn.Toggled += _ => PushAllChannelsFromUi();
    }

    public void PushAllChannelsFromUi()
    {
        if (_overlay is null)
        {
            return;
        }

        var ch = DebugDrawChannel.None;
        if (GetNode<CheckButton>("WalkabilityToggle").ButtonPressed)
        {
            ch |= DebugDrawChannel.Walkability;
        }

        if (GetNode<CheckButton>("VerticalLinksToggle").ButtonPressed)
        {
            ch |= DebugDrawChannel.VerticalLinks;
        }

        if (GetNode<CheckButton>("RayPickToggle").ButtonPressed)
        {
            ch |= DebugDrawChannel.RayPick;
        }

        if (GetNode<CheckButton>("PathsToggle").ButtonPressed)
        {
            ch |= DebugDrawChannel.Paths;
        }

        _overlay.SetChannels(ch);
    }
}
