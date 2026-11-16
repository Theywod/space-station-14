// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

using Content.Shared.Actions;
using Content.Shared.Ninja.Components;
using Content.Shared.Ninja.Systems;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.SS220.Ipc;
using Robust.Shared.Containers;

namespace Content.Server.SS220.Ipc;

/// <summary>
///     Grants the IPC the drain battery action while the drain module is
///     inserted into the modules container and processes its toggling.
/// </summary>
public sealed partial class IpcDrainModuleSystem : EntitySystem
{
    private static readonly LocId IpcDrainReady = "ipc-drain-enabled";
    private static readonly LocId IpcDrainDisabled = "ipc-drain-disabled";
    private static readonly LocId IpcNoBattery = "ipc-no-battery";

    [Dependency] private SharedActionsSystem _action = default!;
    [Dependency] private SharedBatteryDrainerSystem _batteryDrainer = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IpcDrainModuleComponent, EntGotInsertedIntoContainerMessage>(OnModuleInserted);
        SubscribeLocalEvent<IpcDrainModuleComponent, EntGotRemovedFromContainerMessage>(OnModuleRemoved);
        SubscribeLocalEvent<IpcComponent, ToggleDrainActionEvent>(OnToggleDrainAction);
    }

    private void OnModuleInserted(Entity<IpcDrainModuleComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != IpcModuleSlotSystem.ModulesContainerId)
            return;

        if (!TryComp<IpcComponent>(args.Container.Owner, out var ipc))
            return;

        _action.AddAction(args.Container.Owner, ref ipc.DrainBatteryActionEntity, ipc.DrainBatteryAction);
        Dirty(args.Container.Owner, ipc);
    }

    private void OnModuleRemoved(Entity<IpcDrainModuleComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (args.Container.ID != IpcModuleSlotSystem.ModulesContainerId)
            return;

        if (TerminatingOrDeleted(args.Container.Owner))
            return;

        if (!TryComp<IpcComponent>(args.Container.Owner, out var ipc))
            return;

        // Removing the module with an active drain disables it.
        if (ipc.DrainActivated)
        {
            ipc.DrainActivated = false;
            _action.SetToggled(ipc.DrainBatteryActionEntity, false);
            RemComp<BatteryDrainerComponent>(args.Container.Owner);
            _popup.PopupEntity(Loc.GetString(IpcDrainDisabled), args.Container.Owner, args.Container.Owner);
        }

        _action.RemoveAction(args.Container.Owner, ipc.DrainBatteryActionEntity);
        Dirty(args.Container.Owner, ipc);
    }

    /// <summary>
    ///     Enable/disable the drain ability.
    /// </summary>
    private void OnToggleDrainAction(Entity<IpcComponent> ent, ref ToggleDrainActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_powerCell.HasBattery(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString(IpcNoBattery), ent, ent);
            return;
        }

        ent.Comp.DrainActivated = !ent.Comp.DrainActivated;
        _action.SetToggled(ent.Comp.DrainBatteryActionEntity, ent.Comp.DrainActivated);
        args.Handled = true;

        if (ent.Comp.DrainActivated && _powerCell.TryGetBatteryFromSlot(ent.Owner, out var battery))
        {
            EnsureComp<BatteryDrainerComponent>(ent);
            _batteryDrainer.SetBattery(ent.Owner, battery);
        }
        else
            RemComp<BatteryDrainerComponent>(ent);

        var message = ent.Comp.DrainActivated ? IpcDrainReady : IpcDrainDisabled;
        _popup.PopupEntity(Loc.GetString(message), ent, ent);

        Dirty(ent);
    }

    /// <summary>
    ///     Forcibly disables the drain when the IPC battery is removed.
    /// </summary>
    /// <remarks>
    ///     Called from <see cref="IpcSystem"/>, which owns the battery events:
    ///     the event bus allows only one subscription per component and event pair.
    /// </remarks>
    public void DisableDrain(Entity<IpcComponent> ent)
    {
        ent.Comp.DrainActivated = false;
        _action.SetToggled(ent.Comp.DrainBatteryActionEntity, ent.Comp.DrainActivated);

        // Show the popup only if the drainer was removed.
        if (RemComp<BatteryDrainerComponent>(ent))
            _popup.PopupEntity(Loc.GetString(IpcDrainDisabled), ent, ent);

        Dirty(ent);
    }
}
