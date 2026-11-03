using Dalamud.Bindings.ImGui;

namespace BossMod;

public abstract class ColumnStateMachine(Timeline timeline, StateMachineTree tree) : Timeline.Column(timeline)
{
    public enum NodeTextDisplay
    {
        [PropertyDisplay("No text")]
        None,

        [PropertyDisplay("ID only")]
        ID,

        [PropertyDisplay("ID and name")]
        IDName,
    }

    public StateMachineTree Tree = tree;
    public NodeTextDisplay TextDisplay = NodeTextDisplay.IDName;
    public bool DrawUnnamedNodes = true;
    public bool DrawTankbusterNodesOnly;
    public bool DrawRaidwideNodesOnly;

    public float PixelsPerBranch => TextDisplay switch
    {
        NodeTextDisplay.ID => 80f,
        NodeTextDisplay.IDName => 250f,
        _ => 20f,
    };

    private readonly float _nodeHOffset = 10f;
    private readonly float _nodeRadius = 5f;

    protected void DrawNode(StateMachineTree.Node node, bool singleColumn, float? progress = null)
    {
        var phaseStart = Tree.Phases[node.PhaseID].StartTime;

        var drawlist = ImGui.GetWindowDrawList();
        var nodeScreenPos = NodeScreenPos(node, node.BranchID, singleColumn, phaseStart);
        var predScreenPos = NodeScreenPos(node.Predecessor, node.BranchID, singleColumn, phaseStart);
        var connection = nodeScreenPos - predScreenPos;

        // draw connection from predecessor
        var connLen = connection.Length();
        var lenOffset = _nodeRadius + 1;
        if (connLen > 2 * lenOffset)
        {
            var connDir = connection / connLen;
            var connScreenBeg = predScreenPos + lenOffset * connDir;
            var connScreenEnd = nodeScreenPos - lenOffset * connDir;
            drawlist.AddLine(connScreenBeg, connScreenEnd, node.InGroup ? Colors.TextColor1 : Colors.TextColor13);

            var connNormal = new Vector2(connDir.Y, -connDir.X);
            if (node.BossIsCasting)
            {
                drawlist.AddLine(connScreenBeg + 3 * connNormal, connScreenEnd + 3 * connNormal, Colors.TextColor4);
            }

            if (node.IsDowntime)
            {
                drawlist.AddLine(connScreenBeg + 6 * connNormal, connScreenEnd + 6 * connNormal, Colors.TextColor14);
            }

            if (node.IsPositioning)
            {
                drawlist.AddLine(connScreenBeg - 3 * connNormal, connScreenEnd - 3 * connNormal, Colors.TextColor3);
            }

            if (progress != null)
            {
                var currentTimeScreenPos = predScreenPos + connection * Math.Clamp(progress.Value / node.State.Duration, 0, 1);
                drawlist.AddCircleFilled(currentTimeScreenPos, 3, Colors.TextColor1);
            }
        }

        // draw node itself
        var showNode = true;
        showNode &= DrawUnnamedNodes || node.State.Name.Length > 0;
        var isTankbuster = (node.State.EndHint & StateMachine.StateHint.Tankbuster) != 0;
        var isRaidwide = (node.State.EndHint & StateMachine.StateHint.Raidwide) != 0;

        showNode &= !DrawTankbusterNodesOnly || isTankbuster;
        showNode &= !DrawRaidwideNodesOnly || isRaidwide;

        if (showNode)
        {
            var nodeColor = isRaidwide ? (isTankbuster ? Colors.TextColor6 : Colors.TextColor14) : (isTankbuster ? Colors.TextColor3 : Colors.TextColor1);

            drawlist.AddCircleFilled(nodeScreenPos, _nodeRadius, nodeColor);

            var nodeText = TextDisplay switch
            {
                NodeTextDisplay.ID => $"{node.State.ID:X8}",
                NodeTextDisplay.IDName => $"{node.State.ID:X8} '{Loc.Tr(node.State.Name)}'",
                _ => ""
            };
            if (nodeText.Length > 0)
            {
                drawlist.AddText(nodeScreenPos + new Vector2(7, -10), Colors.TextColor1, nodeText);
            }

            if (progress != null)
            {
                drawlist.AddCircle(nodeScreenPos, _nodeRadius + 3, nodeColor);
            }

            if (ImGui.IsMouseHoveringRect(nodeScreenPos - new Vector2(_nodeRadius), nodeScreenPos + new Vector2(_nodeRadius)))
            {
                Timeline.AddTooltip(NodeTooltip(node));
            }
        }
    }

    private Vector2 NodeScreenPos(StateMachineTree.Node? node, int fallbackBranch, bool singleColumn, float phaseStart)
    {
        var branch = singleColumn ? 0 : (node?.BranchID ?? fallbackBranch);
        var time = node?.Time ?? 0;
        return Timeline.ColumnCoordsToScreenCoords(_nodeHOffset + branch * PixelsPerBranch, phaseStart + time);
    }

    private List<string> NodeTooltip(StateMachineTree.Node n)
    {
        List<string> res =
        [
            Loc.Format("State: {0:X8} '{1}'", n.State.ID, Loc.Tr(n.State.Name)),
            Loc.Format("Comment: {0}", Loc.Tr(n.State.Comment)),
            Loc.Format("Phase: {0} '{1}'", n.PhaseID, Loc.Tr(Tree.Phases[n.PhaseID].Name)),
            Loc.Format("Time: {0:f1} ({1:f1} from prev)", n.Time, n.State.Duration),
            Loc.Format("Flags: {0}", n.State.EndHint),
        ];
        return res;
    }
}
