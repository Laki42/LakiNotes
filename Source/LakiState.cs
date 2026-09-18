using System;
using System.Security.Cryptography;

namespace Laki
{
    internal sealed class LakiState
    {
        private readonly LakiConfig config;
        // Independent of UnityEngine.Random and the game's injected System.Random.
        public readonly Random Random;
        public int Counter { get; private set; }
        private bool sessionOpen;
        private bool sessionIsTest;
        public event Action TestModeConsumed;
        public LakiState(LakiConfig config)
        {
            this.config = config;
            Counter = LakiRules.ClampCounter(config.SessionsWithoutSecret);
            var seed = new byte[4];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(seed);
            Random = new Random(BitConverter.ToInt32(seed, 0));
        }
        public bool TryBegin(out bool pityReady, bool visualTest = false)
        {
            pityReady = Counter >= LakiRules.PityLimit;
            if (sessionOpen) return false;
            sessionOpen = true;
            sessionIsTest = visualTest;
            if (!sessionIsTest) Counter = LakiRules.BeginSession(Counter);
            return true;
        }
        public void SecretAppeared() { if (sessionOpen && !sessionIsTest) Counter = 0; }
        public bool ConsumeTest(string mode, bool targetReady)
        {
            if (!sessionOpen || !sessionIsTest || !targetReady || config.TestMode != mode ||
                !LakiTestMode.TryKind(mode, out _)) return false;
            // Persist immediately through BSIPA, once at session setup, never on spawn/cut.
            // sessionIsTest remains true after the setting becomes Off.
            config.TestMode = LakiTestMode.Off;
            TestModeConsumed?.Invoke();
            return true;
        }
        public void End()
        {
            if (!sessionOpen) return;
            sessionOpen = false;
            if (!sessionIsTest) Save();
            sessionIsTest = false;
        }
        public void Save()
        {
            // Generated config schedules its own write; never called by note callbacks.
            try
            {
                if (config.SessionsWithoutSecret != Counter) config.SessionsWithoutSecret = Counter;
            }
            catch (Exception e) { Plugin.Log.Warn("Laki Notes state save failed: " + e.Message); }
        }
    }
}
