using Verse;

namespace MSExotic;

public class CompProperties_MSStimWorn : CompProperties
{
    public int StimUses = 1;

    public CompProperties_MSStimWorn()
    {
        compClass = typeof(CompMSStimWorn);
    }
}