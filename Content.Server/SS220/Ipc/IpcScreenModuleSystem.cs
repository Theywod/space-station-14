// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

using Content.Shared.Actions;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.MagicMirror;
using Content.Shared.Popups;
using Content.Shared.SS220.Ipc;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Server.SS220.Ipc;

/// <summary>
///     Adds the screen change action to an IPC when a screen module is
///     inserted into the modules container and removes it when the module is
///     taken out. The action opens the Magic Mirror UI of the module.
/// </summary>
public sealed partial class IpcScreenModuleSystem : EntitySystem
{
    private static readonly LocId IpcNoPower = "ipc-no-power";
    private static readonly ProtoId<OrganCategoryPrototype> Head = "Head";

    [Dependency] private SharedActionsSystem _action = default!;
    [Dependency] private IpcPowerSystem _ipcPower = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IpcModuleSlotSystem _slots = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MagicMirrorComponent, IpcModuleInsertedEvent>(OnModuleInserted);
        SubscribeLocalEvent<MagicMirrorComponent, IpcModuleRemovedEvent>(OnModuleRemoved);
        SubscribeLocalEvent<IpcComponent, OpenIpcFaceActionEvent>(OnOpenIpcFaceAction);
    }

    private void OnModuleInserted(Entity<MagicMirrorComponent> ent, ref IpcModuleInsertedEvent args)
    {
        var ipc = args.Ipc;
        _action.AddAction(ipc.Owner, ref ipc.Comp.ChangeFaceActionEntity, ipc.Comp.ChangeFaceAction);
        Dirty(ipc);
    }

    private void OnModuleRemoved(Entity<MagicMirrorComponent> ent, ref IpcModuleRemovedEvent args)
    {
        var ipc = args.Ipc;
        _action.RemoveAction(ipc.Owner, ipc.Comp.ChangeFaceActionEntity);
        Dirty(ipc);

        // Without the module the IPC screen is cleared, as if no screen was selected.
        if (TerminatingOrDeleted(ipc.Owner))
            return;

        _visualBody.ApplyMarkings(ipc.Owner, new()
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

        if (!_ipcPower.HasEnoughPower(ent))
        {
            _popup.PopupEntity(Loc.GetString(IpcNoPower), ent, ent);
            return;
        }

        if (!_slots.TryGetModule<MagicMirrorComponent>(ent.Owner, out var moduleUid))
            return;

        // Raise event on the module. MagicMirrorSystem calls UpdateInterface().
        var ev = new BeforeActivatableUIOpenEvent(args.Performer);
        RaiseLocalEvent(moduleUid, ev);

        args.Handled = _ui.TryOpenUi(moduleUid, MagicMirrorUiKey.Key, args.Performer);
    }
}
