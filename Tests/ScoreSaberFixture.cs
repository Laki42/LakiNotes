namespace ScoreSaber
{
    public static class Plugin
    {
        internal static ReplayStateData ReplayState { get; set; } = new ReplayStateData();
    }
    internal sealed class ReplayStateData
    {
        internal bool IsPlaybackEnabled = false;
        internal bool IsLegacyReplay = false;
    }
}
