// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

namespace Content.Server.SS220.CultYogg;

[RegisterComponent]
[Access(typeof(MiGoReplacementSystem))]

public sealed partial class MiGoReplacementComponent : Component
{
    //Should the timer count down the time
    public bool ShouldBeCounted = false;

    //Time to replace MiGo
    public float BeforeReplacemetTime = 15;//ToDo adjust this number
    //ToDo replace this to timespan

    //Timer
    public float ReplacementTimer = 0;
}
