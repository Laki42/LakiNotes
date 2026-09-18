using System;

namespace Laki
{
    internal static class GameplayInstallation
    {
        internal static void Install(LakiConfig config, Action<bool> bind)
        {
            try
            {
                string detail = "Enable Laki Notes OFF";
                bool allowed = TryInstall(config.EnableLaki, config.EnableInMultiplayer, () =>
                {
                    var environment = MultiplayerPlusGuard.Inspect(AppDomain.CurrentDomain.GetAssemblies(), out detail);
                    return environment;
                }, bind);
                if (allowed) Plugin.Log.Debug("Laki Notes StandardPlayer binding registered: " + detail);
                else Plugin.Log.Info("Laki Notes session skipped before binding: " + detail +
                    "; EnableInMultiplayer=" + config.EnableInMultiplayer +
                    "; no GameplaySession, note hooks, visual, Pity update or Test Mode consumption.");
            }
            catch (Exception e)
            {
                // Includes type/message/stack; no exception enters SiraUtil's installer.
                Plugin.Log.Error("Laki Notes optional gameplay installation failed; skipping Laki Notes for this session. " + e);
            }
        }

        internal static bool TryInstall(bool enabled, bool multiplayerEnabled, Func<PlusEnvironment> inspect, Action<bool> bind)
        {
            if (!enabled) return false;
            var environment = inspect();
            if (environment == PlusEnvironment.Unknown) return false;
            bool multiplayerPlus = environment == PlusEnvironment.InRoom;
            if (multiplayerPlus && !multiplayerEnabled) return false;
            bind(multiplayerPlus);
            return true;
        }
    }
}

