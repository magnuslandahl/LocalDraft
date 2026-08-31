namespace LokalDiktering.App;

internal static class AudioLevelMeter
{
    public static double GetWidth(float level, double availableWidth)
    {
        if (level <= 0 || availableWidth <= 0)
        {
            return 0;
        }

        var decibels = 20 * Math.Log10(Math.Clamp(level, 0, 1));
        var normalized = Math.Clamp((decibels + 60) / 60, 0, 1);
        return Math.Max(2, normalized * availableWidth);
    }
}
