using Sandbox;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ZContracts;

public static partial class ContractManager
{
    [ConCmd.Server("set_contract")]
    public static void SetContract( string UUID )
    {
        var Client = ConsoleSystem.Caller;
        // TODO: We should probably make this just a Get() since we don't want to
        // force pawns to use ZContracts.
        var Comp = Client.Pawn.Components.Get<ActiveContract>();
        if (Comp == null) return;

        if ( !ContractSchemas.ContainsKey(UUID) )
        {
            Log.Error($"{Client.Name} tried to select invalid Contract \"{UUID}\"!");
            return;
        }
        Comp.Schema = ContractSchemas[UUID];
        Comp.Threshold.Clear();
        Comp.Timer.Clear();
        Comp.Uses.Clear();
        Comp.RefreshDicts();
        Comp.Progress = 0;

        Log.Info($"Set {Client.Name} contract to \"{UUID}\"!");
    }

    [ConCmd.Server("remove_contract")]
    public static void RemoveContract()
    {
        var Client = ConsoleSystem.Caller;
        // TODO: We should probably make this just a Get() since we don't want to
        // force pawns to use ZContracts.
        var Comp = Client.Pawn.Components.Get<ActiveContract>();
        if (Comp == null) return;

        Comp.Schema = null;
        Comp.Threshold.Clear();
        Comp.Timer.Clear();
        Comp.Uses.Clear();
        Comp.Progress = 0;

        return;
    }


    // When an event is triggered, process logic.
    [ContractEvents.TriggerEvent]
    public static void TriggerEvent(IClient Client, string IncomingEvent, int value)
    {
        // We only care about this event on the server.
        if (Game.IsClient) return;
        if (Client == null) return;

        var Comp = Client.Pawn.Components.Get<ActiveContract>();
        if (Comp == null)
        {
            Log.Warning($"Tried to fire ZContracts Event \"{IncomingEvent}\" for {Client.Name} but no ActiveContract component exists!");
            return;
        }
        if (Comp.Schema == null) return;
        if (Comp.Completed()) return;

        Comp.TriggerEvent(IncomingEvent, value);
    }
}