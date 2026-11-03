using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.Interop;

namespace BossMod;

public sealed class DebugObjects
{
    private readonly UITree _tree = new();
    private bool _showCrap;
    private ulong _selectedID;

    public unsafe void DrawObjectTable()
    {
        ImGui.Checkbox(Loc.Id("Show players, minions and mounts"), ref _showCrap);

        Span<nint> handlers = stackalloc nint[32];
        IGameObject? selected = null;
        for (var i = 0; i < Service.ObjectTable.Length; ++i)
        {
            var obj = Service.ObjectTable[i];
            if (obj == null)
            {
                continue;
            }

            if (!_showCrap && obj.ObjectKind is Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Pc or Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Companion or Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Mount)
            {
                continue;
            }

            var internalObj = Utils.GameObjectInternal(obj);
            var localID = internalObj->LayoutId;
            ulong uniqueID = internalObj->GetGameObjectId();

            var posRot = new Vector4(obj.Position.X, obj.Position.Y, obj.Position.Z, obj.Rotation);
            foreach (var n in _tree.Node($"#{i} {Utils.ObjectString(obj)} ({localID:X}) ({Utils.ObjectKindString(obj)}) {Utils.PosRotString(posRot)}###{uniqueID:X}", contextMenu: () => ObjectContextMenu(obj), select: () => _selectedID = uniqueID))
            {
                var character = obj as ICharacter;
                var internalChara = Utils.CharacterInternal(character);

                _tree.LeafNode(Loc.Format("Unique ID: {0:X}", uniqueID));
                _tree.LeafNode(Loc.Format("Layout ID: {0:X}", Utils.GameObjectInternal(obj)->LayoutId));
                _tree.LeafNode(Loc.Format("Gimmick ID: {0:X}", Utils.ReadField<uint>(internalObj, 0x80)));
                _tree.LeafNode(Loc.Format("Radius: {0:f3}", obj.HitboxRadius));
                _tree.LeafNode(Loc.Format("Owner: {0}", Utils.ObjectString(obj.OwnerId)));
                _tree.LeafNode($"BNpcBase/Name: {obj.BaseId:X}/{Utils.GameObjectInternal(obj)->GetNameId()}");
                _tree.LeafNode(Loc.Format("Targetable: {0}", obj.IsTargetable));
                _tree.LeafNode(Loc.Format("Is character: {0}", internalObj->IsCharacter()));
                _tree.LeafNode(Loc.Format("Event state: {0}", Utils.GameObjectInternal(obj)->EventState));
                _tree.LeafNode(Loc.Format("Renderflags: {0}", (int)Utils.GameObjectInternal(obj)->RenderFlags));
                foreach (var n1 in _tree.Node(Loc.Id("Event IDs")))
                {
                    _tree.LeafNode(Loc.Format("Primary: {0:X}", internalObj->EventId.Id));
                    if (internalObj->EventHandler != null)
                    {
                        _tree.LeafNode($"EH: {internalObj->EventHandler->Info.EventId.Id:X}");
                    }

                    var numHandlers = internalObj->GetEventHandlersImpl((FFXIVClientStructs.FFXIV.Client.Game.Event.EventHandler**)handlers.GetPointer(0));
                    for (var iH = 0; iH < numHandlers; iH++)
                    {
                        _tree.LeafNode($"[{iH}]: {((FFXIVClientStructs.FFXIV.Client.Game.Event.EventHandler*)handlers[iH])->Info.EventId.Id:X}");
                    }
                }
                if (character != null)
                {
                    _tree.LeafNode(Loc.Format("Category: {0}", ActionManager.ClassifyTarget(internalChara)));
                    _tree.LeafNode(Loc.Format("Class: {0} ({1})", (Class)character.ClassJob.RowId, character.ClassJob.RowId));
                    _tree.LeafNode($"HP: {character.CurrentHp}/{character.MaxHp} ({internalChara->ShieldValue})");
                    _tree.LeafNode(Loc.Format("Status flags: {0}", character.StatusFlags));
                }
                if (obj is IBattleChara battleChara)
                {
                    _tree.LeafNode(Loc.Format("Cast: {0} {1}", Utils.CastTimeString(battleChara.CurrentCastTime, battleChara.TotalCastTime), new ActionID((ActionType)battleChara.CastActionType, battleChara.CastActionId)));
                    foreach (var nn in _tree.Node(Loc.Id("Statuses")))
                    {
                        for (var j = 0; j < battleChara.StatusList.Length; ++j)
                        {
                            var s = battleChara.StatusList[j];
                            if (s == null || s.StatusId == 0)
                            {
                                continue;
                            }

                            _tree.LeafNode(Loc.Format("#{0}: {1} ({2:X}) from {3}, {4:f3}s left", j, Utils.StatusString(s.StatusId), s.Param, Utils.ObjectString(s.SourceId), s.RemainingTime));
                        }
                    }
                    var fi = ((Character*)battleChara.Address)->GetForayInfo();
                    if (fi != null)
                    {
                        _tree.LeafNode(Loc.Format("Foray level: {0}", fi->Level));
                        _tree.LeafNode(Loc.Format("Foray ele: {0}", fi->Element));
                    }
                }
            }

            if (uniqueID == _selectedID)
            {
                selected = obj;
            }
        }

        if (selected != null)
        {
            var h = new Vector3(0f, Utils.GameObjectInternal(selected)->Height, 0f);
            var h2 = 2f * h;
            var pos = selected.Position;
            var radius = selected.HitboxRadius;
            if (Camera.Instance is Camera cam)
            {
                cam.DrawWorldLine(Service.ObjectTable.LocalPlayer?.Position ?? default, pos, Colors.TextColor3);
                var col = Colors.TextColor4;
                cam.DrawWorldCircle(pos, radius, col);
                cam.DrawWorldCircle(pos + h, radius, col);
                cam.DrawWorldCircle(pos + h * 2f, radius, col);
                var numSegments = CurveApprox.CalculateCircleSegments(radius, 360f.Degrees(), 1f);
                var invNumSegments = 1f / numSegments;
                for (var i = 0; i < numSegments; ++i)
                {
                    var p = pos + radius * (i * 360f * invNumSegments).Degrees().ToDirection().ToVec3();
                    cam.DrawWorldLine(p, p + h2, col);
                }
            }
        }
    }

