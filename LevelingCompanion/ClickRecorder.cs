using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Hooking;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace LevelingCompanion;

/// <summary>
///     Records what the Companion window sends when a skill is clicked by hand: every callback of the
///     "Buddy" window and every event reaching the Buddy agent, written to the log and kept for the plugin
///     window. Its hooks exist only while recording is switched on; nothing is hooked otherwise.
///     Needed once: the game's skill click is not documented, and the recording shows what to send.
/// </summary>
internal sealed unsafe class ClickRecorder : IDisposable
{
    private const int Kept = 30;

    private Hook<AtkUnitBase.Delegates.FireCallback>? callbackHook;

    private Hook<AgentInterface.Delegates.ReceiveEvent>? agentHook;

    internal List<string> Lines { get; } = [];

    internal bool Recording => this.callbackHook != null;

    internal void Start()
    {
        if (this.Recording)
            return;
        this.callbackHook = Svc.Hook.HookFromAddress<AtkUnitBase.Delegates.FireCallback>(AtkUnitBase.MemberFunctionPointers.FireCallback, this.OnCallback);
        this.callbackHook.Enable();

        AgentInterface* agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Buddy);
        this.agentHook = Svc.Hook.HookFromAddress<AgentInterface.Delegates.ReceiveEvent>((nint)agent->VirtualTable->ReceiveEvent, this.OnAgentEvent);
        this.agentHook.Enable();
        this.Add("Recording: learn one skill by hand in the Companion window's Skills tab.");
    }

    internal void Stop()
    {
        this.callbackHook?.Dispose();
        this.agentHook?.Dispose();
        this.callbackHook = null;
        this.agentHook    = null;
    }

    public void Dispose() => this.Stop();

    private bool OnCallback(AtkUnitBase* addon, uint count, AtkValue* values, bool close)
    {
        try
        {
            string name = addon->NameString;
            if (name is "Buddy" or "SelectYesno")
                this.Add($"callback {name}: {Decode(count, values)}");
        }
        catch (Exception e)
        {
            Svc.Log.Error(e, "LevelingCompanion: recording a callback");
        }
        return this.callbackHook!.Original(addon, count, values, close);
    }

    private AtkValue* OnAgentEvent(AgentInterface* agent, AtkValue* result, AtkValue* values, uint count, ulong kind)
    {
        try
        {
            // The function may be shared with other agents.
            if (agent == AgentModule.Instance()->GetAgentByInternalId(AgentId.Buddy))
                this.Add($"agent Buddy event kind {kind}: {Decode(count, values)}");
        }
        catch (Exception e)
        {
            Svc.Log.Error(e, "LevelingCompanion: recording an agent event");
        }
        return this.agentHook!.Original(agent, result, values, count, kind);
    }

    private static string Decode(uint count, AtkValue* values) =>
        string.Join(", ", Callback.DecodeValues((int)count, values).Select(v => $"[{v}]"));

    private void Add(string line)
    {
        Svc.Log.Info($"LevelingCompanion recorder: {line}");
        this.Lines.Add($"{DateTime.Now:HH:mm:ss} {line}");
        if (this.Lines.Count > Kept)
            this.Lines.RemoveAt(0);
    }
}
