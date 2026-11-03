namespace BossMod.ReplayAnalysis;

sealed class ClientActions
{
    private readonly List<(Replay r, DateTime ts, string warning)> _warnings = [];

    public ClientActions(List<Replay> replays)
    {
        foreach (var r in replays)
        {
            List<ClientState.OpActionRequest> pendingRequests = [];
            ulong playerID = 0;
            ActorCastInfo? pendingCast = null;
            foreach (var op in r.Ops)
            {
                switch (op)
                {
                    case PartyState.OpModify o:
                        if (o.Slot == PartyState.PlayerSlot)
                        {
                            playerID = o.Member.InstanceId;
                            // TODO: is that correct?..
                            pendingRequests.Clear();
                            pendingCast = null;
                        }
                        break;

                    case ClientState.OpActionRequest o:
                        if (pendingCast != null && pendingRequests.Count > 0 && pendingCast.Action == pendingRequests[0].Request.Action)
                        {
                            // cast-end will arrive later
                            pendingCast = null;
                            pendingRequests.RemoveAt(0);
                        }
                        if (pendingRequests.Count > 0)
                        {
                            _warnings.Add((r, op.Timestamp, Loc.Format("New action request {0} while {1} are pending (first = {2}, {3:f3}s ago)", o.Request, pendingRequests.Count, pendingRequests[0].Request, (op.Timestamp - pendingRequests[0].Timestamp).TotalSeconds)));
                        }
                        pendingRequests.Add(o);
                        break;

                    case ClientState.OpActionReject o:
                        var rejIndex = o.Value.SourceSequence != 0
                            ? pendingRequests.FindIndex(a => a.Request.SourceSequence == o.Value.SourceSequence)
                            : pendingRequests.FindIndex(a => a.Request.Action == o.Value.Action);
                        if (rejIndex < 0)
                        {
                            _warnings.Add((r, op.Timestamp, Loc.Format("Reject {0}: unexpected (not found); currently {1} are pending", o.Value, pendingRequests.Count)));
                            pendingRequests.Clear();
                        }
                        else if (rejIndex > 0)
                        {
                            _warnings.Add((r, op.Timestamp, Loc.Format("Reject {0}: unexpected (index {1}); currently {2} are pending, first={3}", o.Value, rejIndex, pendingRequests.Count, pendingRequests[0].Request)));
                            pendingRequests.RemoveRange(0, rejIndex + 1);
                        }
                        else if (pendingRequests[0].Request.SourceSequence != o.Value.SourceSequence || pendingRequests[0].Request.Action != o.Value.Action)
                        {
                            _warnings.Add((r, op.Timestamp, Loc.Format("Reject {0}: mismatched, expected {1}", o.Value, pendingRequests[0].Request)));
                            pendingRequests.RemoveAt(0);
                        }
                        else
                        {
                            _warnings.Add((r, op.Timestamp, Loc.Format("Reject {0}: all good, but still", o.Value)));
                            pendingRequests.RemoveAt(0);
                        }
                        pendingCast = null; // TODO: right?
                        break;

                    case ActorState.OpCastEvent o:
                        if (o.Value.SourceSequence != 0 && o.InstanceID == playerID)
                        {
                            var rqIndex = pendingRequests.FindIndex(r => r.Request.SourceSequence == o.Value.SourceSequence);
                            if (rqIndex < 0)
                            {
                                _warnings.Add((r, op.Timestamp, Loc.Format("Unexpected action-effect {0}: currently {1} are pending", StrEvt(o.Value), pendingRequests.Count)));
                                pendingRequests.Clear();
                            }
                            else if (rqIndex > 0)
                            {
                                _warnings.Add((r, op.Timestamp, Loc.Format("Unexpected action-effect {0}: index={1}, first={2}, count={3}", StrEvt(o.Value), rqIndex, pendingRequests[0].Request, pendingRequests.Count)));
                            }
                            else if (pendingRequests[0].Request.Action != o.Value.Action)
                            {
                                _warnings.Add((r, op.Timestamp, Loc.Format("Request/response action mismatch: requested {0}, got {1}", pendingRequests[0].Request, StrEvt(o.Value))));
                            }
                            else if ((op.Timestamp - pendingRequests[0].Timestamp).TotalSeconds >= pendingRequests[0].Request.InitialAnimationLock + pendingRequests[0].Request.InitialCastTimeTotal)
                            {
                                _warnings.Add((r, op.Timestamp, Loc.Format("Response {0} arrived too late: {1:f3}s ago, initial anim lock was {2:f3}s", StrEvt(o.Value), (op.Timestamp - pendingRequests[0].Timestamp).TotalSeconds, pendingRequests[0].Request.InitialAnimationLock)));
                            }
                            pendingRequests.RemoveRange(0, rqIndex + 1);
                            pendingCast = null;
                        }
                        break;

                    case ActorState.OpCastInfo o:
                        if (o.InstanceID == playerID)
                        {
                            if (o.Value != null)
                            {
                                // start new cast
                                if (pendingRequests.Count == 0)
                                {
                                    _warnings.Add((r, op.Timestamp, Loc.Format("Player cast {0} started without request", StrCast(o.Value))));
                                }
                                else if (pendingRequests[0].Request.Action != o.Value.Action)
                                {
                                    _warnings.Add((r, op.Timestamp, Loc.Format("Player cast {0} started with different request ({1}, count={2}", StrCast(o.Value), pendingRequests[0].Request, pendingRequests.Count)));
                                }
                                if (pendingCast != null)
                                {
                                    _warnings.Add((r, op.Timestamp, Loc.Format("Player cast {0} started when another cast {1} is already in progress", StrCast(o.Value), StrCast(pendingCast))));
                                }
                                pendingCast = o.Value;
                            }
                            else if (pendingCast != null)
                            {
                                var index = pendingRequests.FindIndex(r => r.Request.Action == pendingCast.Action);
                                if (index < 0)
                                {
                                    _warnings.Add((r, op.Timestamp, Loc.Format("Player cast {0} ended without request ({1} pending)", StrCast(pendingCast), pendingRequests.Count)));
                                    pendingRequests.Clear();
                                }
                                else if (index > 0)
                                {
                                    _warnings.Add((r, op.Timestamp, Loc.Format("Player cast {0} ended with index={1} request; first={2}, count={3}", StrCast(pendingCast), index, pendingRequests[0].Request, pendingRequests.Count)));
                                    pendingRequests.RemoveRange(0, index + 1);
                                }
                                else
                                {
                                    pendingRequests.RemoveAt(0);
                                }
                                pendingCast = null;
                            }
                            // else: this was implicitly ended by different op
                        }
                        break;
                }
            }
        }
    }

    public void Draw(UITree tree) => tree.LeafNodes(_warnings, w => $"{w.r.Path} {w.ts:O}: {w.warning}");

    private string StrEvt(ActorCastEvent e) => $"#{e.SourceSequence} {e.Action} @ {e.MainTargetID:X8}";
    private string StrCast(ActorCastInfo e) => $"{e.Action} @ {e.TargetID:X8}";
}
