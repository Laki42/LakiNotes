using System;

namespace Laki
{
    internal enum LakiKind { Laki, Super, Secret }

    internal static class LakiTestMode
    {
        public const string Off = "Off";
        public const string ForceLaki = "Force Laki";
        public const string ForceSuper = "Force Super Laki";
        public const string ForceSecret = "Force Secret Laki";
        public static bool TryKind(string mode, out LakiKind kind)
        {
            kind = LakiKind.Laki;
            if (mode == ForceLaki) return true;
            if (mode == ForceSuper) { kind = LakiKind.Super; return true; }
            if (mode == ForceSecret) { kind = LakiKind.Secret; return true; }
            return false;
        }
    }

    internal static class LakiRules
    {
        public const int PityLimit = 100;
        public static int ClampCounter(int value) => Math.Max(0, Math.Min(PityLimit, value));
        public static int BeginSession(int previous) => Math.Min(PityLimit, ClampCounter(previous) + 1);
        public static bool HasEvent(int roll) => roll < 40;
        public static bool TrySelect(Random random, string testMode, bool rare, bool pityReady, out LakiKind kind)
        {
            if (LakiTestMode.TryKind(testMode, out kind)) return true;
            if (!HasEvent(random.Next(100))) return false;
            kind = Kind(random.Next(100), rare, pityReady);
            return true;
        }
        public static LakiKind Kind(int roll, bool rare, bool pityReady)
        {
            if (!rare) return LakiKind.Laki;
            if (pityReady) return LakiKind.Secret;
            return roll < 75 ? LakiKind.Laki : roll < 95 ? LakiKind.Super : LakiKind.Secret;
        }
    }
}
