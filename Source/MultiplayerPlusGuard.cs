using System;
using System.Reflection;

namespace Laki
{
    internal enum PlusEnvironment { Absent, OutsideRoom, InRoom, Unknown }

    internal static class MultiplayerPlusGuard
    {
        // Only inspect an already loaded assembly. No hard dependency, instance creation,
        // room state traversal, network events, or calls into Multiplayer+ gameplay.
        internal static PlusEnvironment Inspect(Assembly[] assemblies, out string detail)
        {
            detail = "Multiplayer+ absent";
            Assembly found = null;
            foreach (var assembly in assemblies)
            {
                if (assembly.GetName().Name != "BeatSaberPlus_Multiplayer") continue;
                if (found != null) { detail = "Multiple Multiplayer+ assemblies"; return PlusEnvironment.Unknown; }
                found = assembly;
            }
            if (found == null) return PlusEnvironment.Absent;
            if (found.GetName().Version != new Version(6, 4, 2, 0))
            { detail = "Unverified Multiplayer+ version: " + found.GetName().Version; return PlusEnvironment.Unknown; }
            var type = found.GetType("BeatSaberPlus_Multiplayer.Network.NetworkManager", false);
            var property = type?.GetProperty("RoomData", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var getter = property?.GetGetMethod(true);
            if (getter == null || !getter.IsStatic || getter.GetParameters().Length != 0 ||
                property.PropertyType.FullName != "BeatSaberPlus_Multiplayer.Network.Types.RoomData")
            { detail = "Multiplayer+ RoomData API unavailable"; return PlusEnvironment.Unknown; }
            // Verified 6.4.2 IL: get_RoomData is exactly ldsfld m_RoomData; ret.
            bool inRoom = getter.Invoke(null, null) != null;
            detail = "Multiplayer+ 6.4.2 " + (inRoom ? "room present" : "outside room");
            return inRoom ? PlusEnvironment.InRoom : PlusEnvironment.OutsideRoom;
        }
    }
}