    public unsafe void DrawUIObjects()
    {
        var module = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->UIModule->GetUI3DModule();
        ImGui.BeginTable("uiobj", 3, ImGuiTableFlags.Resizable);
        ImGui.TableSetupColumn(Loc.Tr("Index"));
        ImGui.TableSetupColumn("GameObj");
        ImGui.TableSetupColumn("NamePlateKind");
        ImGui.TableHeadersRow();
        for (var i = 0; i < 426; ++i)
        {
            var o = module->ObjectInfos[i].GameObject;
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{i}: {(ulong)o:X}");
            ImGui.TableNextColumn();
            if (o != null)
            {
                ImGui.TextUnformatted($"{o->BaseId:X} '{o->NameString}' <{o->EntityId:X}>");
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{module->ObjectInfos[i].NamePlateObjectKind}");
        }
        ImGui.EndTable();
    }

    public static unsafe void DumpObjectTable()
    {
        var res = new StringBuilder("--- object table dump ---");
        foreach (var obj in Service.ObjectTable)
        {
            var internalObj = Utils.GameObjectInternal(obj);
            res.Append($"\nobj {Utils.ObjectString(obj)}, kind={Utils.ObjectKindString(obj)}, pos={Utils.Vec3String(obj.Position)}, rot={obj.Rotation.Radians()}, renderFlags={internalObj->RenderFlags}, vfxScale={internalObj->VfxScale}, targetable={internalObj->GetIsTargetable()}, drawPtr=0x{(IntPtr)internalObj->DrawObject:X}");
            if (internalObj->DrawObject != null)
            {
                res.Append($", drawPos={Utils.Vec3String(internalObj->DrawObject->Object.Position)}, drawScale={Utils.Vec3String(internalObj->DrawObject->Object.Scale)}");
            }

            if (obj is IBattleChara chara)
            {
                res.Append($", vfxObj=0x{Utils.ReadField<ulong>(internalObj, 0x1840):X}/0x{Utils.ReadField<ulong>(internalObj, 0x1848):X}");
                if (chara.IsCasting)
                {
                    var target = Service.ObjectTable.SearchById(chara.CastTargetObjectId);
                    var targetString = target != null ? Utils.ObjectString(target) : "unknown";
                    res.Append($", castAction={new ActionID((ActionType)chara.CastActionType, chara.CastActionId)}, castTarget={targetString}, castLoc={Utils.Vec3String(Utils.BattleCharaInternal(chara)->GetCastInfo()->TargetLocation)}, castTime={Utils.CastTimeString(chara.CurrentCastTime, chara.TotalCastTime)}");
                }
                foreach (var status in chara!.StatusList)
                {
                    var src = status.SourceObject != null ? Utils.ObjectString(status.SourceObject) : "none";
                    res.Append($"\n  status {status.StatusId} '{status.GameData.Value.Name}': param={status.Param}, stacks={status.Param}, time={status.RemainingTime:f2}, source={src}");
                }
            }
        }
        res.Append("\n--- cid/acid (C) ---");
        var gom = GameObjectManager.Instance();
        for (var i = 0; i < 100; ++i)
        {
            var obj = gom->Objects.IndexSorted[i * 2].Value;
            if (obj != null && obj->IsCharacter())
            {
                var chara = (Character*)obj;
                res.Append($"\n{i}: {chara->AccountId:X}.{chara->ContentId:X} = {obj->NameString}");
            }
        }
        res.Append("\n--- cid/acid (P) ---");
        var gp = GroupManager.Instance()->GetGroup();
        for (var i = 0; i < gp->MemberCount; ++i)
        {
            ref var member = ref gp->PartyMembers[i];
            res.Append($"\n{i}: {member.AccountId:X}.{member.ContentId:X} = {member.NameString} / {(member.NameOverride != null ? member.NameOverride->ToString() : "<null>")}");
        }
        Service.Log(res.ToString());
    }

    private unsafe void ObjectContextMenu(IGameObject obj)
    {
        if (ImGui.MenuItem(Loc.Id("Target")))
        {
            Service.TargetManager.Target = obj;
        }

        if (ImGui.MenuItem(Loc.Id("Interact")))
        {
            TargetSystem.Instance()->InteractWithObject(Utils.GameObjectInternal(obj));
        }
    }
}
