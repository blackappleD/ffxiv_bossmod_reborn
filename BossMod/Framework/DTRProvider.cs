using BossMod.AI;
using BossMod.Autorotation;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Utility.Raii;

namespace BossMod;

internal sealed class DTRProvider : IDisposable
{
    private readonly RotationModuleManager _mgr;
    private readonly AIManager _ai;
    private readonly IDtrBarEntry _autorotationEntry = Service.DtrBar.Get("bmr-autorotation");
    private readonly IDtrBarEntry _aiEntry = Service.DtrBar.Get("bmr-ai");
    private static readonly AIConfig _aiConfig = Service.Config.Get<AIConfig>();
    private bool _wantOpenPopup;
    private bool _wantOpenAIPopup;

    public DTRProvider(RotationModuleManager manager, AIManager ai)
    {
        _mgr = manager;
        _ai = ai;

        _autorotationEntry.OnClick = _ => _wantOpenPopup = true;
        _aiEntry.Tooltip = Loc.Tr("Left Click => Toggle Enabled\nRight Click => Select Configuration");

        _aiEntry.OnClick = e =>
        {
            if (e.ClickType == MouseClickType.Right)
            {
                _wantOpenAIPopup = true;
            }
            else if (_ai.Beh == null)
            {
                _ai.SwitchToFollow(_aiConfig.FollowSlot);
            }
            else
            {
                _ai.SwitchToIdle();
            }
        };
    }

    public void Dispose()
    {
        _autorotationEntry.Remove();
        _aiEntry.Remove();
    }

    public void Update()
    {
        var show = RotationModuleManager.Config.ShowDTR != AutorotationConfig.DtrStatus.None;
        _autorotationEntry.Shown = show;
        if (show)
        {
            var (icon, name) = _mgr.Preset == null ? (BitmapFontIcon.SwordSheathed, Loc.Tr("Idle")) : _mgr.Preset == RotationModuleManager.ForceDisable ? (BitmapFontIcon.SwordSheathed, Loc.Tr("Disabled")) : (BitmapFontIcon.SwordUnsheathed, _mgr.Preset.Name);
            Payload prefix = RotationModuleManager.Config.ShowDTR == AutorotationConfig.DtrStatus.TextOnly ? new TextPayload("bmr: ") : new IconPayload(icon);
            _autorotationEntry.Text = new SeString(prefix, new TextPayload(name));
        }

        var show2 = _aiConfig.ShowDTR;
        _aiEntry.Shown = show2;
        var beh = _ai.Beh;
        if (show2)
        {
            _aiEntry.Text = Loc.Tr(beh == null ? "AI: Off" : "AI: On");
        }

        if (_wantOpenPopup && _mgr.Player != null)
        {
            ImGui.OpenPopup("bmr_dtr_menu");
            _wantOpenPopup = false;
        }

        using (var popup = ImRaii.Popup("bmr_dtr_menu"))
        {
            if (popup)
            {
                if (UIRotationWindow.DrawRotationSelector(_mgr))
                {
                    ImGui.CloseCurrentPopup();
                }
            }
        }

        if (_wantOpenAIPopup && _mgr.Player != null)
        {
            ImGui.OpenPopup("bmr_dtr_ai_menu");
            _wantOpenAIPopup = false;
        }

        using (var popup = ImRaii.Popup("bmr_dtr_ai_menu"))
        {
            if (popup)
            {
                DrawAIMenu();
            }
        }
    }

    private void DrawAIMenu()
    {
        var configModified = false;

        ImGui.TextDisabled(Loc.Tr("Follow party slot"));
        if (ImGui.Selectable(Loc.Id("<idle>"), _ai.Beh == null))
        {
            _ai.SwitchToIdle();
        }
        var party = _ai.WorldState.Party.WithSlot(true);
        var len = party.Length;
        for (var i = 0; i < len; ++i)
        {
            ref readonly var p = ref party[i];
            var slot = p.Item1;
            if (ImGui.Selectable($"{p.Item2.Name}##slot{slot}", _ai.Beh != null && _ai.MasterSlot == slot))
            {
                _ai.SwitchToFollow(slot);
                _aiConfig.FollowSlot = slot;
                configModified = true;
            }
        }

        ImGui.Separator();
        ImGui.TextDisabled(Loc.Tr("Autorotation AI preset"));
        var current = _aiConfig.AIAutorotPresetName;
        if (ImGui.Selectable(Loc.Id("Deactivate"), current == null))
        {
            _ai.SetAIPreset(null);
            configModified = true;
        }
        foreach (var preset in _mgr.Database.Presets.AllPresets)
        {
            if (ImGui.Selectable($"{preset.Name}##aipreset", preset.Name == current))
            {
                _ai.SetAIPreset(preset);
                configModified = true;
            }
        }

        ImGui.Separator();
        if (ImGui.Selectable(Loc.Id("Open AI window")))
        {
            _aiConfig.DrawUI = true;
            configModified = true;
        }

        if (configModified)
        {
            _aiConfig.Modified.Fire();
        }
    }
}
