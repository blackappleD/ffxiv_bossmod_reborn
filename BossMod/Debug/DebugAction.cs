using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Dalamud.Game.Gui;

namespace BossMod;

sealed unsafe class DebugAction : IDisposable
{
    private int _customAction;
    private readonly UITree _tree = new();
    private readonly WorldState _ws;
    private readonly ActionManagerEx _amex;

    private bool _autoAttack;
    //private delegate byte SetAutoAttackDelegate(byte* self, byte value, byte sendPacket, byte isInstant);
    //private readonly HookAddress<SetAutoAttackDelegate> _hook;

    public DebugAction(WorldState ws, ActionManagerEx amex)
    {
        _ws = ws;
        _amex = amex;
        //_hook = new(Service.SigScanner.Module.BaseAddress + 0xAD3740, SetAutoAttackDetour);
        Service.Log("---");
    }

    public void Dispose()
    {
        //_hook.Dispose();
    }

    public void DrawActionManagerExtensions()
    {
        var pc = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObjectManager.Instance()->Objects.IndexSorted[0].Value;
        var pcCast = pc != null ? ((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)pc)->GetCastInfo() : null;
        var amr = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
        var aidCastAction = _amex.CastAction;
        var aidCastSpell = _amex.CastSpell;
        var aidCombo = new ActionID(ActionType.Spell, amr->Combo.Action);
        var aidQueued = _amex.QueuedAction;
        var aidGTAction = new ActionID((ActionType)amr->AreaTargetingActionType, amr->AreaTargetingActionId);
        var aidGTSpell = new ActionID(ActionType.Spell, amr->AreaTargetingSpellId);
        ImGui.TextUnformatted(Loc.Format("ActionManager singleton address: 0x{0:X}", (ulong)amr));
        ImGui.TextUnformatted(Loc.Format("Anim lock: {0:f3}", amr->AnimationLock));
        ImGui.TextUnformatted(Loc.Format("Cast: {0} / {1}, progress={2:f3}/{3:f3}, target={4:X}/{5}", aidCastAction, aidCastSpell, amr->CastTimeElapsed, amr->CastTimeTotal, amr->CastTargetId, Utils.Vec3String(amr->CastTargetPosition)));
        if (pcCast != null)
        {
            ImGui.TextUnformatted(Loc.Format("Cast (obj): {0} {1}, progress={2:f3}/{3:f3}/{4:f3}", pcCast->IsCasting, new ActionID((ActionType)pcCast->ActionType, pcCast->ActionId), pcCast->CurrentCastTime, pcCast->BaseCastTime, pcCast->TotalCastTime));
        }

        ImGui.TextUnformatted(Loc.Format("Combo: {0}, {1:f3}", aidCombo, _amex.ComboTimeLeft));
        ImGui.TextUnformatted(Loc.Format("Queue: {0}, {1} @ {2:X} [{3}], combo={4}", (amr->ActionQueued ? Loc.Tr("active") : Loc.Tr("inactive")), aidQueued, (ulong)amr->QueuedTargetId, amr->QueueType, amr->QueuedComboRouteId));
        ImGui.TextUnformatted($"GT: {aidGTAction} / {aidGTSpell}, arg={Utils.ReadField<uint>(amr, 0x94)}, targ={amr->AreaTargetingExecuteAtObject:X}/{amr->AreaTargetingExecuteAtCursor}, a0={Utils.ReadField<byte>(amr, 0xA0):X2}, bc={Utils.ReadField<byte>(amr, 0xBC):X}");
        ImGui.TextUnformatted($"Ballista: {amr->BallistaActive} {amr->BallistaRowId} @ {amr->BallistaOrigin} a={amr->BallistaRefAngle.Radians()} r={amr->BallistaRadius} t={amr->BallistaEntityId:X8}");
        ImGui.TextUnformatted(Loc.Format("Last used action sequence: {0}", amr->LastUsedActionSequence));
        //ImGui.TextUnformatted($"GT config: 298={Framework.Instance()->SystemConfig.GetConfigOption(298)->Value.UInt}");
        if (ImGui.Button(Loc.Id("GT complete")))
        {
            amr->AreaTargetingExecuteAtCursor = true;
        }
        ImGui.SameLine();
        if (ImGui.Button(Loc.Id("GT set target")))
        {
            var target = TargetSystem.Instance()->Target;
            amr->AreaTargetingExecuteAtObject = target != null ? target->GetGameObjectId() : 0xE0000000;
        }

        if (ImGui.Button(Loc.Id("Rotate 30 CCW")))
        {
            _amex.FaceDirection((_ws.Party.Player()?.Rotation ?? 0.Degrees()) + 30.Degrees());
        }
    }

