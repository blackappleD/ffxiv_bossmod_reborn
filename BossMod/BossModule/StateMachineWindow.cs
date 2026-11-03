using Dalamud.Bindings.ImGui;

namespace BossMod;

public sealed class StateMachineWindow : UIWindow
{
    private readonly Timeline _timeline = new();
    private readonly ColumnStateMachineTree _col;

    public StateMachineWindow(BossModule module) : base(Loc.Id($"{module.GetType().Name} timeline"), true, new(600f, 600f))
    {
        _col = _timeline.Columns.Add(new ColumnStateMachineTree(_timeline, new(module.StateMachine), module.StateMachine));
        _timeline.MaxTime = _col.Tree.TotalMaxTime;
    }

    public override void Draw()
    {
        if (ImGui.CollapsingHeader(Loc.Id("Settings")))
        {
            ImGui.Checkbox(Loc.Id("Draw unnamed nodes"), ref _col.DrawUnnamedNodes);
            ImGui.Checkbox(Loc.Id("Draw tankbuster nodes only"), ref _col.DrawTankbusterNodesOnly);
            ImGui.Checkbox(Loc.Id("Draw raidwide nodes only"), ref _col.DrawRaidwideNodesOnly);
        }

        _timeline.CurrentTime = null;
        if (_col.ControlledSM?.ActiveState != null)
        {
            var dt = _col.ControlledSM.ActiveState.Duration - _col.ControlledSM.TimeSinceTransitionClamped;
            var activeNode = _col.Tree.Nodes[_col.ControlledSM.ActiveState.ID];
            _timeline.CurrentTime = activeNode.Time - dt;
        }

        _timeline.Draw();
    }
}
