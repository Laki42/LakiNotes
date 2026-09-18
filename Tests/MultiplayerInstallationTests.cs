using System;
using System.Reflection;
using System.Reflection.Emit;
using Laki;

internal static class MultiplayerInstallationTests
{
    internal static void Run(Action<bool,string> check)
    {
        int probes = 0, bindings = 0;
        check(!GameplayInstallation.TryInstall(false, true, () => { probes++; throw new Exception("must not probe"); }, isPlus => bindings++), "Master OFF skips installer work");
        check(probes == 0 && bindings == 0, "OFF causes no probe, binding, constructor or resolution");
        foreach (bool enabled in new[] { false, true })
        foreach (bool mp in new[] { false, true })
        foreach (PlusEnvironment environment in Enum.GetValues(typeof(PlusEnvironment)))
        {
            int count = 0;
            bool argument = false;
            bool result = GameplayInstallation.TryInstall(enabled, mp, () => environment,
                plus => { count++; argument = plus; });
            bool expected = enabled && environment != PlusEnvironment.Unknown &&
                (environment != PlusEnvironment.InRoom || mp);
            check(result == expected && count == (expected ? 1 : 0), "Settings/environment matrix");
            if (expected) check(argument == (environment == PlusEnvironment.InRoom), "Correct local session classification");
        }
        foreach (PlusEnvironment state in Enum.GetValues(typeof(PlusEnvironment)))
        {
            bindings = 0;
            bool allowed = GameplayInstallation.TryInstall(true, false, () => state, isPlus => bindings++);
            bool expected = state == PlusEnvironment.Absent || state == PlusEnvironment.OutsideRoom;
            check(allowed == expected && bindings == (expected ? 1 : 0), "Before-bind decision: " + state);
        }
        string detail;
        check(MultiplayerPlusGuard.Inspect(new Assembly[0], out detail) == PlusEnvironment.Absent, "Absent optional mod preserves Solo");
        var good = Fixture(new Version(6,4,2,0), true, false);
        check(MultiplayerPlusGuard.Inspect(new[] { good }, out detail) == PlusEnvironment.OutsideRoom, "Installed Multiplayer+ outside room preserves Solo/Practice");
        var manager = good.GetType("BeatSaberPlus_Multiplayer.Network.NetworkManager");
        manager.GetField("Room").SetValue(null, Activator.CreateInstance(good.GetType("BeatSaberPlus_Multiplayer.Network.Types.RoomData")));
        check(MultiplayerPlusGuard.Inspect(new[] { good }, out detail) == PlusEnvironment.InRoom, "Existing room classified as Multiplayer+ before binding");
        foreach (bool multiplayerEnabled in new[] { false, true })
        {
            var config = new LakiConfig { EnableLaki = true, EnableInMultiplayer = multiplayerEnabled,
                TestMode = LakiTestMode.ForceSecret, SessionsWithoutSecret = 73 };
            bindings = 0;
            check(GameplayInstallation.TryInstall(config.EnableLaki, config.EnableInMultiplayer,
                () => MultiplayerPlusGuard.Inspect(new[] { good }, out detail), isPlus => { check(isPlus, "Room classified as Plus in session argument"); bindings++; }) == multiplayerEnabled, "Multiplayer+ setting gates binding");
            check(bindings == (multiplayerEnabled ? 1 : 0) && config.TestMode == LakiTestMode.ForceSecret && config.SessionsWithoutSecret == 73, "Installer never changes Pity or one-shot state");
        }
        manager.GetField("Room").SetValue(null, null);
        check(MultiplayerPlusGuard.Inspect(new[] { good }, out detail) == PlusEnvironment.OutsideRoom, "Leaving room restores Solo eligibility");
        check(MultiplayerPlusGuard.Inspect(new[] { Fixture(new Version(6,4,3,0), true, false) }, out detail) == PlusEnvironment.Unknown, "Unverified version fails closed");
        check(MultiplayerPlusGuard.Inspect(new[] { Fixture(new Version(6,4,2,0), false, false) }, out detail) == PlusEnvironment.Unknown, "Missing API fails closed");
        check(MultiplayerPlusGuard.Inspect(new[] { good, good }, out detail) == PlusEnvironment.Unknown, "Ambiguous duplicate assembly fails closed");
        bindings = 0;
        try {
            GameplayInstallation.TryInstall(true, false, () => MultiplayerPlusGuard.Inspect(new[] { Fixture(new Version(6,4,2,0), true, true) }, out detail), isPlus => bindings++);
            check(false, "Throwing getter must reach exception boundary");
        } catch (TargetInvocationException e) { check(e.InnerException is InvalidOperationException && bindings == 0, "Probe failure cannot register binding"); }
        int errors = Plugin.Log.Errors;
        GameplayInstallation.Install(null, isPlus => bindings++);
        check(Plugin.Log.Errors == errors + 1 && bindings == 0, "Installer boundary contains failure and records exception");
        Console.WriteLine("Multiplayer installer policy: OFF/no binding, active room/config gated, unknown/no binding, outside room/Solo, no Pity or Test consumption: PASS.");
    }
    private static Assembly Fixture(Version version, bool api, bool throws)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("BeatSaberPlus_Multiplayer") { Version = version }, AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Guard fixture only");
        var room = module.DefineType("BeatSaberPlus_Multiplayer.Network.Types.RoomData", TypeAttributes.Public).CreateType();
        var type = module.DefineType("BeatSaberPlus_Multiplayer.Network.NetworkManager", TypeAttributes.Abstract | TypeAttributes.Sealed);
        var field = type.DefineField("Room", room, FieldAttributes.Public | FieldAttributes.Static);
        if (api)
        {
            var property = type.DefineProperty("RoomData", PropertyAttributes.None, room, Type.EmptyTypes);
            var method = type.DefineMethod("get_RoomData", MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.SpecialName, room, Type.EmptyTypes);
            var il = method.GetILGenerator();
            if (throws) { il.Emit(OpCodes.Newobj, typeof(InvalidOperationException).GetConstructor(Type.EmptyTypes)); il.Emit(OpCodes.Throw); }
            else { il.Emit(OpCodes.Ldsfld, field); il.Emit(OpCodes.Ret); }
            property.SetGetMethod(method);
        }
        type.CreateType();
        return assembly;
    }
}

