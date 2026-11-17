// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

namespace Content.Shared.SS220.Ipc;

/// <summary>
///     Raised on the module entity when it is inserted into the IPC modules container.
/// </summary>
[ByRefEvent]
public record struct IpcModuleInsertedEvent(Entity<IpcComponent> Ipc);

/// <summary>
///     Raised on the module entity when it is removed from the IPC modules container.
/// </summary>
[ByRefEvent]
public record struct IpcModuleRemovedEvent(Entity<IpcComponent> Ipc);

/// <summary>
///     Raised on the IPC and on the module when a module is about to be
///     inserted into the IPC modules container. Subscribers can cancel the
///     insertion and provide a reason to show to the user.
/// </summary>
[ByRefEvent]
public record struct IpcModuleInsertAttemptEvent(Entity<IpcComponent> Ipc, EntityUid Module, bool Cancelled = false, string? Reason = null);
