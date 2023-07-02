using System;
using System.Collections.Generic;

using Sandbox;
namespace ZContracts;

public static class ContractEvents
{
    public const string TriggerEvent = "zcontracts.trigger_event";
    public class TriggerEventAttribute : EventAttribute
    {
        public TriggerEventAttribute() : base(TriggerEvent) { }
    }
    public const string SaveProgress = "zcontracts.save_progress";
    public class SaveProgressAttribute : EventAttribute
    {
        public SaveProgressAttribute() : base(SaveProgress) { }
    }
}

public static class ContractSchemaEvents
{
    public const string ContractReloaded = "zcontracts.contract_reloaded";
    public class ContractReloadedAttribute : EventAttribute
    {
        public ContractReloadedAttribute() : base(ContractReloaded) { }
    }
}