using Sandbox;
using System;
using System.Collections;
using System.Collections.Generic;
using static ZContracts.TimerData;

namespace ZContracts;

/// <summary>
/// This warps a dictionary as a struct as we cannot transport a dict
/// inside of another dictionary. While this is less than ideal, it works for me!
/// </summary>
public partial class ThresholdData : BaseNetworkable
{
    [Net] public IDictionary<string, int> Values { get; set; }
}

/// <summary>
/// This warps a dictionary as a struct as we cannot transport a dict
/// inside of another dictionary. While this is less than ideal, it works for me!
/// </summary>
public partial class TimerData : BaseNetworkable
{
    public struct TimerStruct
    {
        public float TimeStarted;
        public float TimeRemaining;
    }

    [Net] public IDictionary<string, TimerStruct> Values { get; set; }
}


public partial class ActiveContract : EntityComponent
{
    public static readonly string Action_AddProgress = "add";
    public static readonly string Action_SubtractProgress = "subtract";
    public static readonly string Action_ResetProgress = "reset";

    /// <summary>
    /// The current progress for this Contract.
    /// </summary>
    [Net] public int Progress { get; set; }
    /// <summary>
    /// Each event in an object has a "threshold" value. Threshold represents the amount of times
    /// a named event needs to be fired before an event is triggered. The maximum threshold is
    /// stored in Schema.
    /// </summary>
    [Net] public IDictionary<int, ThresholdData> Threshold { get; set; }
    /// <summary>
    /// Each event can have a timer associated with it. The timer represents how long a player has
    /// to reach the event threshold before it's reset.
    /// </summary>
    [Net] public IDictionary<int, TimerData> Timer { get; set; }
    /// <summary>
    /// Each Objective can specify how many times it can be completed (see ContractDefinition.Uses).
    /// This is separate from Event thresholds where that tracks how many times a game-event
    /// (e.g killing a player) has been fired.
    /// </summary>
    [Net] public IDictionary<int, int> Uses { get; set; }

    /// <summary>
    /// The definition file for the active Contract.
    /// </summary>
    [Net] public ContractDefinition Schema { get; set; }

    /// <summary>
    /// Checks to see if the Contract is completed.
    /// </summary>
    /// <returns>Returns true if Progress is greater or equal to the maximum progress defined in the Schema. False otherwise.</returns>
    public bool Completed()
    {
        return Progress >= Schema.MaximumProgress;
    }

    public ActiveContract()
    {
        Threshold = new Dictionary<int, ThresholdData>();
        Timer = new Dictionary<int, TimerData>();
        Uses = new Dictionary<int, int>();
    }

    /// <summary>
    /// Refreshes the dictionaries responsible for timers and thresholds.
    /// </summary>
    public void RefreshDicts()
    {
        // TODO: I'm getting NetworkTable messages related to these dicts.
        // Is that an issue or should I just ignore it?
        Threshold.Clear();
        Timer.Clear();
        Uses.Clear();

        for (int ObjectiveID = 0; ObjectiveID < Schema.Objectives.Count; ObjectiveID++)
        {
            var ThreshData = new ThresholdData();
            var TimeData = new TimerData();
            foreach (var Event in Schema.Objectives[ObjectiveID].Events)
            {
                var ThreshDict = ThreshData.Values;
                if (!ThreshDict.ContainsKey(Event.EventName))
                {
                    ThreshDict[Event.EventName] = 0;
                }

                if (Event.Time > 0)
                {
                    var TimeDict = TimeData.Values;
                    if (!TimeData.Values.ContainsKey(Event.EventName))
                    {
                        var Struct = new TimerStruct();
                        Struct.TimeRemaining = 0.0f;
                        Struct.TimeStarted = 0.0f;
                        TimeDict[Event.EventName] = Struct;
                    }
                }
            }

            Threshold.Add(ObjectiveID, ThreshData);
            if (TimeData.Values.Count > 0) Timer.Add(ObjectiveID, TimeData);
            Uses.Add(ObjectiveID, 0);
        }
    }

