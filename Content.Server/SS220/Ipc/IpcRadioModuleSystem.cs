// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

using Content.Server.Radio.EntitySystems;
using Content.Shared.Radio.Components;
using Content.Shared.SS220.Ipc;

namespace Content.Server.SS220.Ipc;

/// <summary>
///     Adapts an IPC radio module inserted into the modules container to the
///     regular headset system. Container insertion does not raise the
///     inventory equip events, so the headset has to be marked as
///     equipped manually. The module is switched off while the IPC has
///     no charged battery.
/// </summary>
public sealed partial class IpcRadioModuleSystem : EntitySystem
{
    [Dependency] private HeadsetSystem _headset = default!;
    [Dependency] private IpcPowerSystem _ipcPower = default!;
    [Dependency] private IpcModuleSlotSystem _slots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeadsetComponent, IpcModuleInsertedEvent>(OnModuleInserted);
        SubscribeLocalEvent<HeadsetComponent, IpcModuleRemovedEvent>(OnModuleRemoved);
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
        if (!_slots.TryGetModule<HeadsetComponent>(ent.Owner, out var module))
            return;

        var powered = _ipcPower.HasEnoughPower(ent);
        _headset.SetEnabled(module, powered);
    }

    private void OnModuleInserted(Entity<HeadsetComponent> ent, ref IpcModuleInsertedEvent args)
    {
        EquipModule(args.Ipc, ent.Owner);
    }

    private void OnModuleRemoved(Entity<HeadsetComponent> ent, ref IpcModuleRemovedEvent args)
    {
        UnequipModule(ent.Owner);
    }

    private void UnequipModule(EntityUid module)
    {
        if (!TryComp<HeadsetComponent>(module, out var headset))
            return;

        // SetEnabled(false) removes WearingHeadsetComponent from the parent
        // while the headset is still marked as equipped.
        _headset.SetEnabled(module, false, headset);
        headset.IsEquipped = false;
        Dirty(module, headset);
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
