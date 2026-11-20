// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.SS220.Ipc;

/// <summary>
///     Item module of the IPC: deploys a set of items into dedicated virtual
///     hands of the IPC and folds them back into the module when toggled off.
/// </summary>
[RegisterComponent]
public sealed partial class IpcItemModuleComponent : Component
{
    /// <summary>
    ///     Items provided while the module is deployed.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId> Items = new();

    /// <summary>
    ///     Battery energy charged for each item when the module is deployed.
    /// </summary>
    [DataField]
    public float CostPerItem = 50f;

    /// <summary>
    ///     Battery energy charged once when the module is deployed, on top of
    ///     <see cref="CostPerItem"/> per item. Folding is always free.
    /// </summary>
    [DataField]
    public float ActivationCost;

    /// <summary>
    ///     Passive battery draw added to the IPC draw rate while the module
    ///     is inserted but not deployed.
    /// </summary>
    [DataField]
    public float PassiveDrawFolded;

    /// <summary>
    ///     Passive battery draw added to the IPC draw rate while the module
    ///     is deployed.
    /// </summary>
    [DataField]
    public float PassiveDrawDeployed;

    /// <summary>
    ///     Whether the module folds automatically when the IPC battery
    ///     discharges below the critical threshold.
    /// </summary>
    [DataField]
    public bool FoldOnDischarge;

    /// <summary>
    ///     Icon of the action granted by this module. Overrides the default
    ///     action icon.
    /// </summary>
    [DataField]
    public SpriteSpecifier? ActionIcon;

    /// <summary>
    ///     Toggled-on icon of the action granted by this module.
    /// </summary>
    [DataField]
    public SpriteSpecifier? ActionIconOn;

    /// <summary>
    ///     Whether the items are currently deployed into the IPC's hands.
    /// </summary>
    [DataField]
    public bool Deployed;

    /// <summary>
    ///     Deployed items folded back into the holding container, by virtual hand id.
    /// </summary>
    public Dictionary<string, EntityUid> StoredItems = new();

    /// <summary>
    ///     The action granted to the IPC for this module instance.
    /// </summary>
    [DataField]
    public EntityUid? ActionEntity;
}
