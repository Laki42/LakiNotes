using System;
using System.Reflection;

namespace Laki
{
    internal static class ReplayGuard
    {
        public static Assembly FindAssembly(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetName().Name == name) return assembly;
            return null;
        }

        // Optional integrations: metadata/state reads only, once per session.
        // Unknown API, missing state or reflection failure means no Laki.
        public static bool IsSafeLivePlay()
        {
            try
            {
                var bl = FindAssembly("BeatLeader");
                if (bl != null)
                {
                    var type = bl.GetType("BeatLeader.Replayer.ReplayerLauncher", false);
                    var property = type?.GetProperty("IsStartedAsReplay", BindingFlags.Public | BindingFlags.Static);
                    if (property == null || property.PropertyType != typeof(bool) || (bool)property.GetValue(null)) return false;
                }
                var ss = FindAssembly("ScoreSaber");
                if (ss != null)
                {
                    var type = ss.GetType("ScoreSaber.Plugin", false);
                    var property = type?.GetProperty("ReplayState", BindingFlags.NonPublic | BindingFlags.Static);
                    var state = property?.GetValue(null);
                    if (state == null) return false;
                    var field = state.GetType().GetField("IsPlaybackEnabled", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field == null || field.FieldType != typeof(bool) || (bool)field.GetValue(state)) return false;
                    // IsPlaybackEnabled covers modern AND legacy playback. IsLegacyReplay
                    // remains true after ReplayEnd, so it must not gate later live plays.
                }
                return true;
            }
            catch { return false; }
        }
    }
}
