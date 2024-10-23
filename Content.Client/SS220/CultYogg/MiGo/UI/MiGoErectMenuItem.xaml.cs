// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt
using System.Text;
using Content.Shared.SS220.CultYogg.Buildings;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.SS220.CultYogg.MiGo.UI;

[GenerateTypedNameReferences]
public sealed partial class MiGoErectMenuItem : ContainerButton
{
    public CultYoggBuildingPrototype? Building { get; private set; }

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private static readonly Color ColorNormal = Color.FromHex("#7b7e9e");
    private static readonly Color ColorHovered = Color.FromHex("#9699bb");
    private static readonly Color ColorPressed = Color.FromHex("#789B8C");
    private static readonly Color ColorInvactive = Color.FromHex("#6E6F7E");

    private readonly StringBuilder _tooltipString = new();

    private readonly StyleBoxFlat _styleBox = new()
    {
        BackgroundColor = ColorNormal,
    };

    private MiGoErectMenu? _owner;

    public MiGoErectMenuItem()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);
        Panel.PanelOverride = _styleBox;
        OnToggled += (args) =>
        {
            if (Building is null)
                return;
            _owner?.OnItemToggled(this);
        };
    }

    public void SetOwner(MiGoErectMenu owner)
    {
        _owner = owner;
    }

    public void SetItem(CultYoggBuildingPrototype building)
    {
        Building = building;
        ItemView.SetPrototype(building.ResultEntityId.Id);
        MaterialsContainer.DisposeAllChildren();
        if (ItemView.Entity == null)
            return;
        var meta = _entityManager.GetComponent<MetaDataComponent>(ItemView.Entity.Value.Owner);
        _tooltipString.Clear();
        _tooltipString.Append("[head=2]");
        _tooltipString.Append(meta.EntityName);
        _tooltipString.Append("[/head]");
        _tooltipString.AppendLine();
        _tooltipString.Append("[color=gray]");
        _tooltipString.Append(meta.EntityDescription);
        _tooltipString.Append("[/color]");
        _tooltipString.AppendLine();
        _tooltipString.AppendLine();
        for (var i = 0; i < building.Materials.Count; i++)
        {
            var material = building.Materials[i];
            if (!_prototypeManager.TryIndex(material.StackType, out var type))
                continue;
            _tooltipString.Append("[bullet]");
            _tooltipString.Append('x');
            _tooltipString.Append(material.Count);
            _tooltipString.Append(' ');
            _tooltipString.Append(Loc.GetString(type.Name));
            if (i < building.Materials.Count - 1)
            {
                _tooltipString.AppendLine();
            }
            var materialView = new MiGoErectMaterialView() { IconScale = new System.Numerics.Vector2(2, 2) };
            materialView.SetItem(material.Icon, material.Count);
            MaterialsContainer.AddChild(materialView);
        }
        TooltipSupplier = CreateRichTooltip;
        ToolTip = _tooltipString.ToString();
    }

    private void UpdateColors()
    {
        _styleBox.BackgroundColor = DrawMode switch
        {
            DrawModeEnum.Normal => ColorNormal,
            DrawModeEnum.Pressed => ColorPressed,
            DrawModeEnum.Hover => ColorHovered,
            DrawModeEnum.Disabled => ColorInvactive,
            _ => ColorNormal
        };
    }

    protected override void StylePropertiesChanged()
    {
        base.StylePropertiesChanged();
        UpdateColors();
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        UpdateColors();
    }

    private Control CreateRichTooltip(Control hovered)
    {
        var tooltip = new Tooltip()
        {
            Tracking = hovered.TrackingTooltip,
        };
        if (FormattedMessage.TryFromMarkup(hovered.ToolTip ?? "", out var message))
        {
            tooltip.SetMessage(message);
        }
        else
        {
            tooltip.Text = hovered.ToolTip;
        }
        return tooltip;
    }
}
