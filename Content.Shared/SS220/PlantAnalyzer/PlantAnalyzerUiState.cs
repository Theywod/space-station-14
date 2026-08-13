using Content.Shared.DoAfter;
using Content.Shared.Atmos;
using Robust.Shared.Serialization;

namespace Content.Shared.SS220.PlantAnalyzer;

[Serializable, NetSerializable]
public enum PlantAnalyzerUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed partial class PlantAnalyzerDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed class PlantAnalyzerScannedUserMessage : BoundUserInterfaceMessage
{
    public PlantAnalyzerUiState State;

    public PlantAnalyzerScannedUserMessage(PlantAnalyzerUiState state)
    {
        State = state;
    }
}

[Serializable, NetSerializable]
public sealed class PlantAnalyzerPrintMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed record PlantChemicalEntry(string ReagentId, string Name, float Min, float Max, bool Inherent);

[Serializable, NetSerializable]
public sealed record PlantGasEntry(Gas Gas, float Amount, bool Consumed);

[Serializable, NetSerializable]
public sealed class PlantAnalyzerUiState
{
    public NetEntity? TargetEntity;
    public bool? ScanMode;
    public bool CanPrint;

    // Общий статус
    public bool HasSeed;
    public bool Dead;
    public bool Mutating; // MutationLevel > 0
    public string? SeedDisplayName;
    public float Health;
    public float MaxHealth; // Seed.Endurance
    public int Age;
    public float Lifespan;
    public float Maturation;
    public float Production;

    // Текущие показатели лотка
    public float WaterLevel;
    public float NutritionLevel;
    public float Toxins;
    public float PestLevel;
    public float WeedLevel;

    // Требования растения к среде
    public float IdealHeat;
    public float HeatTolerance;
    public float IdealLight;
    public float LightTolerance;
    public float LowPressureTolerance;
    public float HighPressureTolerance;
    public float ToxinsTolerance;
    public float PestTolerance;
    public float WeedTolerance;

    // Плоды
    public int Yield;
    public float Potency;
    public bool Seedless;
    public bool Harvest;

    // Состав плодов
    public List<PlantChemicalEntry> Chemicals = new();
    public List<PlantGasEntry> ConsumeGasses = new();
    public List<PlantGasEntry> ExudeGasses = new();

    // Мутации
    public List<string> Mutations = new();

    // Что залито в почвенный раствор прямо сейчас
    public List<PlantChemicalEntry> SoilReagents = new();
}