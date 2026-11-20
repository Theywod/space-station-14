// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.SS220.Ipc;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.SS220.Ipc;

/// <summary>
///     Deploys the items of an IPC item module into dedicated virtual hands
///     of the IPC for a battery cost, and folds them back into the module
///     when toggled off or when the module is removed.
/// </summary>
public sealed partial class IpcItemModuleSystem : EntitySystem
{
    private const string HoldingContainerId = "holding_container";
    private static readonly EntProtoId ToggleAction = "ActionIpcToggleItemModule";
    private static readonly LocId IpcNoPower = "ipc-no-power";

    [Dependency] private SharedActionsSystem _action = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IpcPowerSystem _ipcPower = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IpcItemModuleComponent, IpcModuleInsertedEvent>(OnModuleInserted);
        SubscribeLocalEvent<IpcItemModuleComponent, IpcModuleRemovedEvent>(OnModuleRemoved);
        SubscribeLocalEvent<IpcComponent, ToggleIpcItemModuleActionEvent>(OnToggleAction);
    }

    private void OnModuleInserted(Entity<IpcItemModuleComponent> ent, ref IpcModuleInsertedEvent args)
    {
        var ipc = args.Ipc;
        _action.AddAction(ipc.Owner, ref ent.Comp.ActionEntity, ToggleAction);
        _action.SetIcon(ent.Comp.ActionEntity!.Value, ent.Comp.ActionIcon);
        _action.SetIconOn(ent.Comp.ActionEntity!.Value, ent.Comp.ActionIconOn);
        RaisePassiveDrawChanged(ipc.Owner);
    }

    private void OnModuleRemoved(Entity<IpcItemModuleComponent> ent, ref IpcModuleRemovedEvent args)
    {
        var ipc = args.Ipc;

        if (!TerminatingOrDeleted(ipc.Owner) && ent.Comp.Deployed)
            FoldItems(ipc.Owner, ent);

        _action.RemoveAction(ipc.Owner, ent.Comp.ActionEntity);
        RaisePassiveDrawChanged(ipc.Owner);
    }

    private void OnToggleAction(Entity<IpcComponent> ent, ref ToggleIpcItemModuleActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryGetModule(ent.Owner, args.Action, out var module))
            return;

        args.Handled = true;

        if (module.Comp.Deployed)
            FoldItems(ent.Owner, module);
        else
            DeployItems(ent.Owner, module);
    }

    private bool TryGetModule(EntityUid ipc, Entity<ActionComponent> action, out Entity<IpcItemModuleComponent> module)
    {
        module = default;

        if (!_container.TryGetContainer(ipc, IpcModuleSlotSystem.ModulesContainerId, out var container))
            return false;

        foreach (var contained in container.ContainedEntities)
        {
            if (TryComp<IpcItemModuleComponent>(contained, out var comp) && comp.ActionEntity == action.Owner)
            {
                module = (contained, comp);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Total passive draw of all item modules installed into the IPC:
    ///     the folded draw of every module plus the deployed draw of the
    ///     deployed ones.
    /// </summary>
    public float GetTotalPassiveDraw(EntityUid ipc)
    {
        if (!_container.TryGetContainer(ipc, IpcModuleSlotSystem.ModulesContainerId, out var container))
            return 0f;

        var total = 0f;

        foreach (var contained in container.ContainedEntities)
        {
            if (!TryComp<IpcItemModuleComponent>(contained, out var comp))
                continue;

            total += comp.PassiveDrawFolded;

            if (comp.Deployed)
                total += comp.PassiveDrawDeployed;
        }

        return total;
    }

    /// <summary>
    ///     Folds the deployed modules flagged with
    ///     <see cref="IpcItemModuleComponent.FoldOnDischarge"/> when the IPC
    ///     battery runs out of charge.
    /// </summary>
    /// <remarks>
    ///     Called from <see cref="IpcSystem"/>, which owns the battery
    ///     events: the event bus allows only one subscription per component
    ///     and event pair.
    /// </remarks>
    public void UpdateModulePowerState(Entity<IpcComponent> ent)
    {
        if (_ipcPower.HasEnoughPower(ent))
            return;

        if (!_container.TryGetContainer(ent.Owner, IpcModuleSlotSystem.ModulesContainerId, out var container))
            return;

        foreach (var contained in container.ContainedEntities)
        {
            if (TryComp<IpcItemModuleComponent>(contained, out var comp) && comp.FoldOnDischarge && comp.Deployed)
                FoldItems(ent.Owner, (contained, comp));
        }
    }

    private void DeployItems(EntityUid ipcUid, Entity<IpcItemModuleComponent> module)
    {
        var cost = module.Comp.ActivationCost + module.Comp.CostPerItem * module.Comp.Items.Count;

        if (!_powerCell.TryGetBatteryFromSlot(ipcUid, out var battery)
            || _battery.GetCharge(battery.Value.AsNullable()) < cost)
        {
            _popup.PopupEntity(Loc.GetString(IpcNoPower), ipcUid, ipcUid);
            return;
        }

        if (!_container.TryGetContainer(module.Owner, HoldingContainerId, out var container)
            || !TryComp<HandsComponent>(ipcUid, out var hands))
        {
            return;
        }

        if (!_battery.TryUseCharge(battery.Value.AsNullable(), cost))
            return;

        var coordinates = Transform(ipcUid).Coordinates;

        for (var i = 0; i < module.Comp.Items.Count; i++)
        {
            var handId = $"{GetNetEntity(module.Owner)}-hand-{i}";

            _hands.AddHand((ipcUid, hands), handId, HandLocation.Middle);

            if (module.Comp.StoredItems.TryGetValue(handId, out var stored) && !TerminatingOrDeleted(stored))
            {
                // The item is inside the module's holding container. Eject it
                // to the map first so that the client sees the same state flow
                // as the first deployment: a direct container-to-container move
                // on redeploy makes the in-hand sprite disappear.
                _container.RemoveEntity(module.Owner, stored, destination: coordinates);

                if (_hands.TryPickup(ipcUid, stored, handId, false))
                {
                    EnsureComp<UnremoveableComponent>(stored);
                    module.Comp.StoredItems.Remove(handId);
                }
                else
                {
                    // Put the item back into the module's holding container so
                    // that it is not lost on the ground.
                    _container.Insert(stored, container);
                }

                continue;
            }

            module.Comp.StoredItems.Remove(handId);

            var item = Spawn(module.Comp.Items[i], coordinates);

            if (_hands.TryPickup(ipcUid, item, handId, false))
                EnsureComp<UnremoveableComponent>(item);
            else
                QueueDel(item);
        }

        module.Comp.Deployed = true;
        _action.SetToggled(module.Comp.ActionEntity, true);
        RaisePassiveDrawChanged(ipcUid);
    }

    private void FoldItems(EntityUid ipcUid, Entity<IpcItemModuleComponent> module)
    {
        if (TerminatingOrDeleted(module.Owner))
            return;

        if (!_container.TryGetContainer(module.Owner, HoldingContainerId, out var container)
            || !TryComp<HandsComponent>(ipcUid, out var hands))
        {
            return;
        }

        for (var i = 0; i < module.Comp.Items.Count; i++)
        {
            var handId = $"{GetNetEntity(module.Owner)}-hand-{i}";

            if (_hands.TryGetHeldItem((ipcUid, hands), handId, out var held))
            {
                RemComp<UnremoveableComponent>(held.Value);
                _container.Insert(held.Value, container);
                module.Comp.StoredItems[handId] = held.Value;
            }
            else
            {
                module.Comp.StoredItems.Remove(handId);
            }

            _hands.RemoveHand((ipcUid, hands), handId);
        }

        module.Comp.Deployed = false;
        _action.SetToggled(module.Comp.ActionEntity, false);
        RaisePassiveDrawChanged(ipcUid);
    }

    private void RaisePassiveDrawChanged(EntityUid ipc)
    {
        var ev = new IpcPassiveDrawChangedEvent();
        RaiseLocalEvent(ipc, ref ev);
    }
}
