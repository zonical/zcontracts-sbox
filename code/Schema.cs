using System;
using System.Collections.Generic;
using System.Reflection;
using Sandbox;

namespace ZContracts;

public static partial class ContractManager
{
    // The server is the only place where all the schemas should be stored as we
    // don't want clients modifying their contract data at runtime.
    public static IDictionary<string, ContractDefinition> ContractSchemas;
    static ContractManager()
    {
        ContractSchemas = new Dictionary<string, ContractDefinition>();
        LoadAllContracts();
    }

    // Refreshes the contract schema on the server.
    [ConCmd.Server("reload_contracts")]
    public static void LoadAllContracts()
    {
        ContractSchemas.Clear();
        var AllContractFiles = ResourceLibrary.GetAll<ContractDefinition>();
        foreach (var ContractFile in AllContractFiles)
        {
            if (!ContractSchemas.ContainsKey(ContractFile.UUID))
            {
                ContractSchemas.Add(ContractFile.UUID, ContractFile);
                Log.Info($"Loaded Contract: {ContractFile.UUID}");
            }
        }
        Log.Info($"Loaded {ContractSchemas.Count} contracts!");
    }

    // Refreshes a specific contract in the schema.
    [ConCmd.Server("reload_contract")]
    public static void ReloadContract(string ContractPath)
    {
        var ContractFile = ResourceLibrary.Get<ContractDefinition>(ContractPath);
        if (ContractFile == null)
        {
            Log.Error($"Failed to reload contract: \"{ContractPath}\"");
            return;
        }

        if (ContractSchemas.ContainsKey(ContractFile.UUID))
        {
            ContractSchemas.Remove(ContractFile.UUID);
        }
        ContractSchemas.Add(ContractFile.UUID, ContractFile);

        // Refresh the schema for all clients who have this contract selected.
        foreach (var Client in Game.Clients)
        {
            var Comp = Client.Pawn.Components.Get<ActiveContract>();
            if (Comp == null) continue;
            if (Comp.Schema.UUID == ContractFile.UUID)
            {
                Comp.Schema = ContractSchemas[ContractFile.UUID];
                Comp.RefreshDicts();
            }
        }
    }

    // Prints a list of all the contracts.
    [ConCmd.Server("list_contracts")]
    public static void ListAllContracts()
    {
        Log.Info("========================= LIST OF CONTRACTS =========================");
        foreach (var ContractFile in ContractSchemas.Values)
        {
            Log.Info(ContractFile.UUID + ": " + ContractFile.DisplayName);
        }
    }
}