    public void DrawActionData()
    {
        var mgr = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
        ImGui.InputInt(Loc.Id("Action to show details for"), ref _customAction);
        if (_customAction != 0)
        {
            var data = Service.LuminaRow<Lumina.Excel.Sheets.Action>((uint)_customAction);
            if (data != null)
            {
                ImGui.TextUnformatted(Loc.Format("Name: {0}", data.Value.Name));
                ImGui.TextUnformatted(Loc.Format("Cast time: {0:f1} + {1:f1}", data.Value.Cast100ms * 0.1f, data.Value.ExtraCastTime100ms * 0.1f));
                ImGui.TextUnformatted(Loc.Format("Range: {0}", data.Value.Range));
                ImGui.TextUnformatted(Loc.Format("Effect range: {0}", data.Value.EffectRange));
                ImGui.TextUnformatted(Loc.Format("Cooldown group: {0}", data.Value.CooldownGroup));
                ImGui.TextUnformatted(Loc.Format("Max charges: {0}", data.Value.MaxCharges));
                ImGui.TextUnformatted(Loc.Format("Category: {0} ({1})", data.Value.ActionCategory.RowId, data.Value.ActionCategory.ValueNullable?.Name));

                if (data.Value.CooldownGroup > 0 && data.Value.CooldownGroup <= mgr->Cooldowns.Length)
                {
                    ref var cd = ref mgr->Cooldowns[data.Value.CooldownGroup - 1];
                    ImGui.TextUnformatted(Loc.Format("Cooldown: active={0}, {1:f3}/{2:f3}", cd.IsActive, cd.Elapsed, cd.Total));
                }
            }
        }

        var hover = Service.GameGui.HoveredAction;
        if (hover.ActionId != 0)
        {
            var mnemonic = Service.PlayerState.ClassJob.ValueNullable?.Abbreviation.ToString();
            var rotationType = mnemonic != null ? GeneratedFactories.FindType($"BossMod.{mnemonic}Rotation+AID") : null;
            ImGui.TextUnformatted(Loc.Format("Hover action: {0} {1} (base={2}) ({3}: {4})", hover.DetailKind, hover.ActionId, hover.BaseActionId, mnemonic, rotationType?.GeneratedEnumName(hover.ActionId)));

            var name = "";
            var type = FFXIVClientStructs.FFXIV.Client.Game.ActionType.None;
            uint unlockLink = 0;
            if (hover.DetailKind == DetailKind.Action) // action
            {
                var data = Service.LuminaRow<Lumina.Excel.Sheets.Action>(hover.ActionId);
                name = data?.Name.ToString() ?? "";
                type = FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action;
                unlockLink = data?.UnlockLink.RowId ?? 0;
            }
            else if (hover.DetailKind == DetailKind.GeneralAction)
            {
                var data = Service.LuminaRow<Lumina.Excel.Sheets.GeneralAction>(hover.ActionId);
                name = data?.Name.ToString() ?? "";
                type = FFXIVClientStructs.FFXIV.Client.Game.ActionType.GeneralAction;
                unlockLink = data?.UnlockLink ?? 0;
            }
            else if (hover.DetailKind == DetailKind.Trait)
            {
                var data = Service.LuminaRow<Lumina.Excel.Sheets.Trait>(hover.ActionId);
                name = data?.Name.ToString() ?? "";
                unlockLink = data?.Quest.RowId ?? 0;
            }

            ImGui.TextUnformatted(Loc.Format("Name: {0}", name));
            ImGui.TextUnformatted(Loc.Format("Unlock: {0} ({1}) = {2}", unlockLink, Service.LuminaRow<Lumina.Excel.Sheets.Quest>(unlockLink)?.Name, FFXIVClientStructs.FFXIV.Client.Game.QuestManager.IsQuestComplete(unlockLink)));
            if (hover.DetailKind == DetailKind.Action)
            {
                ImGui.TextUnformatted(Loc.Format("Range: {0}", FFXIVClientStructs.FFXIV.Client.Game.ActionManager.GetActionRange(hover.ActionId)));
                ImGui.TextUnformatted(Loc.Format("Stacks: {0}", FFXIVClientStructs.FFXIV.Client.Game.ActionManager.GetMaxCharges(hover.ActionId, 0)));
                ImGui.TextUnformatted(Loc.Format("Adjusted ID: {0}", mgr->GetAdjustedActionId(hover.ActionId)));
                ImGui.TextUnformatted(Loc.Format("Range: {0}", FFXIVClientStructs.FFXIV.Client.Game.ActionManager.GetActionRange(hover.ActionId)));
                ImGui.TextUnformatted(Loc.Format("Stacks: {0}", FFXIVClientStructs.FFXIV.Client.Game.ActionManager.GetMaxCharges(hover.ActionId, 0)));
                ImGui.TextUnformatted(Loc.Format("Adjusted ID: {0}", mgr->GetAdjustedActionId(hover.ActionId)));
            }

            if (type != FFXIVClientStructs.FFXIV.Client.Game.ActionType.None)
            {
                //ImGui.TextUnformatted($"Cost: {FFXIVClientStructs.FFXIV.Client.Game.ActionManager.GetActionCost(type, hover.ActionID, 0, 0, 0, 0)}");
                var action = new ActionID((ActionType)type, hover.ActionId);
                DrawStatus("Status RC", action, true, true);
                DrawStatus("Status R-", action, true, false);
                DrawStatus("Status -C", action, false, true);
                DrawStatus("Status --", action, false, false);
                ImGui.TextUnformatted(Loc.Format("Adjusted recast: {0:f2}", FFXIVClientStructs.FFXIV.Client.Game.ActionManager.GetAdjustedRecastTime(type, hover.ActionId)));
                ImGui.TextUnformatted(Loc.Format("Adjusted cast: {0:f2}", FFXIVClientStructs.FFXIV.Client.Game.ActionManager.GetAdjustedCastTime(type, hover.ActionId)));
                ImGui.TextUnformatted(Loc.Format("Recast: {0:f2}", mgr->GetRecastTime(type, hover.ActionId)));
                ImGui.TextUnformatted(Loc.Format("Recast elapsed: {0:f2}", mgr->GetRecastTimeElapsed(type, hover.ActionId)));
                ImGui.TextUnformatted(Loc.Format("Recast active: {0}", mgr->IsRecastTimerActive(type, hover.ActionId)));
                var groupID = mgr->GetRecastGroup((int)type, hover.ActionId);
                ImGui.TextUnformatted(Loc.Format("Recast group: {0}", groupID));
                var group = mgr->GetRecastGroupDetail(groupID);
                if (group != null)
                {
                    ImGui.TextUnformatted(Loc.Format("Recast group details: active={0}, action={1}, elapsed={2:f3}, total={3:f3}, cooldown={4:f3}", group->IsActive, group->ActionId, group->Elapsed, group->Total, group->Total - group->Elapsed));
                }
            }
        }
        else if (Service.GameGui.HoveredItem != 0)
        {
            var itemID = (uint)Service.GameGui.HoveredItem % 1000000;
            var isHQ = Service.GameGui.HoveredItem / 1000000 > 0;
            ImGui.TextUnformatted(Loc.Format("Hover item: {0}", Service.GameGui.HoveredItem));
            ImGui.TextUnformatted(Loc.Format("Name: {0}{1}", Service.LuminaRow<Lumina.Excel.Sheets.Item>(itemID)?.Name, (isHQ ? " (HQ)" : "")));
            ImGui.TextUnformatted(Loc.Format("Count: {0}", FFXIVClientStructs.FFXIV.Client.Game.InventoryManager.Instance()->GetInventoryItemCount(itemID, isHQ, false, false)));
            ImGui.TextUnformatted(Loc.Format("Status: {0}", mgr->GetActionStatus(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Item, itemID)));
            ImGui.TextUnformatted(Loc.Format("Adjusted recast: {0:f2}", FFXIVClientStructs.FFXIV.Client.Game.ActionManager.GetAdjustedRecastTime(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Item, itemID)));
            ImGui.TextUnformatted(Loc.Format("Adjusted cast: {0:f2}", FFXIVClientStructs.FFXIV.Client.Game.ActionManager.GetAdjustedCastTime(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Item, itemID)));
            ImGui.TextUnformatted(Loc.Format("Recast: {0:f2}", mgr->GetRecastTime(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Item, itemID)));
            ImGui.TextUnformatted(Loc.Format("Recast elapsed: {0:f2}", mgr->GetRecastTimeElapsed(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Item, itemID)));
            ImGui.TextUnformatted(Loc.Format("Recast active: {0}", mgr->IsRecastTimerActive(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Item, itemID)));
            var groupID = mgr->GetRecastGroup(2, itemID);
            ImGui.TextUnformatted(Loc.Format("Recast group: {0}", groupID));
            var group = mgr->GetRecastGroupDetail(groupID);
            if (group != null)
            {
                ImGui.TextUnformatted(Loc.Format("Recast group details: active={0}, action={1}, elapsed={2}, total={3}", group->IsActive, group->ActionId, group->Elapsed, group->Total));
            }
        }
        else
        {
            ImGui.TextUnformatted(Loc.Tr("Hover: none"));
        }

        foreach (var nr in _tree.Node(Loc.Id("Player actions")))
        {
            DrawFilteredActions("Hostile + friendly", a => a.IsPlayerAction && a.CanTargetHostile && (a.CanTargetSelf || a.CanTargetParty || a.CanTargetAlliance || a.CanTargetAlly || a.CanTargetOwnPet || a.CanTargetPartyPet));
        }
    }

