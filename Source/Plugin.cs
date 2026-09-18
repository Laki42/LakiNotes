using System;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using IPA.Logging;
using SiraUtil.Zenject;
using Zenject;

[assembly: System.Reflection.AssemblyVersion("0.1.4.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.1.4.0")]
[assembly: System.Reflection.AssemblyTitle("Laki Notes")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("IPA.Config.Generated")]

namespace Laki
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public sealed class Plugin
    {
        internal static Logger Log;

        [Init]
        public Plugin(Logger logger, [Config.Name("Laki")] Config config, Zenjector zenjector)
        {
            Log = logger;
            var settings = config.Generated<LakiConfig>();
            var state = new LakiState(settings);
            var visuals = new LakiVisualFactory();
            zenjector.Install(Location.App, c =>
            {
                c.BindInstance(settings);
                c.BindInstance(state);
                c.BindInstance(visuals);
                c.BindInterfacesTo<AppLifetime>().AsSingle();
            });
            zenjector.Install(Location.Menu, c => c.BindInterfacesTo<SettingsMenu>().AsSingle());
#if LAKI_NO_GAMEPLAY
            Log.Info("Laki Notes DIAGNOSTIC NO-GAMEPLAY: no StandardPlayer or Multiplayer installer registered; App/Menu only.");
#else
            // Multiplayer+ 6.4.2 launches StandardGameplayInstaller as well. Classify
            // BEFORE adding any binding, using the live config captured from Init.
            zenjector.Install(Location.StandardPlayer, c =>
                GameplayInstallation.Install(settings,
                    multiplayerPlus => c.BindInterfacesTo<GameplaySession>().AsSingle().WithArguments(multiplayerPlus)));
            // Intentionally no MultiPlayer / MultiplayerCore / AllPlayers registration.
            Log.Info("Laki Notes 0.1.4: Multiplayer+ 6.4.2 local visuals supported; EnableInMultiplayer gates binding. Native Multiplayer installer remains disabled.");
#endif
        }

        private sealed class AppLifetime : IDisposable
        {
            private readonly LakiState state;
            private readonly LakiVisualFactory visuals;
            public AppLifetime(LakiState state, LakiVisualFactory visuals)
            { this.state = state; this.visuals = visuals; }
            public void Dispose() { state.Save(); visuals.Dispose(); }
        }
    }
}


