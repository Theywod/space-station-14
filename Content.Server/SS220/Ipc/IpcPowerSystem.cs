// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.SS220.Ipc;

namespace Content.Server.SS220.Ipc;

/// <summary>
///     Shared power checks for IPC systems.
/// </summary>
public sealed class IpcPowerSystem : EntitySystem
{
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedBatterySystem _battery = default!;

    public bool TryGetBattery(EntityUid ipc, out Entity<BatteryComponent> battery)
    {
        if (!_powerCell.TryGetBatteryFromSlot(ipc, out var batteryEntity))
        {
            battery = default;
            return false;
        }

        battery = batteryEntity.Value;
        return true;
    }

    /// <summary>
    ///     Checks if the IPC battery has a charge below the critical threshold
    ///     <see cref="IpcComponent.CritCharge"/>.
    /// </summary>
    public bool HasCriticalCharge(Entity<IpcComponent> ent)
    {
        return TryGetBattery(ent.Owner, out var battery)
            && _battery.GetCharge(battery.AsNullable()) / battery.Comp.MaxCharge <= ent.Comp.CritCharge;
    }
}
