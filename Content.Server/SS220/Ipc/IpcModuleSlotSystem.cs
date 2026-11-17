// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

using System.Diagnostics.CodeAnalysis;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.SS220.Ipc;
using Content.Shared.Tag;
using Content.Shared.Trigger.Components.Effects;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.SS220.Ipc;

/// <summary>
///     Handles inserting modules into the shared IPC modules container
///     and ejecting them via the right-click verb menu.
/// </summary>
public sealed partial class IpcModuleSlotSystem : EntitySystem
{
    public const string ModulesContainerId = "modules";
    private static readonly ProtoId<TagPrototype> IpcModuleTag = new("IpcModule");

    private static readonly LocId IpcModuleEjectVerb = "ipc-module-eject-verb";
    private static readonly LocId IpcModuleFull = "ipc-module-slot-full";
    private static readonly LocId IpcModuleDuplicate = "ipc-module-duplicate";
    private static readonly LocId IpcModuleConflict = "ipc-module-conflict";
    private static readonly LocId IpcModuleDeny = "ipc-module-insert-denied";

    private static readonly SoundSpecifier InsertSound = new SoundPathSpecifier("/Audio/Items/pistol_magin.ogg");
    private static readonly SoundSpecifier EjectSound = new SoundPathSpecifier("/Audio/Items/pistol_magout.ogg");

    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IpcComponent, InteractUsingEvent>(OnInteractUsing, before: [typeof(ItemSlotsSystem)]);
        SubscribeLocalEvent<IpcComponent, GetVerbsEvent<AlternativeVerb>>(AddModuleVerbs);
        SubscribeLocalEvent<IpcComponent, EntInsertedIntoContainerMessage>(OnModuleInserted);
        SubscribeLocalEvent<IpcComponent, EntRemovedFromContainerMessage>(OnModuleRemoved);
    }

    /// <summary>
    ///     Inserts a module into the IPC modules container.
    /// </summary>
    public bool TryInsertModule(Entity<IpcComponent> ipc, EntityUid module)
    {
        if (!_tag.HasTag(module, IpcModuleTag))
            return false;

        if (!_container.TryGetContainer(ipc.Owner, ModulesContainerId, out var container))
            return false;

        if (!CanInsert(ipc.Comp, container, module))
            return false;

        if (!RaiseInsertAttempt(ipc, module, out _))
            return false;

        return _container.Insert(module, container);
    }

    /// <summary>
    ///     Ejects a module from the IPC modules container.
    /// </summary>
    public bool TryEjectModule(Entity<IpcComponent> ipc, EntityUid module, EntityUid? user = null)
    {
        if (!_container.TryGetContainer(ipc.Owner, ModulesContainerId, out var container))
            return false;

        if (!_container.RemoveEntity(ipc.Owner, module))
            return false;

        if (user is { } player)
        {
            _hands.TryPickupAnyHand(player, module);
            _audio.PlayPredicted(EjectSound, ipc.Owner, player);
        }

        return true;
    }

    /// <summary>
    ///     Notifies the module systems that a module was inserted into the
    ///     modules container. The event is raised on the module itself so that
    ///     each system subscribes with its own component and event pair.
    /// </summary>
    private void OnModuleInserted(Entity<IpcComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ModulesContainerId)
            return;

        var ev = new IpcModuleInsertedEvent(ent);
        RaiseLocalEvent(args.Entity, ref ev);
    }

    /// <summary>
    ///     Notifies the module systems that a module was removed from the
    ///     modules container.
    /// </summary>
    private void OnModuleRemoved(Entity<IpcComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ModulesContainerId)
            return;

        var ev = new IpcModuleRemovedEvent(ent);
        RaiseLocalEvent(args.Entity, ref ev);
    }

    private void OnInteractUsing(Entity<IpcComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Everything with the module tag is handled here so that ItemSlotsSystem
        // does not insert it into the battery slot, which has no whitelist.
        if (!_tag.HasTag(args.Used, IpcModuleTag))
            return;

        args.Handled = true;

        if (!_container.TryGetContainer(ent.Owner, ModulesContainerId, out var container))
            return;

        if (container.ContainedEntities.Count >= ent.Comp.ModuleCapacity)
        {
            _popup.PopupClient(Loc.GetString(IpcModuleFull), ent.Owner, args.User);
            return;
        }

        if (HasDuplicate(container, args.Used))
        {
            _popup.PopupClient(Loc.GetString(IpcModuleDuplicate), ent.Owner, args.User);
            return;
        }

        if (HasOverlappingComponents(container, args.Used))
        {
            _popup.PopupClient(Loc.GetString(IpcModuleConflict), ent.Owner, args.User);
            return;
        }

        if (!RaiseInsertAttempt(ent, args.Used, out var reason))
        {
            _popup.PopupClient(Loc.GetString(reason ?? IpcModuleDeny), ent.Owner, args.User);
            return;
        }

        if (!_hands.TryDrop(args.User, args.Used, suppressDropSound: true))
            return;

        _container.Insert(args.Used, container);
        _audio.PlayPredicted(InsertSound, ent.Owner, args.User);
    }

    private void AddModuleVerbs(Entity<IpcComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (args.Hands == null || !args.CanAccess || !args.CanInteract)
            return;

        if (!_container.TryGetContainer(ent.Owner, ModulesContainerId, out var container))
            return;

        var user = args.User;

        foreach (var module in container.ContainedEntities)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString(IpcModuleEjectVerb, ("module", Identity.Name(module, EntityManager))),
                Priority = 10,
                Act = () => TryEjectModule(ent, module, user)
            });
        }
    }

    private bool CanInsert(IpcComponent ipc, BaseContainer container, EntityUid module)
    {
        if (container.ContainedEntities.Count >= ipc.ModuleCapacity)
            return false;

        if (HasDuplicate(container, module))
            return false;

        return !HasOverlappingComponents(container, module);
    }

    /// <summary>
    ///     Raises <see cref="IpcModuleInsertAttemptEvent"/> on the IPC and on
    ///     the module so that other systems can veto the insertion.
    /// </summary>
    private bool RaiseInsertAttempt(Entity<IpcComponent> ipc, EntityUid module, out string? reason)
    {
        var ev = new IpcModuleInsertAttemptEvent(ipc, module);
        RaiseLocalEvent(ipc.Owner, ref ev);
        RaiseLocalEvent(module, ref ev);
        reason = ev.Reason;
        return !ev.Cancelled;
    }

    private bool HasDuplicate(BaseContainer container, EntityUid module)
    {
        var prototype = MetaData(module).EntityPrototype;

        foreach (var existing in container.ContainedEntities)
        {
            if (MetaData(existing).EntityPrototype == prototype)
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Finds the first module with the given component in the IPC modules
    ///     container.
    /// </summary>
    public bool TryGetModule<T>(EntityUid ipc, [NotNullWhen(true)] out EntityUid module)
        where T : IComponent
    {
        if (_container.TryGetContainer(ipc, ModulesContainerId, out var container))
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (HasComp<T>(contained))
                {
                    module = contained;
                    return true;
                }
            }
        }

        module = default;
        return false;
    }

    /// <summary>
    ///     Modules that add the same components to the IPC conflict with each
    ///     other: the second one would overwrite the first one's components.
    /// </summary>
    private bool HasOverlappingComponents(BaseContainer container, EntityUid module)
    {
        if (!TryComp<AddComponentsOnTriggerComponent>(module, out var adding))
            return false;

        foreach (var existing in container.ContainedEntities)
        {
            if (!TryComp<AddComponentsOnTriggerComponent>(existing, out var existingAdding))
                continue;

            foreach (var component in adding.Components.Keys)
            {
                if (existingAdding.Components.ContainsKey(component))
                    return true;
            }
        }

        return false;
    }
}
