using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using RebornMaterial;

namespace BossMod;

public sealed class BossModulePrePullHintsWindow : RebornUI.RebornWindow
{
    private const string WindowID = "###Boss module pre-pull hints";
    private const string NeverShowTooltip = "You can turn this popup back on in this encounter's settings.";

    private readonly BossModuleManager _mgr;
    private BossModule? _displayedModule;
    private BossModule? _dismissedModule;
    private bool _permanentlySuppressing;

    protected override M3Density Density => SettingsDensity;

    public BossModulePrePullHintsWindow(BossModuleManager mgr) : base(WindowID, false, new(450f, 180f))
    {
        _mgr = mgr;
        RespectCloseHotkey = false;
    }

    public override void PreOpenCheck()
    {
        var module = _mgr.ActiveModule;
        _displayedModule = module;

        var config = BossModuleManager.Config;
        var show = config.ShowPrePullHints && module != null && module.StateMachine.ActiveState == null && module.Info is { HasPrePullHints: true } info
            && module.PrePullHints.Length > 0 && config.ShowPrePullHintsFor(info.PrimaryActorOID) && !ReferenceEquals(module, _dismissedModule);

        IsOpen = show;
        if (show)
        {
            var encounterName = string.IsNullOrEmpty(module!.PrimaryActor.Name) ? module.GetType().Name : module.PrimaryActor.Name;
            WindowName = Loc.Format("Pre-fight hints: {0}", encounterName) + WindowID;
        }
    }

    public override void Draw()
    {
        var module = _displayedModule;
        if (module == null)
        {
            return;
        }

        if (M3.IsInitialized)
        {
            DrawThemed(module);
        }
        else
        {
            DrawClassic(module);
        }
    }

    private void DrawThemed(BossModule module)
    {
        M3Text.Draw(Loc.Tr("Before the pull"), M3TextStyle.Heading);
        ImGui.Dummy(new(0f, M3.Space1));
        using (ImRaii.TextWrapPos(0f))
        {
            var hints = module.PrePullHints;
            var len = hints.Length;
            for (var i = 0; i < len; ++i)
            {
                M3Text.Bullet(Loc.Tr(hints[i]));
            }
        }

        M3Widgets.Divider(6f);

        if (M3Widgets.Button("##dismiss", Loc.Tr("Dismiss for now"), M3ButtonStyle.Filled, FontAwesomeIcon.Check))
        {
            Dismiss(module);
        }

        ImGui.SameLine(0f, M3.Space2);
        if (M3Widgets.Button("##never", Loc.Tr("Never show again"), M3ButtonStyle.Text, FontAwesomeIcon.EyeSlash, tooltip: Loc.Tr(NeverShowTooltip)))
        {
            NeverShowAgain(module);
        }
    }

    private void DrawClassic(BossModule module)
    {
        ImGui.TextWrapped(Loc.Tr("Before the pull:"));
        ImGui.Separator();
        using (ImRaii.TextWrapPos(0f))
        {
            var hints = module.PrePullHints;
            var len = hints.Length;
            for (var i = 0; i < len; ++i)
            {
                ImGui.Bullet();
                ImGui.SameLine();
                ImGui.TextUnformatted(Loc.Tr(hints[i]));
            }
        }

        ImGui.Spacing();
        ImGui.Separator();

        if (ImGui.Button(Loc.Id("Dismiss for now")))
        {
            Dismiss(module);
        }

        ImGui.SameLine();
        if (ImGui.Button(Loc.Id("Never show again")))
        {
            NeverShowAgain(module);
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Loc.Tr(NeverShowTooltip));
        }
    }

    private void Dismiss(BossModule module)
    {
        _dismissedModule = module;
        IsOpen = false;
    }

    private void NeverShowAgain(BossModule module)
    {
        _permanentlySuppressing = true;
        if (module.Info != null)
        {
            BossModuleManager.Config.SetShowPrePullHintsFor(module.Info.PrimaryActorOID, false);
        }
        IsOpen = false;
    }

    public override void PostDraw()
    {
        base.PostDraw();
        // Closing with the title-bar X should behave like "Dismiss for now" rather than reopening next frame.
        if (!IsOpen && _displayedModule != null && !_permanentlySuppressing)
        {
            _dismissedModule = _displayedModule;
        }
        _permanentlySuppressing = false;
    }
}
