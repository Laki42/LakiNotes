using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Laki;

internal static class Tests
{
    private static int assertions;
    private static string game;
    private static void Check(bool condition, string message)
    { assertions++; if (!condition) throw new Exception(message); }
    public static int Main(string[] args)
    {
        game = args[0];
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        try { Run(); Console.WriteLine("PASS: " + assertions + " assertions"); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    private static Assembly Resolve(object sender, ResolveEventArgs args)
    {
        string name = new AssemblyName(args.Name).Name + ".dll";
        foreach (string folder in new[] { "Beat Saber_Data/Managed", "Libs", "Plugins" })
        {
            string path = Path.Combine(game, folder, name);
            if (File.Exists(path)) return Assembly.LoadFrom(path);
        }
        return null;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        int events = 0; var kinds = new int[3];
        for (int roll = 0; roll < 100; roll++)
        {
            if (LakiRules.HasEvent(roll)) events++;
            kinds[(int)LakiRules.Kind(roll, true, false)]++;
            Check(LakiRules.Kind(roll, false, true) == LakiKind.Laki, "Rare-off overrides pity");
            Check(LakiRules.Kind(roll, true, true) == LakiKind.Secret, "Armed pity forces Secret");
        }
        Check(events == 40 && kinds[0] == 75 && kinds[1] == 20 && kinds[2] == 5, "Exact probability partitions");
        var config = new LakiConfig(); var state = new LakiState(config);
        bool ready;
        for (int play = 1; play <= 100; play++)
        {
            Check(state.TryBegin(out ready) && !ready, "Pity must not force within first 100 plays");
            Check(!state.TryBegin(out ready), "Duplicate live session rejected");
            state.End();
        }
        Check(config.SessionsWithoutSecret == 100, "Saved at session end");
        state = new LakiState(config);
        Check(state.TryBegin(out ready) && ready, "101st play armed after reload");
        state.End();
        Check(state.TryBegin(out ready) && ready, "No-event play retains pity");
        state.SecretAppeared();
        Check(state.Counter == 0 && config.SessionsWithoutSecret == 100, "Appearance resets memory, no mid-song persistence");
        state.End();
        Check(config.SessionsWithoutSecret == 0, "Appearance persisted at teardown");
        state = new LakiState(new LakiConfig { SessionsWithoutSecret = int.MaxValue });
        Check(state.Counter == 100, "Overflow-safe state load");
        Check(LakiRules.ClampCounter(-1) == 0, "Negative state recovery");
        VisualTestMode();
        ModifierTests.Run(Check);
        MultiplayerInstallationTests.Run(Check);

        var selector = new LakiSelector();
        Check(selector.Eligible(Note(10), 10), "Practice boundary is inclusive");
        Check(!selector.Eligible(Note(9.999f), 10), "Pre-practice notes excluded");
        Check(!selector.Eligible(Note(float.NaN), 0), "Non-finite time excluded");
        Check(!selector.Eligible(Note(float.PositiveInfinity), 0), "Infinite time excluded");
        Check(!selector.Eligible(NoteData.CreateBombNoteData(10, 0, 0, 0, NoteLineLayer.Base), 0), "Bomb excluded");
        foreach (NoteData.ScoringType score in Enum.GetValues(typeof(NoteData.ScoringType)))
            Check(selector.Eligible(Note(10, score), 0) == (score == NoteData.ScoringType.Normal), "Scoring whitelist: " + score);
        var arc = Note(10); arc.MarkAsSliderHead();
        Check(!selector.Eligible(arc, 0), "Arc head excluded");
        Check(!selector.Eligible(new UnknownNote(), 0), "Unknown NoteData subclass excluded");
        var map = new BeatmapData(4);
        var before = Note(1); var first = Note(10); var second = Note(20);
        map.AddBeatmapObjectDataInOrder(before); map.AddBeatmapObjectDataInOrder(first); map.AddBeatmapObjectDataInOrder(second);
        var random = new Random(91234); int firstCount = 0;
        for (int i = 0; i < 10000; i++)
        {
            var selected = selector.Select(map, 10, random);
            Check(ReferenceEquals(selected, first) || ReferenceEquals(selected, second), "Original reachable note references only");
            if (ReferenceEquals(selected, first)) firstCount++;
        }
        Check(Math.Abs(firstCount - 5000) < 200, "Uniform candidate choice");
        Check(selector.Select(map, 21, random) == null, "Empty reachable collection disables event");
        Check(selector.Select(new BeatmapData(0), 0, random) == null, "Invalid map disables event");
        Check(selector.Select(map, float.NaN, random) == null, "Invalid filter disables event");
        Check(before.time == 1 && first.time == 10 && second.time == 20, "Note timing unchanged");
        CustomNotes();
        ReplayFlags();
        GeometryTests.Run(Check, Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Visual-preview.png")));
        Console.WriteLine("Real 1.40.8 NoteData + BeatmapData selector checks passed; first/second: " + firstCount + "/" + (10000-firstCount));
        Distribution();
    }
    private static void VisualTestMode()
    {
        LakiKind kind;
        Check(!LakiRules.TrySelect(new Rolls(40), LakiTestMode.Off, true, false, out kind), "Off: no-event boundary");
        Check(LakiRules.TrySelect(new Rolls(39, 74), LakiTestMode.Off, true, false, out kind) && kind == LakiKind.Laki,
            "Off: regular Laki draw");
        Check(LakiRules.TrySelect(new Rolls(0, 75), LakiTestMode.Off, true, false, out kind) && kind == LakiKind.Super,
            "Off: Super draw");
        Check(LakiRules.TrySelect(new Rolls(0, 95), LakiTestMode.Off, true, false, out kind) && kind == LakiKind.Secret,
            "Off: Secret draw");
        Check(LakiRules.TrySelect(new Rolls(0, 99), LakiTestMode.Off, false, true, out kind) && kind == LakiKind.Laki,
            "Off: Rare disabled, including armed pity");
        Check(LakiRules.TrySelect(new Rolls(0, 0), LakiTestMode.Off, true, true, out kind) && kind == LakiKind.Secret,
            "Off: pity forces Secret");
        Check(!LakiRules.TrySelect(new Rolls(99), LakiTestMode.Off, true, true, out kind), "Pity does not force existence");
        Check(!LakiTestMode.TryKind("unknown", out kind), "Unknown saved choice cannot force a note");

        string[] modes = { LakiTestMode.ForceLaki, LakiTestMode.ForceSuper, LakiTestMode.ForceSecret };
        for (int index = 0; index < modes.Length; index++)
        {
            string mode = modes[index];
            foreach (bool rare in new[] { false, true })
                foreach (bool pity in new[] { false, true })
                    Check(LakiRules.TrySelect(new Rolls(), mode, rare, pity, out kind) && (int)kind == index,
                        mode + ": bypass both RNG draws and Rare/Pity");
            foreach (int previous in new[] { 0, 37, 100 })
            {
                var config = new LakiConfig { TestMode = mode, SessionsWithoutSecret = previous };
                var state = new LakiState(config); bool ready;
                Check(!state.ConsumeTest(mode, true) && config.TestMode == mode, "No session: retain reservation");
                Check(state.TryBegin(out ready, true), "Begin test session");
                Check(state.Counter == previous, "Test begin never increments pity");
                Check(!state.TryBegin(out ready), "Duplicate ordinary begin cannot change test identity");
                Check(!state.ConsumeTest(mode, false) && config.TestMode == mode, "No safe target: retain reservation");
                state.End();
                Check(config.TestMode == mode && config.SessionsWithoutSecret == previous, "Failed setup: saved reservation and pity unchanged");
                state = new LakiState(config); // retry after a normal restart
                Check(state.TryBegin(out ready, true), "Retry pending test");
                int notifications = 0;
                state.TestModeConsumed += () => notifications++;
                Check(state.ConsumeTest(mode, true) && config.TestMode == LakiTestMode.Off, "Prepared target consumes one-shot");
                Check(!state.ConsumeTest(mode, true) && notifications == 1, "One-shot consumes exactly once");
                state.SecretAppeared(); // even after TestMode auto-switches Off!
                Check(state.Counter == previous, "Test Secret cannot reset pity after consumption");
                state.End();
                Check(config.SessionsWithoutSecret == previous, "Test teardown never persists a different pity");
                state = new LakiState(config);
                Check(config.TestMode == LakiTestMode.Off && state.TryBegin(out ready), "Next session is ordinary after reload");
                Check(state.Counter == Math.Min(previous+1, 100), "Ordinary counting resumes");
                state.SecretAppeared(); state.End();
                Check(config.SessionsWithoutSecret == 0, "Ordinary Secret reset still works");
            }
            var map = new BeatmapData(4);
            var candidates = new List<NoteData>();
            for (int i = 0; i < 101; i++) { var n = Note(i); candidates.Add(n); map.AddBeatmapObjectDataInOrder(n); }
            var selector = new LakiSelector();
            Check(ReferenceEquals(selector.Select(map, 0, new Rolls(), true), candidates[50]), "Test selects middle without RNG");
            Check(ReferenceEquals(selector.Select(map, 60, new Rolls(), true), candidates[80]), "Practice middle uses only reachable notes");
            Check(ReferenceEquals(selector.Select(map, 100, new Rolls(), true), candidates[100]), "One eligible note fallback");
            Check(selector.Select(map, 101, new Rolls(), true) == null, "No reachable target");
            Console.WriteLine(mode + ": PASS (force selection, RNG isolation, pity unchanged, one-shot and retry); VR NOT RUN");
        }
        var ordinaryConfig = new LakiConfig { TestMode = LakiTestMode.ForceSecret };
        var ordinaryState = new LakiState(ordinaryConfig); bool ordinaryReady;
        ordinaryState.TryBegin(out ordinaryReady);
        Check(!ordinaryState.ConsumeTest(LakiTestMode.ForceSecret, true), "Ordinary session cannot consume test request");
        ordinaryState.End();
    }
    private sealed class Rolls : Random
    {
        private readonly int[] values;
        private int next;
        public Rolls(params int[] values) { this.values = values; }
        public override int Next(int maxValue)
        {
            if (next == values.Length) throw new Exception("Unexpected RNG call (force mode must not advance RNG)");
            int value = values[next++];
            if (value < 0 || value >= maxValue) throw new Exception("Test RNG value outside range");
            return value;
        }
    }
    private static void CustomNotes()
    {
        var assembly = Assembly.LoadFrom(Path.Combine(game, "Plugins", "CustomJSONData.dll"));
        var dataType = assembly.GetType("CustomJSONData.CustomBeatmap.CustomData", true);
        var noteType = assembly.GetType("CustomJSONData.CustomBeatmap.CustomNoteData", true);
        var data = (IDictionary<string, object>)Activator.CreateInstance(dataType);
        var note = (NoteData)Activator.CreateInstance(noteType, new object[] { 10f, 0f, 0, 0,
            NoteLineLayer.Base, NoteLineLayer.Base, NoteData.GameplayType.Normal, NoteData.ScoringType.Normal,
            ColorType.ColorA, NoteCutDirection.Up, 0f, 0f, 0, 0f, 0f, 1f, data, new Version(3,0,0) });
        var selector = new LakiSelector();
        Check(selector.Eligible(note, 0), "Real CustomNoteData accepted");
        data["color"] = new object();
        Check(selector.Eligible(note, 0), "Color-only metadata accepted without inspecting/mutating color");
        foreach (string key in new[] { "_fake", "uninteractable", "track", "animation", "unknown" })
        {
            data[key] = false;
            Check(!selector.Eligible(note, 0), "Unsafe custom key excluded: " + key);
            data.Remove(key);
        }
    }
    private static void ReplayFlags()
    {
        Check(ReplayGuard.IsSafeLivePlay(), "No replay plugins is safe");
        string fixtures = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures");
        // These small fixtures reproduce only the metadata verified in the installed DLLs.
        // Do not execute the real plugin's Unity initialization outside the game.
        var bl = Assembly.LoadFrom(Path.Combine(fixtures, "BeatLeader.dll"));
        var flag = bl.GetType("BeatLeader.Replayer.ReplayerLauncher").GetProperty("IsStartedAsReplay");
        Check(ReplayGuard.IsSafeLivePlay(), "BeatLeader live play");
        flag.SetValue(null, true);
        Check(!ReplayGuard.IsSafeLivePlay(), "BeatLeader replay excluded");
        flag.SetValue(null, false);
        var ss = Assembly.LoadFrom(Path.Combine(fixtures, "ScoreSaber.dll"));
        var stateProperty = ss.GetType("ScoreSaber.Plugin").GetProperty("ReplayState", BindingFlags.NonPublic | BindingFlags.Static);
        object replay = stateProperty.GetValue(null);
        var playback = replay.GetType().GetField("IsPlaybackEnabled", BindingFlags.NonPublic | BindingFlags.Instance);
        var legacy = replay.GetType().GetField("IsLegacyReplay", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(ReplayGuard.IsSafeLivePlay(), "ScoreSaber live play");
        playback.SetValue(replay, true);
        Check(!ReplayGuard.IsSafeLivePlay(), "ScoreSaber modern replay excluded");
        legacy.SetValue(replay, true);
        Check(!ReplayGuard.IsSafeLivePlay(), "ScoreSaber legacy replay excluded");
        playback.SetValue(replay, false);
        Check(ReplayGuard.IsSafeLivePlay(), "Stale legacy flag must not suppress later live play");
        stateProperty.SetValue(null, null);
        Check(!ReplayGuard.IsSafeLivePlay(), "Missing ScoreSaber state fails closed");
    }
    private static NoteData Note(float time, NoteData.ScoringType score = NoteData.ScoringType.Normal) =>
        new NoteData(time, 0, 0, 0, NoteLineLayer.Base, NoteLineLayer.Base, NoteData.GameplayType.Normal,
            score, ColorType.ColorA, NoteCutDirection.Up, 0, 0, 0, 0, 0, 1);
    private sealed class UnknownNote : NoteData
    {
        public UnknownNote() : base(10, 0, 0, 0, NoteLineLayer.Base, NoteLineLayer.Base, GameplayType.Normal,
            ScoringType.Normal, ColorType.ColorA, NoteCutDirection.Up, 0, 0, 0, 0, 0, 1) { }
    }
    private static void Distribution()
    {
        const int n = 2000000;
        var random = new Random(14087379); var counts = new int[4]; int counter = 0;
        for (int i = 0; i < n; i++)
        {
            bool ready = counter >= 100; counter = LakiRules.BeginSession(counter);
            if (!LakiRules.HasEvent(random.Next(100))) { counts[3]++; continue; }
            var kind = LakiRules.Kind(random.Next(100), true, ready);
            counts[(int)kind]++; if (kind == LakiKind.Secret) counter = 0;
        }
        double q = Math.Pow(.98, 100), meanWait = (1-q)/.02 + q/.4;
        double armedFraction = (q/.4)/meanWait;
        double[] expected = { .3*(1-armedFraction), .08*(1-armedFraction), 1/meanWait, .6 };
        for (int i = 0; i < 4; i++)
        {
            double actual = (double)counts[i]/n;
            Check(Math.Abs(actual-expected[i]) < .0015, "Stationary distribution: " + i);
            Console.WriteLine("Distribution " + i + ": " + actual.ToString("P4") + "; expected " + expected[i].ToString("P4"));
        }
    }
}

// Only the persistence/logging boundary is stubbed. All note/map types above are
// the actual inspected game assemblies; no invented game API or Unity simulation.
namespace Laki
{
    internal sealed class LakiConfig
    {
        public bool EnableLaki { get; set; } = true;
        public bool EnableInMultiplayer { get; set; } = true;
        public int SessionsWithoutSecret { get; set; }
        public string TestMode { get; set; } = LakiTestMode.Off;
    }
    internal static class Plugin { public static readonly TestLog Log = new TestLog(); }
    internal sealed class TestLog
    {
        public int Errors;
        public void Warn(string text) { Console.Error.WriteLine(text); }
        public void Info(string text) { }
        public void Debug(string text) { }
        public void Error(string text) { Errors++; }
    }
}