    /// <summary>
    /// Processes an event fired from somewhere in-game.
    /// </summary>
    /// <param name="IncomingEvent">The name of the event.</param>
    /// <param name="value">Value passed by the event that will be added to the threshold.</param>
    public void TriggerEvent(string IncomingEvent, int value)
    {
        for (int ObjectiveID = 0; ObjectiveID < Schema.Objectives.Count; ObjectiveID++)
        {
            var Objective = Schema.Objectives[ObjectiveID];

            // Have we already hit our maximum amount of uses?
            if (Objective.Uses > 0 && Uses[ObjectiveID] >= Objective.Uses) return;

            foreach (var Event in Objective.Events)
            {
                if (Event.EventName == IncomingEvent)
                {
                    // If this Event should have a timer, start one!
                    if (Event.Time > 0 && Timer.ContainsKey(ObjectiveID))
                    {
                        var ObjectiveTimerData = Timer[ObjectiveID].Values;
                        if (ObjectiveTimerData[Event.EventName].TimeRemaining == 0.0f)
                        {
                            var Struct = ObjectiveTimerData[Event.EventName];
                            Struct.TimeStarted = Time.Now;
                            Struct.TimeRemaining = Event.Time;
                            ObjectiveTimerData[Event.EventName] = Struct;
                        }
                    }

                    // Process threshold logic.
                    var ObjectiveThresholdData = Threshold[ObjectiveID].Values;
                    ObjectiveThresholdData[Event.EventName] += value;
                    if (ObjectiveThresholdData[Event.EventName] >= Event.Threshold)
                    {
                        // Cancel our timer if we have one going!
                        if (Event.Time > 0 && Timer.ContainsKey(ObjectiveID))
                        {
                            var ObjectiveTimerData = Timer[ObjectiveID].Values;
                            if (ObjectiveTimerData[Event.EventName].TimeRemaining != 0.0f)
                            {
                                var Struct = ObjectiveTimerData[Event.EventName];
                                Struct.TimeStarted = 0.0f;
                                Struct.TimeRemaining = 0.0f;
                                ObjectiveTimerData[Event.EventName] = Struct;
                            }
                        }

                        if (Event.Action == Action_AddProgress) Progress += Objective.Award;
                        else if (Event.Action == Action_SubtractProgress) Progress -= Objective.Award;
                        else if (Event.Action == Action_ResetProgress) Progress = 0;

                        ObjectiveThresholdData[Event.EventName] = 0;

                        if (Objective.Uses > 0)
                        {
                            Uses[ObjectiveID]++;
                            Uses[ObjectiveID] = Math.Clamp(Uses[ObjectiveID]++, 0, Objective.Uses);
                        }
                    }
                }
            }
        }
        Progress = Math.Clamp(Progress, 0, Schema.MaximumProgress);
    }

    [ConVar.Client("zcontracts_draw_debug")]
    public static bool DebugEnabled { get; set; } = true;

