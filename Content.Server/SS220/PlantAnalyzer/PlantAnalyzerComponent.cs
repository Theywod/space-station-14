using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Prototypes;

namespace Content.Server.SS220.PlantAnalyzer;

[RegisterComponent]
public sealed partial class PlantAnalyzerComponent : Component
{
    [DataField]
    public TimeSpan ScanDelay = TimeSpan.FromSeconds(1.5);

    [DataField]
    public EntityUid? ScannedEntity;

    [DataField]
    public SoundSpecifier? ScanningBeginSound;

    [DataField]
    public SoundSpecifier ScanningEndSound = new SoundPathSpecifier("/Audio/Items/Medical/healthscanner.ogg");

    [DataField]
    public bool Silent;

    [DataField]
    public bool CanPrint;

    [DataField]
    public EntProtoId MachineOutput = "PlantAnalyzerReportPaper";

    [DataField]
    public TimeSpan PrintCooldown = TimeSpan.FromSeconds(10);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan PrintReadyAt = TimeSpan.Zero;

    public string LastScannedName = string.Empty;
    public string LastScannedReport = string.Empty;
}