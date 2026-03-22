using System;
using System.Collections;
using System.Reflection;
using Terraria.ModLoader;

namespace InfernumFablesExpansion.Systems;

internal static class InfernumFablesReflection
{
    private static FieldInfo specsByNpcTypeField;

    public static void Load()
    {
        specsByNpcTypeField = null;

        if (!ModLoader.TryGetMod("InfernumFables", out Mod infernumFables))
            return;

        Type registryType = infernumFables.Code?.GetType("InfernumFables.InfernumBossIntroRegistry");
        specsByNpcTypeField = registryType?.GetField("SpecsByNpcType", BindingFlags.NonPublic | BindingFlags.Static);
    }

    public static void Unload()
    {
        specsByNpcTypeField = null;
    }

    public static bool HasCustomIntro(int npcType)
    {
        if (specsByNpcTypeField?.GetValue(null) is not IDictionary dictionary)
            return false;

        return dictionary.Contains(npcType);
    }
}
