namespace BossMod;

public static class ReplayUtils
{
    public static string ParticipantString(Replay.Participant? p, DateTime t)
    {
        if (p == null)
        {
            return "<none>";
        }

        var name = p.NameAt(t);
        return $"{p.Type} {p.InstanceID:X} ({p.OID:X}/{name.id}) '{name.name}' {p.LayoutID:X}";
    }

    public static string ParticipantPosRotString(Replay.Participant? p, DateTime t) => p != null ? $"{ParticipantString(p, t)} {Utils.PosRotString(p.PosRotAt(t))}" : "<none>";

    public static string ActionEffectString(ActionEffect eff)
    {
        var s = $"{eff.Type}: {eff.Param0:X2} {eff.Param1:X2} {eff.Param2:X2} {eff.Param3:X2} {eff.Param4:X2} {eff.Value:X4}";
        if (eff.FromTarget)
        {
            s = Loc.Tr("(from target) ") + s;
        }

        if (eff.AtSource)
        {
            s = Loc.Tr("(at source) ") + s;
        }

        var desc = ActionEffectParser.DescribeFields(eff);
        if (desc.Length > 0)
        {
            s += $": {desc}";
        }

        return s;
    }

    public static string ActionTargetString(Replay.ActionTarget t, DateTime ts)
    {
        var confirmTarget = t.ConfirmationTarget != default ? Loc.Format("confirmed at +{0:f3}s", (t.ConfirmationTarget - ts).TotalSeconds) : Loc.Tr("unconfirmed");
        var confirmSource = t.ConfirmationSource != default ? Loc.Format("confirmed at +{0:f3}s", (t.ConfirmationSource - ts).TotalSeconds) : Loc.Tr("unconfirmed");
        return Loc.Format("{0}, target {1}, source {2}", ParticipantPosRotString(t.Target, ts), confirmTarget, confirmSource);
    }

    public static int ActionDamage(Replay.ActionTarget a)
    {
        var res = 0;
        var effects = a.Effects.ValidEffects();
        var len = effects.Length;
        for (var i = 0; i < len; ++i)
        {
            ref readonly var eff = ref effects[i];
            if (eff.Type is ActionEffectType.Damage or ActionEffectType.BlockedDamage or ActionEffectType.ParriedDamage && !eff.AtSource)
            {
                res += eff.DamageHealValue;
            }
        }
        return res;
    }
}
