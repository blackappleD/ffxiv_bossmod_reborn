using BossMod.Autorotation;
using BossMod.Autorotation.xan;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace BossMod;

sealed class MainDebugWindow(WorldState ws, RotationModuleManager autorot, ZoneModuleManager zmm, ActionManagerEx amex, MovementOverride move, AIHintsBuilder hintBuilder, IDalamudPluginInterface dalamud, RotationSolverRebornModule rsr) : UIWindow(Loc.Id("Boss mod debug UI"), false, new(300, 200))
{
    private readonly DebugObstacles _debugObstacles = new(hintBuilder.Obstacles, dalamud);
    private readonly DebugObjects _debugObjects = new();
    private readonly DebugParty _debugParty = new();
    private readonly DebugMapEffect _debugMapEffect = new(ws);
    private readonly DebugGraphics _debugGraphics = new();
    private readonly DebugAction _debugAction = new(ws, amex);
    private readonly DebugHate _debugHate = new(ws);
    private readonly DebugInput _debugInput = new(autorot, move);
    private readonly DebugAutorotation _debugAutorot = new(autorot);
    private readonly DebugAddon _debugAddon = new();
    private readonly DebugTiming _debugTiming = new();
    private readonly DebugTeleport _debugTeleport = new();
    private readonly DebugCollision _debugCollision = new();
    private readonly DebugQuests _debugQuests = new();
    private readonly DebugCastMovement _debugCastMovement = new(ws, autorot.Hints, amex, move, rsr);

    protected override void Dispose(bool disposing)
    {
        _debugAction.Dispose();
        _debugInput.Dispose();
        _debugAddon.Dispose();
        _debugMapEffect.Dispose();
        _debugTeleport.Dispose();
        _debugCollision.Dispose();
        base.Dispose(disposing);
    }