    public void DrawDutyActions()
    {
        var cd = EventFramework.Instance()->DirectorModule.ActiveContentDirector;
        if (cd == null)
        {
            ImGui.TextUnformatted(Loc.Tr("Content director is unavailable"));
            return;
        }
        ImGui.TextUnformatted(Loc.Format("Excel rows: pending={0}, current={1}", cd->DutyActionManager.PendingContentExActionRowId, cd->DutyActionManager.CurrentContentExActionRowId));
        ImGui.TextUnformatted(Loc.Format("Num valid slots: {0}, actions present={1}", cd->DutyActionManager.NumValidSlots, cd->DutyActionManager.ActionActive[0] && cd->DutyActionManager.NumValidSlots > 0));
        for (var i = 0; i < cd->DutyActionManager.NumValidSlots; ++i)
        {
            var chargeText = i < 2 ? $"{cd->DutyActionManager.CurCharges[i]}/{cd->DutyActionManager.MaxCharges[i]}" : "?/?";
            ImGui.TextUnformatted($"[{i}]: action={new ActionID(ActionType.Spell, cd->DutyActionManager.ActionId[i])}, active={cd->DutyActionManager.ActionActive[i]}, charges={chargeText}");
        }
    }

    public void DrawAutoAttack()
    {
        var aa = UIState.Instance()->WeaponState.AutoAttackState.IsAutoAttacking;
        if (_autoAttack != aa)
        {
            Service.Log($"AA state changed: {_autoAttack} -> {aa}");
        }

        _autoAttack = aa;
        ImGui.TextUnformatted(Loc.Format("Auto-attack: {0}", aa));
    }

