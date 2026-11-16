// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

using Content.Shared.Actions;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.MagicMirror;
using Content.Shared.Popups;
using Content.Shared.SS220.Ipc;
using Content.Shared.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.SS220.Ipc;

/// <summary>
///     Adds the screen change action to an IPC when a screen module is
///     inserted into the screen slot and removes it when the module is
///     taken out. The action opens the Magic Mirror UI of the module.
/// </summary>
public sealed partial class IpcScreenModuleSystem : EntitySystem
{
    private const string ScreenSlotId = "screen_slot";

    private static readonly LocId IpcNoPower = "ipc-no-power";
    private static readonly ProtoId<OrganCategoryPrototype> Head = "Head";

    [Dependency] private SharedActionsSystem _action = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private IpcPowerSystem _ipcPower = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MagicMirrorComponent, EntGotInsertedIntoContainerMessage>(OnModuleInserted);
        SubscribeLocalEvent<MagicMirrorComponent, EntGotRemovedFromContainerMessage>(OnModuleRemoved);
        SubscribeLocalEvent<IpcComponent, OpenIpcFaceActionEvent>(OnOpenIpcFaceAction);
    }

    private void OnModuleInserted(Entity<MagicMirrorComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ScreenSlotId)
            return;

        if (!TryComp<IpcComponent>(args.Container.Owner, out var ipc))
            return;

        _action.AddAction(args.Container.Owner, ref ipc.ChangeFaceActionEntity, ipc.ChangeFaceAction);
        Dirty(args.Container.Owner, ipc);
    }

    private void OnModuleRemoved(Entity<MagicMirrorComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ScreenSlotId)
            return;

        if (!TryComp<IpcComponent>(args.Container.Owner, out var ipc))
            return;

        _action.RemoveAction(args.Container.Owner, ipc.ChangeFaceActionEntity);
        Dirty(args.Container.Owner, ipc);

        // Without the module the IPC screen is cleared, as if no screen was selected.
        if (TerminatingOrDeleted(args.Container.Owner))
            return;

        _visualBody.ApplyMarkings(args.Container.Owner, new()
        {
            [Head] = new()
            {
                [HumanoidVisualLayers.Snout] = new()
            }
        });
    }

    private void OnOpenIpcFaceAction(Entity<IpcComponent> ent, ref OpenIpcFaceActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_container.TryGetContainer(ent.Owner, ScreenSlotId, out var container)
            || container.ContainedEntities.Count == 0)
            return;

        if (!_ipcPower.HasEnoughPower(ent))
        {
            _popup.PopupEntity(Loc.GetString(IpcNoPower), ent, ent);
            return;
        }

        var module = container.ContainedEntities[0];

        // Raise event on the module. MagicMirrorSystem calls UpdateInterface().
        var ev = new BeforeActivatableUIOpenEvent(args.Performer);
        RaiseLocalEvent(module, ev);

        args.Handled = _ui.TryOpenUi(module, MagicMirrorUiKey.Key, args.Performer);
    }
}
