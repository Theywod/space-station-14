// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

using Content.Server.Radio.EntitySystems;
using Content.Shared.Radio.Components;
using Content.Shared.SS220.Ipc;
using Robust.Shared.Containers;

namespace Content.Server.SS220.Ipc;

/// <summary>
///     Adapts an IPC radio module inserted into the radio slot to the
///     regular headset system. Container insertion does not raise the
///     inventory equip events, so the headset has to be marked as
///     equipped manually. The module is switched off while the IPC has
///     no charged battery.
/// </summary>
public sealed partial class IpcRadioModuleSystem : EntitySystem
{
    private const string RadioSlotId = "radio_slot";

    [Dependency] private HeadsetSystem _headset = default!;
    [Dependency] private IpcPowerSystem _ipcPower = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IpcComponent, EntInsertedIntoContainerMessage>(OnModuleInserted);
        SubscribeLocalEvent<IpcComponent, EntRemovedFromContainerMessage>(OnModuleRemoved);
    }

    /// <summary>
    ///     Switches the radio module on or off depending on the IPC power state.
    /// </summary>
    /// <remarks>
    ///     Called from <see cref="IpcSystem"/>, which owns the battery events:
    ///     the event bus allows only one subscription per component and event pair.
    /// </remarks>
    public void UpdateModulePowerState(Entity<IpcComponent> ent)
    {
        if (!_container.TryGetContainer(ent.Owner, RadioSlotId, out var container))
            return;

        var powered = _ipcPower.HasEnoughPower(ent);

        foreach (var module in container.ContainedEntities)
        {
            if (TryComp<HeadsetComponent>(module, out var headset))
                _headset.SetEnabled(module, powered, headset);
        }
    }

    private void OnModuleInserted(Entity<IpcComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != RadioSlotId)
            return;

        EquipModule(ent, args.Entity);
    }

    private void OnModuleRemoved(Entity<IpcComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != RadioSlotId)
            return;

        UnequipModule(ent.Owner, args.Entity);
    }

    private void UnequipModule(EntityUid ipc, EntityUid module)
    {
        if (!TryComp<HeadsetComponent>(module, out var headset))
            return;

        headset.IsEquipped = false;
        Dirty(module, headset);
        _headset.SetEnabled(module, false, headset);

        if (TryComp<WearingHeadsetComponent>(ipc, out var wearing)
            && wearing.Headset == module)
        {
            RemComp<WearingHeadsetComponent>(ipc);
        }
    }

    private void EquipModule(Entity<IpcComponent> ipc, EntityUid module)
    {
        if (!TryComp<HeadsetComponent>(module, out var headset))
            return;

        headset.IsEquipped = true;
        Dirty(module, headset);
        _headset.SetEnabled(module, _ipcPower.HasEnoughPower(ipc), headset);
    }
}