    private void DrawStatus(string prompt, ActionID action, bool checkRecast, bool checkCasting)
    {
        uint extra;
        var status = _amex.GetActionStatus(action, Service.ObjectTable.LocalPlayer?.TargetObjectId ?? 0xE0000000, checkRecast, checkCasting, &extra);
        ImGui.TextUnformatted($"{prompt}: {status} [{extra}] '{Service.LuminaRow<Lumina.Excel.Sheets.LogMessage>(status)?.Text}'");
    }

    private void DrawFilteredActions(string tag, Func<Lumina.Excel.Sheets.Action, bool> filter)
    {
        foreach (var nr in _tree.Node(Loc.Id(tag)))
        {
            foreach (var a in Service.LuminaSheet<Lumina.Excel.Sheets.Action>()!)
            {
                if (!filter(a))
                {
                    continue;
                }

                _tree.LeafNode($"#{a.RowId} {a.Name}");
            }
        }
    }

    //private byte SetAutoAttackDetour(byte* self, byte value, byte sendPacket, byte isInstant)
    //{
    //    if (*self != 0 || value != 0)
    //        Service.Log($"SAA: {*self} -> {value} ({sendPacket}, {isInstant})");
    //    return _hook.Original(self, value, sendPacket, isInstant);
    //}
}
