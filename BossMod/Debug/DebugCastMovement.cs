using BossMod.AI;
using BossMod.Autorotation.MiscAI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace BossMod;

// diagnostics for interactions between BMR movement and casts started by us or an external rotation (RSR):
// which movement system is active, what each side believes the cast state is, what we report over ipc and how recent casts ended
// also hosts the fake aoe test that forces cast-interrupting dodges to check that no cast/interrupt loop forms
sealed class DebugCastMovement(WorldState ws, AIHints hints, ActionManagerEx amex, MovementOverride move, RotationSolverRebornModule rsr)
{
    public void Draw()
    {
        DrawState();
        ImGui.Separator();
        DrawFakeAoe();
        ImGui.Separator();
        DrawHistory();
    }

    private void DrawState()
    {
        var aiActive = AIManager.Instance?.Beh != null;
        var autorotMovement = NormalMovement.Instance != null;
        var system = aiActive ? "Automovement (AI)" : autorotMovement ? "Autorotation (Automatic movement module)" : "none";
        ImGui.TextUnformatted(Loc.Format("Movement system: {0}{1}", Loc.Tr(system), aiActive && autorotMovement ? Loc.Tr(" - automatic movement module is loaded but idle while automovement is on") : ""));

        var rsrInstalled = rsr.IsInstalled;
        ImGui.TextUnformatted(Loc.Format("RSR: installed={0}, autorotation active={1}", rsrInstalled, rsrInstalled && rsr.IsAutorotationActive));
        ImGui.TextUnformatted(Loc.Format("Prevent movement while casting: config={0}, effective={1}, input blocked now={2}", ActionManagerEx.Config.PreventMovingWhileCasting, amex.PreventMovingWhileCasting, move.MovementBlocked));

        // live client state vs worldstate: the worldstate only shows our own casts once the server has confirmed them
        var castRemaining = amex.CastTimeRemaining;
        ImGui.TextUnformatted(Loc.Format("Cast (live): {0}, move might interrupt={1}", castRemaining > 0f ? Loc.Format("{0}, {1:f2}s left", amex.CastAction, castRemaining) : Loc.Tr("none"), amex.MoveMightInterruptCast));
        var player = ws.Party.Player();
        var cast = player?.CastInfo;
        ImGui.TextUnformatted(Loc.Format("Cast (worldstate): {0}", cast != null ? Loc.Format("{0}, {1:f2}s left, effect happened={2}", cast.Action, cast.RemainingTime, cast.EventHappened) : Loc.Tr("none")));

        var forced = hints.ForcedMovement;
        ImGui.TextUnformatted(Loc.Format("Hints: forced movement={0}, hold casts={1}, max cast time={2}, force cancel mechanic/other={3}/{4}", forced != null ? $"{forced.Value.Length():f2}y" : Loc.Tr("none"), hints.HoldCasts, Seconds(hints.MaxCastTime), hints.ForceCancelCastMechanic, hints.ForceCancelCastOther));
        ImGui.TextUnformatted(Loc.Format("IPC Movement.IsMoving (what RSR sees): {0}", forced != null || hints.HoldCasts));

        if (AIManager.Instance is { } ai)
        {
            var ctrl = ai.Controller;
            var dest = ctrl.NaviTargetPos is { } pos && player != null ? Loc.Format("{0:f2}y away", (pos - player.Position).Length()) : Loc.Tr("none");
            ImGui.TextUnformatted(Loc.Format("Automovement: destination={0}, leeway={1}, override={2}, force cancel mechanic/other={3}/{4}", dest, Seconds(ctrl.NaviLeewaySeconds), ctrl.AllowInterruptingCastByMovement, ctrl.ForceCancelCastMechanicAI, ctrl.ForceCancelCastOtherAI));
        }

        ImGui.TextUnformatted(Loc.Format("Input: user={0}, actual={1}, desired direction={2}", Dir(move.UserMove), Dir(move.ActualMove), move.DesiredDirection is { } d ? Utils.Vec3String(d) : Loc.Tr("none")));
    }

