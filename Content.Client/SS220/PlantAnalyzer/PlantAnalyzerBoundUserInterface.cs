using Content.Shared.SS220.PlantAnalyzer;
using Robust.Client.UserInterface;

namespace Content.Client.SS220.PlantAnalyzer;

public sealed class PlantAnalyzerBoundUserInterface : BoundUserInterface
{
    private PlantAnalyzerWindow? _window;

    public PlantAnalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<PlantAnalyzerWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.PrintButton.OnPressed += _ => SendMessage(new PlantAnalyzerPrintMessage());
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (_window == null || message is not PlantAnalyzerScannedUserMessage cast)
            return;

        _window.Populate(cast.State);
    }
}