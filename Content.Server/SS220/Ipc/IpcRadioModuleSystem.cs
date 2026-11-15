// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

using Content.Server.Radio;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Radio.Components;
using Content.Shared.SS220.Ipc;
using Robust.Shared.Containers;

namespace Content.Server.SS220.Ipc;

/// <summary>
///     Adapts an IPC radio module inserted into the radio slot to the
///     regular headset system. Container insertion does not raise the
///     inventory equip events, so the headset has to be marked as
///     equipped manually.
/// </summary>
public sealed class IpcRadioModuleSystem : EntitySystem
{
    private const string RadioSlotId = "radio_slot";

    [Dependency] private readonly HeadsetSystem _headset = default!;
    [Dependency] private readonly IpcPowerSystem _ipcPower = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IpcComponent, EntInsertedIntoContainerMessage>(OnModuleInserted);
        SubscribeLocalEvent<IpcComponent, EntRemovedFromContainerMessage>(OnModuleRemoved);
        SubscribeLocalEvent<IpcComponent, RadioSendAttemptEvent>(OnRadioSendAttempt);
        SubscribeLocalEvent<IpcComponent, RadioReceiveAttemptEvent>(OnRadioReceiveAttempt);
    }

    private void OnModuleInserted(Entity<IpcComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != RadioSlotId)
            return;

        EquipModule(ent.Owner, args.Entity);
    }

    private void OnModuleRemoved(Entity<IpcComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != RadioSlotId)
            return;

        UnequipModule(ent.Owner, args.Entity);
    }

    private void EquipModule(EntityUid ipc, EntityUid module)
    {
        if (!TryComp<HeadsetComponent>(module, out var headset))
            return;

        headset.IsEquipped = true;
        Dirty(module, headset);
        _headset.SetEnabled(module, true, headset);
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

    /// <summary>
    ///     IPC radio stops working when the battery is low.
    /// </summary>
    private void OnRadioSendAttempt(Entity<IpcComponent> ent, ref RadioSendAttemptEvent args)
    {
        if (args.RadioSource != ent.Owner
            && Transform(args.RadioSource).ParentUid != ent.Owner)
            return;

        if (!_ipcPower.HasCriticalCharge(ent))
            return;

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("ipc-no-power"), ent, ent);
    }

    /// <summary>
    ///     IPC does not receive radio messages if the battery is low.
    /// </summary>
    private void OnRadioReceiveAttempt(Entity<IpcComponent> ent, ref RadioReceiveAttemptEvent args)
    {
        if (args.RadioReceiver != ent.Owner
            && Transform(args.RadioReceiver).ParentUid != ent.Owner)
            return;

        if (_ipcPower.HasCriticalCharge(ent))
            args.Cancelled = true;
    }
}
