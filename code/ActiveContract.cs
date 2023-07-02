using Sandbox;
using System;
using System.Collections.Generic;

namespace ZContracts;

public partial class ActiveContract : EntityComponent
{
    public static readonly string Action_AddProgress = "add";
    public static readonly string Action_SubtractProgress = "subtract";
    public static readonly string Action_ResetProgress = "reset";

    // The current progress for this Contract.
    [Net] public int Progress { get; set; }
    // Each event in an object has a "threshold" value. Threshold represents the amount of times
    // a named event needs to be fired before an event is triggered. The maximum threshold is
    // stored in Schema. TODO: Does this need to be networked?
    public static IDictionary<int, IDictionary<string, int>> Threshold { get; set; }

    // The definition file for the active Contract.
    [Net] public ContractDefinition Schema { get; set; }

    public bool Completed()
    {
        return Progress >= Schema.MaximumProgress;
    }

    public void TriggerEvent(string IncomingEvent, int value)
    {
        for (int ObjectiveID = 0; ObjectiveID < Schema.Objectives.Count; ObjectiveID++)
        {
            var Objective = Schema.Objectives[ObjectiveID];
            foreach (var Event in Objective.Events)
            {
                if (Event.EventName == IncomingEvent)
                {
                    Threshold[ObjectiveID][Event.EventName] += value;
                    if (Threshold[ObjectiveID][Event.EventName] >= Event.Threshold)
                    {
                        if (Event.Action == Action_AddProgress) Progress += value;
                        else if (Event.Action == Action_SubtractProgress) Progress += -value;
                        else if (Event.Action == Action_ResetProgress) Progress = 0;
                    }
                }
            }
        }
        Progress = Math.Clamp(Progress, 0, Schema.MaximumProgress);
    }

    public void RefreshThresholdDicts()
    {
        // TODO: Is this smart design?
        Threshold = new Dictionary<int, IDictionary<string, int>>();
        for (int ObjectiveID = 0; ObjectiveID < Schema.Objectives.Count; ObjectiveID++)
        {
            Threshold[ObjectiveID] = new Dictionary<string, int>();
            foreach (var Event in Schema.Objectives[ObjectiveID].Events)
            {
                var EventDict = Threshold[ObjectiveID];
                if (!EventDict.ContainsKey(Event.EventName))
                {
                    EventDict[Event.EventName] = 0;
                }
            }
        }
    }

    [GameEvent.Tick]
    public void Tick()
    {
        if (Schema == null) return;
        if (Game.IsClient)
        {
            DebugOverlay.ScreenText($"Loaded Contract: {Schema.DisplayName} (FILE: \"{Schema.ResourcePath}\")", 0, 0.1f);
            DebugOverlay.ScreenText($"Contract Progress: {Progress}", 1, 0.1f);
        }        
    }
}