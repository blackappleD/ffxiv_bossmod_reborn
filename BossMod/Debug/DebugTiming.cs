using Dalamud.Bindings.ImGui;

namespace BossMod;

public sealed class DebugTiming
{
    uint _prevFrameCounter;
    long _prevQPC;

    public unsafe void Draw()
    {
        var fwk = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
        var dtReal = (double)(fwk->PerformanceCounterValue - _prevQPC) / fwk->PerformanceCounterFrequency;
        ImGui.TextUnformatted(Loc.Format("Frame counter: {0}", fwk->FrameCounter));
        ImGui.TextUnformatted(Loc.Format("Frame time effective: {0}", fwk->FrameDeltaTime));
        ImGui.TextUnformatted(Loc.Format("Framerate: {0}", fwk->FrameRate));
        ImGui.TextUnformatted(Loc.Format("Forced frame duration: {0}", fwk->FrameDeltaTimeOverride));
        ImGui.TextUnformatted(Loc.Format("Forced next frame duration: {0}", fwk->NextFrameDeltaTimeOverride));
        ImGui.TextUnformatted(Loc.Format("Frame duration multiplier: {0}", fwk->FrameDeltaFactor));
        ImGui.TextUnformatted(Loc.Format("Tick speed multiplier: {0}", fwk->GameSpeedMultiplier));
        ImGui.TextUnformatted(Loc.Format("QPC freq: {0}", fwk->PerformanceCounterFrequency));
        ImGui.TextUnformatted(Loc.Format("QPC value: {0}", fwk->PerformanceCounterValue));
        ImGui.TextUnformatted(Loc.Format("dt raw: {0}", fwk->RealFrameDeltaTime));
        ImGui.TextUnformatted(Loc.Format("dt real: {0} = raw + {1}", dtReal, dtReal - fwk->RealFrameDeltaTime));
        ImGui.TextUnformatted(Loc.Format("dt ms granularity: {0} + {1}", fwk->FrameDeltaTimeMSInt, fwk->FrameDeltaTimeMSRem));
        ImGui.TextUnformatted(Loc.Format("dt us granularity: {0} + {1}", fwk->FrameDeltaTimeUSInt, fwk->FrameDeltaTimeUSRem));
        ImGui.TextUnformatted(Loc.Format("dt timer: {0}", DateTime.UnixEpoch.AddSeconds(fwk->UtcTime.Timestamp)));
        _prevFrameCounter = fwk->FrameCounter;
        _prevQPC = fwk->PerformanceCounterValue;
    }
}
