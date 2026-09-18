using System;
using UnityEngine;
using Zenject;

namespace Laki
{
    internal sealed class GameplaySession : IInitializable, IDisposable
    {
        private readonly DiContainer container;
        private readonly bool multiplayerPlus;
        private LakiState state;
        private BeatmapObjectManager manager;
        private NoteData target;
        private NoteController attached;
        private LakiVisualController visual;
        private LakiKind kind;
        private bool counted, subscribed, consumed, effects;
        private bool visualTest;

        public GameplaySession(DiContainer container, bool multiplayerPlus)
        { this.container = container; this.multiplayerPlus = multiplayerPlus; }

        public void Initialize()
        {
            try
            {
                var version = Application.version;
                if (version != "1.40.8" && version != "1.40.8_7379") return;
                if (!ReplayGuard.IsSafeLivePlay()) return;
                // Only StandardPlayer registers this session. Multiplayer+ uses that
                // same local standard scene; never resolve a native remote-player scope.
                var config = container.Resolve<LakiConfig>();
                if (!config.EnableLaki || (multiplayerPlus && !config.EnableInMultiplayer)) return;
                manager = container.TryResolve<BeatmapObjectManager>();
                var init = container.TryResolve<BeatmapCallbacksController.InitData>();
                var map = container.TryResolve<IReadonlyBeatmapData>();
                var pause = container.TryResolve<IGamePause>();
                var setup = container.TryResolve<GameplayCoreSceneSetupData>();

                var factory = container.Resolve<LakiVisualFactory>();
                var selector = new LakiSelector();
                if (manager == null || manager.GetType() != typeof(BasicBeatmapObjectManager) ||
                    init == null || !ReferenceEquals(init.beatmapData, map) || !selector.KnownMap(map) ||
                    pause == null || setup == null || float.IsNaN(init.startFilterTime) || float.IsInfinity(init.startFilterTime)) return;
                state = container.Resolve<LakiState>();
                string testMode = config.TestMode;
                visualTest = LakiTestMode.TryKind(testMode, out kind);
                bool pityReady;
                if (!state.TryBegin(out pityReady, visualTest)) return;
                counted = true;

                // Ghost notes / disappearing arrows could otherwise acquire unintended cues.
                if (setup.gameplayModifiers.ghostNotes || setup.gameplayModifiers.disappearingArrows || setup.gameplayModifiers.zenMode) return;
                if (!LakiRules.TrySelect(state.Random, testMode, config.EnableRareLaki, pityReady, out kind)) return;
                var start = init.startFilterTime;
                if (setup.practiceSettings != null) start = Math.Max(start, setup.practiceSettings.startSongTime);
                target = selector.Select(map, start, state.Random, visualTest);
                if (target == null || !factory.EnsureReady()) return;
                effects = config.EnableEffects;
                // Preallocate one five-motif visual before any note callbacks.
                visual = factory.Create(kind, pause);
                manager.noteWasSpawnedEvent += OnSpawn;
                manager.noteWasCutEvent += OnCut;
                manager.noteWasMissedEvent += OnGone;
                manager.noteWasDespawnedEvent += OnGone;
                manager.noteDidStartDissolvingEvent += OnDissolve;
                manager.didHideAllBeatmapObjectsEvent += OnHide;
                subscribed = true;
                if (visualTest)
                {
                    // Commit only after target selection, visual preallocation and hooks succeed.
                    if (!state.ConsumeTest(testMode, target != null))
                        throw new InvalidOperationException("Test Mode request changed before one-shot commit.");
                    Plugin.Log.Info("Laki Notes Test: " + testMode + "; kind=" + kind +
                        "; note time=" + target.time + "; lane=" + target.lineIndex +
                        "; layer=" + target.noteLineLayer + "; color=" + target.colorType +
                        "; direction=" + target.cutDirection + "; visual prepared; one-shot consumed -> Off.");
                }
            }
            catch (Exception e) { Abort(e); }
        }

        private void OnSpawn(NoteController note)
        {
            if (consumed || note == null || !ReferenceEquals(note.noteData, target)) return;
            consumed = true; // No replacement or second appearance after seeking/reuse.
            try
            {
                if (note.GetType() != typeof(GameNoteController) || note.hidden || note.dissolving ||
                    manager.spawnHidden || note.noteTransform == null || !note.gameObject.activeInHierarchy ||
                    note.noteData.gameplayType != NoteData.GameplayType.Normal ||
                    note.noteData.scoringType != NoteData.ScoringType.Normal)
                {
                    if (visualTest) Plugin.Log.Info("Laki Notes Test: selected note failed spawn safety checks; visual skipped.");
                    return;
                }
                if (!visual.Attach(note))
                {
                    if (visualTest) Plugin.Log.Info("Laki Notes Test: visual attachment rejected by safety checks.");
                    return;
                }
                attached = note;
                if (kind == LakiKind.Secret) state.SecretAppeared();
                if (visualTest) Plugin.Log.Info("Laki Notes Test: visual attached (" + kind + ").");
            }
            catch (Exception e) { Abort(e); }
        }

        private void OnCut(NoteController note, in NoteCutInfo cut)
        {
            if (!ReferenceEquals(note, attached)) return;
            try
            {
                attached = null;
                bool success = cut.allIsOK && ReferenceEquals(cut.noteData, target);
                if (success && effects) visual.Celebrate();
                else visual.Hide();
                if (visualTest) Plugin.Log.Info(success
                    ? (effects ? "Laki Notes Test: normal cut; success effect started." : "Laki Notes Test: normal cut; effects disabled by setting.")
                    : "Laki Notes Test: bad cut; no success effect.");
            }
            catch (Exception e) { Abort(e); }
        }
        private void OnGone(NoteController note)
        {
            if (!ReferenceEquals(note, attached)) return;
            attached = null;
            try { if (visual != null) visual.Hide(); } catch (Exception e) { Abort(e); }
        }
        private void OnDissolve(NoteControllerBase note)
        {
            if (ReferenceEquals(note, attached)) OnGone(attached);
        }
        private void OnHide(bool hidden)
        {
            // Also cancel a detached success visual during a seek/exit/hide operation.
            if (!hidden) return;
            attached = null;
            try { if (visual != null) visual.Hide(); } catch (Exception e) { Abort(e); }
        }
        private void Abort(Exception e)
        {
            Plugin.Log.Error("Laki Notes disabled for this play: " + e);
            Unsubscribe();
            if (visual != null) { UnityEngine.Object.Destroy(visual.gameObject); visual = null; }
            attached = null;
        }
        private void Unsubscribe()
        {
            if (!subscribed || manager == null) return;
            subscribed = false;
            manager.noteWasSpawnedEvent -= OnSpawn;
            manager.noteWasCutEvent -= OnCut;
            manager.noteWasMissedEvent -= OnGone;
            manager.noteWasDespawnedEvent -= OnGone;
            manager.noteDidStartDissolvingEvent -= OnDissolve;
            manager.didHideAllBeatmapObjectsEvent -= OnHide;
        }
        public void Dispose()
        {
            Unsubscribe();
            if (visual != null) { visual.Hide(); UnityEngine.Object.Destroy(visual.gameObject); }
            if (counted) { counted = false; state.End(); }
            attached = null;
            target = null;
        }
    }
}


