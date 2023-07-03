using Sandbox;
using System;
using System.Collections.Generic;

namespace ZContracts;

public struct EventData
{
    [Property]
    [Description("The name of the event to listen for in code.")]
    public string EventName { get; set; }
    [Property]
    [Description("How many times this event needs to be fired before it's triggered.")]
    public int Threshold { get; set; }
    [Property]
    [Description("What to do when this event is triggered (e.g add progress)")]
    public string Action { get; set; }
    [Property]
    [Description("Number thats used with the action.")]
    public int Variable { get; set; }

    [Property]
    [Category("Timer")]
    [Description("The length of the timer.")]
    public float Time { get; set; }
}

public struct ObjectiveData
{
    [Property]
    [Category("Basic Information")]
    [Description("Display name of the Objective.")]
    public string DisplayName { get; set; }

    [Property]
    [Category("Basic Information")]
    [Description("The award thats added to the progress when an event is triggered.")]
    public int Award { get; set; }

    [Property]
    [Category("Events")]
    [Description("The display name of the Objective.")]
    public List<EventData> Events { get; set; }
}

[GameResource("Contract Definition", "contract", "Outlines a Contract that can be completed by a player.")]
public partial class ContractDefinition : GameResource
{
    [Property]
    [Category("Basic Information")]
    [Description("The uniuqe identifier of this Contract. UUID4 is recommended!")]
    public string UUID { get; set; }

    [Property]
    [Category("Basic Information")]
    [Description("The display name of the Contract.")]
    public string DisplayName { get; set; }

    [Property]
    [Category("Progress")]
    [Description("The maximum progress value needed to complete this Objective.")]
    public int MaximumProgress { get; set; }

    [Property]
    [Category("Objectives")]
    [Description("The display name of the Contract.")]
    public List<ObjectiveData> Objectives { get; set; }

    // Hotloading support, mainly for debugging!
    protected override void PostReload()
    {
        base.PostReload();
        if (Game.IsServer) { ContractManager.ReloadContract(this.ResourcePath); }
    }
}
