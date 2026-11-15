// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.SS220.Ipc;

namespace Content.Server.SS220.Ipc;

/// <summary>
///     Power checks shared by IPC systems.
/// </summary>
public sealed partial class IpcPowerSystem : EntitySystem
{
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedBatterySystem _battery = default!;

    /// <summary>
    ///     Checks if the IPC battery has enough charge to power its systems.
    ///     An IPC without a battery counts as having no power.
    /// </summary>
    public bool HasEnoughPower(Entity<IpcComponent> ent)
    {
        if (!_powerCell.TryGetBatteryFromSlot(ent.Owner, out var battery))
            return false;

        return _battery.GetChargeLevel(battery.Value.AsNullable()) > ent.Comp.CritCharge;
    }
}