    private void DrawFakeAoe()
    {
        ImGui.TextWrapped(Loc.Tr("Fake AOE: repeatedly drops a circle under the player so the active movement system has to dodge with no leeway. With RSR casting, expect one interrupted cast per drop followed by an instant - never back-to-back interrupts."));

        var enabled = FakeAoeTest.Enabled;
        if (ImGui.Checkbox(Loc.Id("Enable fake AOE under player"), ref enabled))
        {
            FakeAoeTest.Enabled = enabled;
            FakeAoeTest.Reset();
        }
        ImGui.DragFloat(Loc.Tr("Radius##fakeaoe"), ref FakeAoeTest.Radius, 0.1f, 1f, 15f, "%.1f y");
        ImGui.DragFloat(Loc.Tr("Activation delay##fakeaoe"), ref FakeAoeTest.ActivationDelay, 0.05f, 0f, 10f, "%.2f s");
        ImGui.DragFloat(Loc.Tr("Re-drop interval##fakeaoe"), ref FakeAoeTest.Interval, 0.1f, 0.5f, 30f, "%.1f s");

        if (FakeAoeTest.Enabled)
        {
            var now = ws.CurrentTime;
            var toActivation = (FakeAoeTest.Activation - now).TotalSeconds;
            ImGui.TextUnformatted(Loc.Format("Drops: {0}, current at {1}, {2}, next drop in {3:f2}s", FakeAoeTest.Drops, Pos(FakeAoeTest.Center), toActivation > 0d ? Loc.Format("activates in {0:f2}s", toActivation) : Loc.Tr("active"), (FakeAoeTest.NextDrop - now).TotalSeconds));
        }
    }

    private void DrawHistory()
    {
        var tracker = amex.CastTracker;
        var history = tracker.History;
        var count = history.Count;
        int interrupted = 0, streak = 0, longestStreak = 0;
        for (var i = 0; i < count; ++i)
        {
            if (history[i].Outcome == CastTracker.Outcome.Interrupted)
            {
                ++interrupted;
                longestStreak = Math.Max(longestStreak, ++streak);
            }
            else if (history[i].Outcome != CastTracker.Outcome.Casting)
            {
                streak = 0;
            }
        }
        // casts interrupted back to back are the signature of a cast/interrupt loop
        ImGui.TextUnformatted(Loc.Format("Recent casts: {0}, interrupted: {1}, longest back-to-back interrupt streak: {2}{3}", count, interrupted, longestStreak, longestStreak >= 3 ? Loc.Tr(" (might be a loop)") : ""));
        ImGui.SameLine();
        if (ImGui.Button(Loc.Tr("Clear##casthistory")))
        {
            tracker.Clear();
            return; // count above is stale now; the table is drawn again next frame
        }

        using var table = ImRaii.Table("cast_history", 9, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit);
        if (!table)
        {
            return;
        }

        ImGui.TableSetupColumn(Loc.Tr("Action"));
        ImGui.TableSetupColumn(Loc.Tr("Ago"));
        ImGui.TableSetupColumn(Loc.Tr("Duration"));
        ImGui.TableSetupColumn(Loc.Tr("Outcome"));
        ImGui.TableSetupColumn(Loc.Tr("Left"));
        ImGui.TableSetupColumn(Loc.Tr("BMR move frames"));
        ImGui.TableSetupColumn(Loc.Tr("Manual move frames"));
        ImGui.TableSetupColumn(Loc.Tr("Drift"));
        ImGui.TableSetupColumn(Loc.Tr("Likely cause"));
        ImGui.TableHeadersRow();

        var now = DateTime.Now;
        for (var i = count - 1; i >= 0; --i) // newest first
        {
            var e = history[i];
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(e.Action.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{(now - e.Start).TotalSeconds:f1}s");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{e.Duration:f2}s");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(Loc.Tr(e.Outcome.ToString()));
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(e.Outcome == CastTracker.Outcome.Interrupted ? $"{e.RemainingAtEnd:f2}s" : "");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(e.AutoMoveFrames.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(e.UserMoveFrames.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{e.Drift:f2}y");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(Loc.Tr(e.LikelyCause));
        }
    }

    private static string Seconds(float s) => s >= 1e6f ? "inf" : $"{s:f2}s";
    private static string Dir(WDir d) => d == default ? Loc.Tr("none") : $"({d.X:f2}, {d.Z:f2})";
    private static string Pos(WPos p) => $"({p.X:f1}, {p.Z:f1})";
}