    public override unsafe void Draw()
    {
        var playerCID = UIState.Instance()->PlayerState.ContentId;
        var player = Service.ObjectTable.LocalPlayer;
        ImGui.TextUnformatted(Loc.Format("Current zone: {0}, player=0x{1:X}, playerCID={2:X}, pos = {3}", ws.CurrentZone, (ulong)Utils.GameObjectInternal(player), playerCID, Utils.Vec3String(player?.Position ?? new Vector3())));
        // ImGui.TextUnformatted($"ID scramble: {Network.IDScramble.Delta} = {*Network.IDScramble.OffsetAdjusted} - {*Network.IDScramble.OffsetBaseFixed} - {*Network.IDScramble.OffsetBaseChanging}");
        ImGui.TextUnformatted(Loc.Format("Player mode: {0}", player is null ? Loc.Tr("No player found") : Utils.CharacterInternal(player)->Mode.ToString()));

        var eventFwk = FFXIVClientStructs.FFXIV.Client.Game.Event.EventFramework.Instance();
        var instanceDirector = eventFwk != null ? eventFwk->GetInstanceContentDirector() : null;
        ImGui.TextUnformatted(Loc.Format("Content time left: {0}", instanceDirector != null ? $"{instanceDirector->ContentDirector.ContentTimeLeft:f1}" : "n/a"));
        if (instanceDirector != null)
        {
            ImGui.TextUnformatted(Loc.Format("Director address: 0x{0:X}", (nint)instanceDirector));
        }

        if (ImGui.Button(Loc.Id("Perform full dump")))
        {
            DebugObjects.DumpObjectTable();
            DebugGraphics.DumpScene();
        }

        if (ImGui.CollapsingHeader(Loc.Id("Obstacles")))
        {
            _debugObstacles.Draw();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Full object list")))
        {
            _debugObjects.DrawObjectTable();
        }
        if (ImGui.CollapsingHeader(Loc.Id("UI object list")))
        {
            _debugObjects.DrawUIObjects();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Statuses")))
        {
            DrawStatuses();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Casting enemies")))
        {
            DrawCastingEnemiesList();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Party")))
        {
            _debugParty.Draw(false);
        }
        if (ImGui.CollapsingHeader(Loc.Id("Inventory")))
        {
            DrawInventory();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Party (duty recorder)")))
        {
            _debugParty.Draw(true);
        }
        if (ImGui.CollapsingHeader(Loc.Id("Action effects")))
        {
            DrawEffects();
        }
        /*
        if (ImGui.CollapsingHeader("Gauge"))
        {
            var gauge = ws.Client.GaugePayload;
            ImGui.Text($"Raw: {gauge.High:X8} {gauge.Low:X8}");
            var as_ = ws.Client.GetGauge<BeastmasterGauge>();
            Dalamud.Utility.Util.ShowObject(as_);
        }
        */
        if (ImGui.CollapsingHeader(Loc.Id("Map effects")))
        {
            _debugMapEffect.Draw();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Autorotation")))
        {
            _debugAutorot.Draw();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Party health")))
        {
            DrawPartyHealth();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Solo duty module")))
        {
            if (zmm.ActiveModule is QuestBattle.QuestBattle qb)
            {
                qb.DrawDebugInfo();
            }
        }
        if (ImGui.CollapsingHeader(Loc.Id("Graphics scene")))
        {
            _debugGraphics.DrawSceneTree();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Graphics watch")))
        {
            _debugGraphics.DrawWatchedMods();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Matrices")))
        {
            _debugGraphics.DrawMatrices();
        }
        if (Camera.Instance != null && ImGui.CollapsingHeader(Loc.Id("In-game overlay")))
        {
            _debugGraphics.DrawOverlay();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Action manager ex")))
        {
            _debugAction.DrawActionManagerExtensions();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Actions")))
        {
            _debugAction.DrawActionData();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Duty actions")))
        {
            _debugAction.DrawDutyActions();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Auto attacks")))
        {
            _debugAction.DrawAutoAttack();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Hate")))
        {
            _debugHate.Draw();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Targets")))
        {
            DrawTargets();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Input")))
        {
            _debugInput.Draw();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Markers")))
        {
            DrawMarkers();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Player attributes")))
        {
            DrawPlayerAttributes();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Countdown")))
        {
            DrawCountdown();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Addon / agent")))
        {
            _debugAddon.Draw();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Timing")))
        {
            _debugTiming.Draw();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Window system")))
        {
            DrawWindowSystem();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Teleport")))
        {
            _debugTeleport.Draw();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Collision")))
        {
            _debugCollision.Draw();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Limit break")))
        {
            DrawLimitBreak();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Quests")))
        {
            _debugQuests.Draw();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Rotation Solver Reborn")))
        {
            DrawRSR();
        }
        if (ImGui.CollapsingHeader(Loc.Id("Cast / movement (RSR)")))
        {
            _debugCastMovement.Draw();
        }
    }

    private void DrawRSR()
    {
        ImGui.TextUnformatted(Loc.Format("RSR installed: {0}", rsr.IsInstalled));
        if (!rsr.IsInstalled)
        {
            return;
        }

        if (ImGui.Button(Loc.Id("Pause (NoCasting)")))
        {
            rsr.PauseRSR();
        }

        ImGui.SameLine();
        if (ImGui.Button(Loc.Id("Unpause (EndSpecial)")))
        {
            rsr.UnPauseRSR();
        }

        ImGui.Separator();
        ImGui.TextUnformatted("TriggerSpecialStateWithDuration:");
        var cmds = GeneratedEnumMetadata.Values<RotationSolverRebornModule.SpecialCommandType>();
        var len = cmds.Length;
        for (var i = 0; i < len; ++i)
        {
            var cmd = cmds[i];
            if (cmd == RotationSolverRebornModule.SpecialCommandType.EndSpecial)
            {
                continue;
            }

            if (ImGui.Button($"{cmd}##rsr"))
            {
                rsr.TriggerSpecialStateWithDuration(cmd, 7f);
            }

            ImGui.SameLine();
        }
        ImGui.NewLine();
    }

    private unsafe void DrawStatuses()
    {
        var player = (Character*)GameObjectManager.Instance()->Objects.IndexSorted[0].Value;
        if (player == null)
        {
            return;
        }

        ImGui.TextUnformatted(Loc.Format("Forced movement direction: {0}", MovementOverride.ForcedMovementDirection->Radians()));
        ImGui.SameLine();
        if (ImGui.Button(Loc.Id("Add misdirection")))
        {
            player->GetStatusManager()->SetStatus(20, 3909, 20.0f, 100, (GameObjectId)0xE0000000, true);
        }

        ImGui.SameLine();
        if (ImGui.Button(Loc.Id("Add thin ice")))
        {
            player->GetStatusManager()->SetStatus(20, 911, 20.0f, 50, (GameObjectId)0xE0000000, true); // param = distance * 10
        }

        ImGui.SameLine();
        if (ImGui.Button(Loc.Id("Add spinning")))
        {
            player->GetStatusManager()->SetStatus(20, 2973, 20.0f, 7, (GameObjectId)0xE0000000, true);
        }

        if (ImGui.Button(Loc.Id("Clear temp status")))
        {
            player->GetStatusManager()->RemoveStatus(20);
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(Loc.Format("Forced movement direction: {0}", ws.Client.ForcedMovementDirection));

        ImGui.TextUnformatted(Loc.Format("Player move speed: {0:f2}", ws.Client.MoveSpeed));

        foreach (var elem in ws.Actors)
        {
            var obj = (elem.InstanceID >> 32) == 0 ? Service.ObjectTable.SearchById((uint)elem.InstanceID) : null;
            if (ImGui.TreeNode(Utils.ObjectString(obj!)))
            {
                if (obj is IBattleChara chara)
                {
                    foreach (var status in chara.StatusList)
                    {
                        var src = status.SourceObject != null ? Utils.ObjectString(status.SourceObject) : "none";
                        ImGui.TextUnformatted($"{status.StatusId} '{status.GameData.Value.Name}': param={status.Param}, stacks={status.Param}, time={status.RemainingTime:f2}, source={src} ({status.SourceId:X8})");
                    }
                }
                ImGui.TreePop();
            }
        }
    }

    private void DrawCastingEnemiesList()
    {
        ImGui.BeginTable("enemies", 7, ImGuiTableFlags.Resizable);
        ImGui.TableSetupColumn(Loc.Tr("Caster"));
        ImGui.TableSetupColumn(Loc.Tr("Target"));
        ImGui.TableSetupColumn(Loc.Tr("Action"));
        ImGui.TableSetupColumn(Loc.Tr("Time"));
        ImGui.TableSetupColumn(Loc.Tr("Location"));
        ImGui.TableSetupColumn(Loc.Tr("Position"));
        ImGui.TableSetupColumn(Loc.Tr("Rotation"));
        ImGui.TableHeadersRow();
        foreach (var elem in ws.Actors)
        {
            if (elem.CastInfo == null || elem.IsAlly)
            {
                continue;
            }

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(Utils.ObjectString(elem.InstanceID));
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(Utils.ObjectString(elem.CastInfo.TargetID));
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(elem.CastInfo.Action.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(Utils.CastTimeString(elem.CastInfo, ws.CurrentTime));
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(Utils.Vec3String(elem.CastInfo.Location));
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(elem.Position.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(elem.CastInfo.Rotation.ToString());
        }
        ImGui.EndTable();
    }

    private void DrawInventory()
    {
        var player = Service.ObjectTable.LocalPlayer;
        if (player == null)
        {
            return;
        }

        ImGui.BeginTable("items", 3, ImGuiTableFlags.Resizable | ImGuiTableFlags.RowBg);
        ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 30);
        ImGui.TableSetupColumn(Loc.Tr("Quant"), ImGuiTableColumnFlags.WidthFixed, 30);
        ImGui.TableSetupColumn(Loc.Tr("Name"));
        ImGui.TableHeadersRow();
        foreach (var (k, i) in ws.Client.Inventory)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(k.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(i.ToString());
            ImGui.TableNextColumn();
            var namebase = Service.LuminaRow<Lumina.Excel.Sheets.Item>(k % 500000)?.Name.ToString() ?? "<unknown>";
            var namefull = k > 500000 ? $"{namebase} (HQ)" : namebase;
            ImGui.TextUnformatted(namefull);
        }
        ImGui.EndTable();
    }

    private unsafe void DrawEffects()
    {
        var player = Service.ObjectTable.LocalPlayer;
        if (player == null)
        {
            return;
        }

        var aeh = ((BattleChara*)player.Address)->GetActionEffectHandler();
        if (aeh == null)
        {
            return;
        }

        ImGui.TextUnformatted(Loc.Format("Effecthandler address: {0:X}", (nint)aeh));

        ImGui.BeginTable("effects", 6, ImGuiTableFlags.Resizable | ImGuiTableFlags.RowBg);
        ImGui.TableSetupColumn(Loc.Tr("Seq"), ImGuiTableColumnFlags.WidthFixed, 30);
        ImGui.TableSetupColumn(Loc.Tr("Index"), ImGuiTableColumnFlags.WidthFixed, 30);
        ImGui.TableSetupColumn(Loc.Tr("Action"));
        ImGui.TableSetupColumn(Loc.Tr("Source"));
        ImGui.TableSetupColumn(Loc.Tr("Target"));
        ImGui.TableSetupColumn(Loc.Tr("Effects"));
        ImGui.TableHeadersRow();
        foreach (var entry in aeh->IncomingEffects)
        {
            if (entry.ActionId == 0)
            {
                continue;
            }

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(entry.GlobalSequence.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(entry.TargetIndex.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{new ActionID((ActionType)entry.ActionType, entry.ActionId)} ({entry.SpellId})");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{entry.Source.Id:X} confirmed={entry.SourceConfirmed}");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{entry.Target.Id:X} confirmed={entry.TargetConfirmed}");
            ImGui.TableNextColumn();
            foreach (var eff in entry.Effects.Effects)
            {
                if (eff.Type > 0)
                {
                    ImGui.TextUnformatted($"{(ActionEffectType)eff.Type} {eff.Param0:X2} {eff.Param1:X2} {eff.Param2:X2} {eff.Param3:X2} {eff.Param4:X2} {eff.Value}");
                }
            }
        }
        ImGui.EndTable();
    }

    private readonly TrackPartyHealth _partyHealth = new(ws);

    private void DrawPartyHealth()
    {
        _partyHealth.Update(autorot.Hints);

        var overall = _partyHealth.PartyHealth;

        ImGui.TextUnformatted(Loc.Format("Avg: {0:f1} (current) / {1:f1} (predicted)", overall.AvgCurrent * 100, overall.AvgPredicted * 100));
        ImGui.TextUnformatted(Loc.Format("StD: {0:f3} (current) / {1:f3} (predicted)", overall.StdDevCurrent, overall.StdDevCurrent));

        ImGui.BeginTable("partyhealth", 4, ImGuiTableFlags.Resizable);
        ImGui.TableSetupColumn(Loc.Tr("Name"));
        ImGui.TableSetupColumn(Loc.Tr("HP"));
        ImGui.TableSetupColumn(Loc.Tr("Type"));
        ImGui.TableHeadersRow();
        foreach (var (_, actor) in _partyHealth.TrackedMembers)
        {
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(actor.Name);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{actor.HPMP.CurHP} ({actor.PendingHPDifference:+#;-#;+0}) / {actor.HPMP.MaxHP} ({actor.HPRatio:#0.#%} / {actor.PendingHPRatio:#0.#%})");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{actor.Type}");
            ImGui.TableNextRow();
        }
        ImGui.EndTable();
    }

    private unsafe void DrawTargets()
    {
        var cursorPos = amex.GetWorldPosUnderCursor();
        ImGui.TextUnformatted(Loc.Format("World pos under cursor: {0}", cursorPos == null ? "n/a" : Utils.Vec3String(cursorPos.Value)));

        var player = Service.ObjectTable.LocalPlayer;
        var selfPos = player?.Position ?? new();
        var targPos = Service.ObjectTable.LocalPlayer?.TargetObject?.Position ?? new();
        var angle = player?.Rotation.Radians() ?? default; //Angle.FromDirection(new((targPos - selfPos).XZ()));
        var ts = FFXIVClientStructs.FFXIV.Client.Game.Control.TargetSystem.Instance();
        DrawTarget("Target", ts->Target, selfPos, angle);
        DrawTarget("Soft target", ts->SoftTarget, selfPos, angle);
        DrawTarget("GPose target", ts->GPoseTarget, selfPos, angle);
        DrawTarget("Mouseover", ts->MouseOverTarget, selfPos, angle);
        DrawTarget("Focus", ts->FocusTarget, selfPos, angle);
        var mouseover = FFXIVClientStructs.FFXIV.Client.UI.Misc.PronounModule.Instance()->UiMouseOverTarget;
        ImGui.TextUnformatted(Loc.Format("UI Mouseover: {0}", Utils.ObjectString(mouseover != null ? mouseover->EntityId : 0)));

        if (ImGui.Button(Loc.Id("Target closest enemy")))
        {
            IGameObject? closest = null;
            var closestDist = float.MaxValue;
            foreach (var o in Service.ObjectTable)
            {
                if (o.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.BattleNpc || o.SubKind != 5)
                {
                    continue;
                }

                var d = (o.Position - selfPos).LengthSquared();
                if (d < closestDist)
                {
                    closestDist = d;
                    closest = o;
                }
            }
            if (closest != null)
            {
                Service.TargetManager.Target = closest;
            }
        }
    }

    private unsafe void DrawTarget(string kind, GameObject* obj, Vector3 selfPos, Angle refAngle)
    {
        if (obj == null)
        {
            return;
        }

        var selfToObj = (Vector3)obj->Position - selfPos;
        var dist = selfToObj.Length();
        var angle = Angle.FromDirection(new(selfToObj.XZ())) - refAngle;
        var visHalf = Angle.Asin(obj->HitboxRadius / dist);
        ImGui.TextUnformatted($"{Loc.Tr(kind)}: #{obj->ObjectIndex} {Utils.ObjectString(obj->EntityId)} {obj->BaseId}:{obj->GetNameId()}, hb={obj->HitboxRadius} ({visHalf}), dist={dist}, angle={angle} ({Math.Max(0, angle.Abs().Rad - visHalf.Rad).Radians()})");
    }

    private static readonly string[] FieldMarkers = ["A", "B", "C", "D", "1", "2", "3", "4"];

    private unsafe void DrawMarkers()
    {
        var markers = MarkingController.Instance();
        using (ImRaii.Table("Field", 2))
        {
            for (var i = 0; i < markers->FieldMarkers.Length; ++i)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(FieldMarkers[i]);
                ImGui.TableNextColumn();
                ImGui.Text(markers->FieldMarkers[i].Active ? markers->FieldMarkers[i].Position.ToString() : "-");
            }
        }

        using (ImRaii.Table("Target", 2))
        {
            for (var i = 0; i < markers->Markers.Length; ++i)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text($"{i} ???");
                ImGui.TableNextColumn();
                ImGui.Text(markers->Markers[i].ObjectId.ToString("X8"));
            }
        }
    }

    private unsafe void DrawPlayerAttributes()
    {
        if (ImGui.Button(Loc.Id("Clear trial")))
        {
            Utils.WriteField((void*)Service.Condition.Address, (int)Dalamud.Game.ClientState.Conditions.ConditionFlag.OnFreeTrial, false);
        }

        var uiState = UIState.Instance();
        var level = (uint)uiState->PlayerState.CurrentLevel;
        var paramGrow = Service.LuminaRow<Lumina.Excel.Sheets.ParamGrow>(level);
        if (paramGrow != null)
        {
            ImGui.TextUnformatted(Loc.Format("Level: {0}, baseSpeed={1}, levelMod={2}", level, paramGrow.Value.BaseSpeed, paramGrow.Value.LevelModifier));
            var sksValue = uiState->PlayerState.Attributes[45];
            var spsValue = uiState->PlayerState.Attributes[46];
            var sksMod = 130 * (paramGrow.Value.BaseSpeed - sksValue) / paramGrow.Value.LevelModifier + 1000;
            var spsMod = 130 * (paramGrow.Value.BaseSpeed - spsValue) / paramGrow.Value.LevelModifier + 1000;
            var hasteValue = uiState->PlayerState.Attributes[47];
            ImGui.TextUnformatted($"SKS: value={sksValue}, mod={sksMod}, gcd={2500 * sksMod / 1000}");
            ImGui.TextUnformatted($"SPS: value={spsValue}, mod={spsMod}, gcd={2500 * spsMod / 1000}");
            ImGui.TextUnformatted($"Haste: value={hasteValue}, gcd-sks={2500 * sksMod / 1000 * hasteValue / 100}, gcd-sps={2500 * spsMod / 1000 * hasteValue / 100}");
        }

        ImGui.BeginTable("attrs", 2);
        ImGui.TableSetupColumn(Loc.Tr("Index"));
        ImGui.TableSetupColumn(Loc.Tr("Value"));
        ImGui.TableHeadersRow();
        for (var i = 0; i < 74; ++i)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(i.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(uiState->PlayerState.Attributes[i].ToString());
        }
        ImGui.EndTable();
    }

    private unsafe void DrawCountdown()
    {
        var agent = AgentCountDownSettingDialog.Instance();
        ImGui.TextUnformatted(Loc.Format("Active: {0} (showing cd={1})", agent->Active, agent->ShowingCountdown));
        ImGui.TextUnformatted(Loc.Format("Initiator: {0}", Utils.ObjectString(agent->InitiatorId)));
        ImGui.TextUnformatted(Loc.Format("Time left: {0:f3}", agent->TimeRemaining));
    }

    private void DrawWindowSystem()
    {
        ImGui.TextUnformatted(Loc.Format("Any focus: {0}", Service.WindowSystem!.HasAnyFocus));
        foreach (var w in Service.WindowSystem.Windows)
        {
            ImGui.TextUnformatted($"{w.WindowName}: focus={w.IsFocused}");
        }
    }

    private unsafe void DrawLimitBreak()
    {
        var lb = LimitBreakController.Instance();
        ImGui.TextUnformatted(Loc.Format("Value: {0}/{1} ({2} bars)", lb->CurrentUnits, lb->BarUnits, lb->BarCount));
        ImGui.TextUnformatted($"PVP: {lb->IsPvP}");
    }
}
