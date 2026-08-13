using System.Linq;
using Content.Server.Botany.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.SS220.PlantAnalyzer;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;
using Robust.Server.GameObjects;
using Content.Shared.Chemistry.Reagent;

namespace Content.Server.SS220.PlantAnalyzer;

public sealed class PlantAnalyzerSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<PlantAnalyzerComponent, PlantAnalyzerDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<PlantAnalyzerComponent, PlantAnalyzerPrintMessage>(OnPrint);
    }

    private void OnAfterInteract(Entity<PlantAnalyzerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target || !args.CanReach)
            return;

        if (!HasComp<PlantHolderComponent>(target))
            return;

        args.Handled = true;
        _audio.PlayPvs(ent.Comp.ScanningBeginSound, ent);

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.ScanDelay,
            new PlantAnalyzerDoAfterEvent(), ent, target: target, used: ent)
        {
            NeedHand = true,
            BreakOnMove = true,
        });
    }

    private void OnDoAfter(Entity<PlantAnalyzerComponent> ent, ref PlantAnalyzerDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        if (!ent.Comp.Silent)
            _audio.PlayPvs(ent.Comp.ScanningEndSound, ent);

        ent.Comp.ScannedEntity = target;

        if (!_ui.HasUi(ent, PlantAnalyzerUiKey.Key))
            return;

        _ui.OpenUi(ent.Owner, PlantAnalyzerUiKey.Key, args.User);
        SendScanUpdate(ent, target);

        args.Handled = true;
    }

    private void SendScanUpdate(Entity<PlantAnalyzerComponent> ent, EntityUid target)
    {
        var state = BuildState(ent, target);
        ent.Comp.LastScannedName = state.SeedDisplayName ?? Loc.GetString("plant-analyzer-window-no-plant-text");
        ent.Comp.LastScannedReport = BuildReport(state);

        _ui.ServerSendUiMessage(ent.Owner, PlantAnalyzerUiKey.Key, new PlantAnalyzerScannedUserMessage(state));
    }

    private PlantAnalyzerUiState BuildState(Entity<PlantAnalyzerComponent> ent, EntityUid target)
    {
        var state = new PlantAnalyzerUiState
        {
            TargetEntity = GetNetEntity(target),
            ScanMode = true,
            CanPrint = ent.Comp.CanPrint,
        };

        if (!TryComp<PlantHolderComponent>(target, out var holder))
            return state;

        state.WaterLevel = holder.WaterLevel;
        state.NutritionLevel = holder.NutritionLevel;
        state.Toxins = holder.Toxins;
        state.PestLevel = holder.PestLevel;
        state.WeedLevel = holder.WeedLevel;

        // Что залито в почвенный раствор
        if (holder.SoilSolution is { } soilSoln)
        {
            foreach (var (reagent, quantity) in soilSoln.Comp.Solution.Contents)
            {
                if (!_prototype.TryIndex<ReagentPrototype>(reagent.Prototype, out var reagentProto))
                    continue;

                state.SoilReagents.Add(new PlantChemicalEntry(reagent.Prototype, reagentProto.LocalizedName, (float)quantity, (float)quantity, true));
            }
        }

        var seed = holder.Seed;
        if (seed == null)
        {
            state.HasSeed = false;
            return state;
        }

        state.HasSeed = true;
        state.Dead = holder.Dead;
        state.Mutating = holder.MutationLevel > 0;
        state.SeedDisplayName = Loc.GetString(seed.DisplayName);
        state.Health = holder.Health;
        state.MaxHealth = seed.Endurance;
        state.Age = holder.Age;
        state.Lifespan = seed.Lifespan;
        state.Maturation = seed.Maturation;
        state.Production = seed.Production;

        state.IdealHeat = seed.IdealHeat;
        state.HeatTolerance = seed.HeatTolerance;
        state.IdealLight = seed.IdealLight;
        state.LightTolerance = seed.LightTolerance;
        state.LowPressureTolerance = seed.LowPressureTolerance;
        state.HighPressureTolerance = seed.HighPressureTolerance;
        state.ToxinsTolerance = seed.ToxinsTolerance;
        state.PestTolerance = seed.PestTolerance;
        state.WeedTolerance = seed.WeedTolerance;

        state.Yield = seed.Yield;
        state.Potency = seed.Potency;
        state.Seedless = seed.Seedless;
        state.Harvest = holder.Harvest;

        foreach (var (chemId, chemData) in seed.Chemicals)
        {
            var name = _prototype.TryIndex<ReagentPrototype>(chemId, out var reagentProto)
                ? reagentProto.LocalizedName
                : chemId;
            state.Chemicals.Add(new PlantChemicalEntry(chemId, name, (float)chemData.Min, (float)chemData.Max, chemData.Inherent));
        }

        foreach (var (gas, amount) in seed.ConsumeGasses)
            state.ConsumeGasses.Add(new PlantGasEntry(gas, amount, true));

        foreach (var (gas, amount) in seed.ExudeGasses)
            state.ExudeGasses.Add(new PlantGasEntry(gas, amount, false));

        state.Mutations = seed.Mutations.Select(m => Loc.GetString(m.Name)).ToList();

        return state;
    }

    private string BuildReport(PlantAnalyzerUiState state)
    {
        var b = new System.Text.StringBuilder();
        b.AppendLine(Loc.GetString("plant-analyzer-report-header"));
        b.AppendLine();

        if (!state.HasSeed)
        {
            b.AppendLine(Loc.GetString("plant-analyzer-window-no-plant-text"));
            return b.ToString();
        }

        b.AppendLine(Loc.GetString("plant-analyzer-report-name", ("name", state.SeedDisplayName!)));
        b.AppendLine(Loc.GetString("plant-analyzer-report-status",
            ("value", state.Dead ? Loc.GetString("plant-analyzer-status-dead")
                : state.Mutating ? Loc.GetString("plant-analyzer-status-mutating")
                : Loc.GetString("plant-analyzer-status-alive"))));
        b.AppendLine(Loc.GetString("plant-analyzer-report-health", ("value", state.Health), ("max", state.MaxHealth)));
        b.AppendLine();

        b.AppendLine(Loc.GetString("plant-analyzer-report-section-tray"));
        b.AppendLine(Loc.GetString("plant-analyzer-report-water", ("value", state.WaterLevel)));
        b.AppendLine(Loc.GetString("plant-analyzer-report-nutrition", ("value", state.NutritionLevel)));
        b.AppendLine(Loc.GetString("plant-analyzer-report-toxins", ("value", state.Toxins)));
        b.AppendLine(Loc.GetString("plant-analyzer-report-pests", ("value", state.PestLevel)));
        b.AppendLine(Loc.GetString("plant-analyzer-report-weeds", ("value", state.WeedLevel)));
        b.AppendLine();

        if (state.SoilReagents.Count > 0)
        {
            b.AppendLine(Loc.GetString("plant-analyzer-report-section-soil-reagents"));
            foreach (var r in state.SoilReagents)
                b.AppendLine($" · {r.Name}: {r.Min:F1}u");
            b.AppendLine();
        }

        b.AppendLine(Loc.GetString("plant-analyzer-report-section-requirements"));
        b.AppendLine(Loc.GetString("plant-analyzer-report-heat", ("value", state.IdealHeat), ("tolerance", state.HeatTolerance)));
        b.AppendLine(Loc.GetString("plant-analyzer-report-light", ("value", state.IdealLight), ("tolerance", state.LightTolerance)));
        b.AppendLine(Loc.GetString("plant-analyzer-report-pressure", ("min", state.LowPressureTolerance), ("max", state.HighPressureTolerance)));
        b.AppendLine();

        b.AppendLine(Loc.GetString("plant-analyzer-report-section-yield"));
        b.AppendLine(Loc.GetString("plant-analyzer-report-yield", ("value", state.Yield)));
        b.AppendLine(Loc.GetString("plant-analyzer-report-potency", ("value", state.Potency)));
        b.AppendLine(Loc.GetString("plant-analyzer-report-production-cycle", ("value", state.Production)));
        b.AppendLine();

        if (state.Chemicals.Count > 0)
        {
            b.AppendLine(Loc.GetString("plant-analyzer-report-section-chemicals"));
            foreach (var c in state.Chemicals)
                b.AppendLine($" · {c.Name}: {c.Min:F1}-{c.Max:F1}u");
            b.AppendLine();
        }

        if (state.ConsumeGasses.Count > 0 || state.ExudeGasses.Count > 0)
        {
            b.AppendLine(Loc.GetString("plant-analyzer-report-section-gasses"));
            foreach (var g in state.ConsumeGasses)
                b.AppendLine($" · {Loc.GetString("plant-analyzer-report-gas-consume", ("gas", g.Gas), ("amount", g.Amount))}");
            foreach (var g in state.ExudeGasses)
                b.AppendLine($" · {Loc.GetString("plant-analyzer-report-gas-exude", ("gas", g.Gas), ("amount", g.Amount))}");
            b.AppendLine();
        }

        b.AppendLine(Loc.GetString("plant-analyzer-report-section-mutations"));
        if (state.Mutations.Count == 0)
            b.AppendLine(Loc.GetString("plant-analyzer-report-none"));
        else
            foreach (var m in state.Mutations)
                b.AppendLine($" · {m}");

        return b.ToString();
    }

    private void OnPrint(Entity<PlantAnalyzerComponent> ent, ref PlantAnalyzerPrintMessage args)
    {
        if (!ent.Comp.CanPrint)
            return;

        if (_timing.CurTime < ent.Comp.PrintReadyAt)
        {
            _popup.PopupEntity(Loc.GetString("plant-analyzer-printer-not-ready"), ent, args.Actor);
            return;
        }

        if (string.IsNullOrWhiteSpace(ent.Comp.LastScannedReport))
        {
            _popup.PopupEntity(Loc.GetString("plant-analyzer-printer-no-data"), ent, args.Actor);
            return;
        }

        var printed = Spawn(ent.Comp.MachineOutput, Transform(ent).Coordinates);
        _hands.PickupOrDrop(args.Actor, printed, checkActionBlocker: false);

        if (TryComp<PaperComponent>(printed, out var paper))
        {
            _metaData.SetEntityName(printed, Loc.GetString("plant-analyzer-report-title", ("plant", ent.Comp.LastScannedName)));
            _paper.SetContent((printed, paper), ent.Comp.LastScannedReport);
        }

        ent.Comp.PrintReadyAt = _timing.CurTime + ent.Comp.PrintCooldown;
    }
}