    [GameEvent.Tick]
    /// <summary>
    /// Client: Draws debug text (might be removed in the future!)
    /// Server: Checks every tick to see if the timer is expired.
    /// </summary>
    public void Tick()
    {
        if (Schema == null) return;
        // We've got to wait for these dicts to be constructed first!
        if (Threshold == null) return;
        if (Timer == null) return;
        if (Uses == null) return;

        // Temporary debug drawing.
        if (Game.IsClient && DebugEnabled)
        {
            DebugOverlay.ScreenText($"Loaded Contract: {Schema.DisplayName} (FILE: \"{Schema.ResourcePath}\")", 0, 0.1f);
            DebugOverlay.ScreenText($"Contract Progress: {Progress}/{Schema.MaximumProgress}", 1, 0.1f);
            DebugOverlay.ScreenText($"========== OBJECTIVES (total: {Schema.Objectives.Count}) ==========", 3, 0.1f);
            int DebugLine = 4;
            for (int ObjectiveID = 0; ObjectiveID < Schema.Objectives.Count; ObjectiveID++)
            {
                var Objective = Schema.Objectives[ObjectiveID];
                DebugOverlay.ScreenText($"  Objective {ObjectiveID}: \"{Objective.DisplayName}\"", DebugLine, 0.1f); DebugLine++;
                DebugOverlay.ScreenText($"  Award: {Objective.Award}", DebugLine, 0.1f); DebugLine++;
                DebugOverlay.ScreenText($"  Uses: {Uses[ObjectiveID]}/{Objective.Uses} (is infinite: {(Objective.Uses == 0).ToString()})", DebugLine, 0.1f); DebugLine++;
                DebugOverlay.ScreenText($"  ========== EVENTS (total {Objective.Events.Count}) ==========", DebugLine, 0.1f); DebugLine++;
                foreach (var Event in Objective.Events)
                {
                    var ObjectiveThresholdData = Threshold[ObjectiveID].Values;
                    DebugOverlay.ScreenText($"      Event Name: \"{Event.EventName}\"", DebugLine, 0.1f); DebugLine++;
                    DebugOverlay.ScreenText($"      Event Threshold: {ObjectiveThresholdData[Event.EventName]}/{Event.Threshold}", DebugLine, 0.1f); DebugLine++;
                    DebugOverlay.ScreenText($"      Event Action: \"{Event.Action}\"", DebugLine, 0.1f); DebugLine++;
                    DebugOverlay.ScreenText($"      Event Variable: {Event.Variable}", DebugLine, 0.1f); DebugLine++;
                    if (Event.Time > 0 && Timer.ContainsKey(ObjectiveID))
                    {
                        var ObjectiveTimerData = Timer[ObjectiveID].Values;
                        DebugOverlay.ScreenText($"      Event Timer: {ObjectiveTimerData[Event.EventName].TimeRemaining}/{Event.Time}", DebugLine, 0.1f); DebugLine++;
                    }
                    
                }
                DebugOverlay.ScreenText($"  ========== EVENTS END ==========", DebugLine, 0.1f); DebugLine++;
                DebugOverlay.ScreenText($"  ================================", DebugLine, 0.1f); DebugLine++;
            }
            DebugOverlay.ScreenText($"========== OBJECTIVES END ==========", DebugLine, 0.1f); DebugLine++;
        }

        // Timer logic.
        if (Game.IsServer)
        {
            for (int ObjectiveID = 0; ObjectiveID < Schema.Objectives.Count; ObjectiveID++)
            {
                if (!Timer.ContainsKey(ObjectiveID)) return;
                var Objective = Schema.Objectives[ObjectiveID];
                foreach (var Event in Objective.Events)
                {
                    var ObjectiveTimerData = Timer[ObjectiveID].Values;
                    var ObjectiveThresholdData = Threshold[ObjectiveID].Values;

                    if (Event.Time > 0 && ObjectiveTimerData[Event.EventName].TimeRemaining != 0.0f)
                    {
                        // Reset the threshold if our timer is expired.
                        var Struct = ObjectiveTimerData[Event.EventName];
                        Struct.TimeRemaining = Struct.TimeStarted + Event.Time - Time.Now;

                        if (Struct.TimeRemaining <= 0.0f)
                        {
                            Struct.TimeStarted = 0.0f;
                            Struct.TimeRemaining = 0.0f;
                            ObjectiveThresholdData[Event.EventName] = 0;
                            //Log.Info($"Timer expired for {Entity.Client.Name} (Objective ID: {ObjectiveID}, Event Name: \"{Event.EventName}\")");
                        }
                        ObjectiveTimerData[Event.EventName] = Struct;
                    }
                }
            }
        }
    }
}