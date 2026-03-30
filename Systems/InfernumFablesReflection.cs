using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Terraria.Localization;
using Terraria.ModLoader;

namespace InfernumFablesExpansion.Systems;

internal static class InfernumFablesReflection
{
    private static FieldInfo specsByNpcTypeField;
    private static FieldInfo infernumIntroKeyField;
    private static IDictionary specsByNpcTypeDictionary;

    public static void Load()
    {
        specsByNpcTypeField = null;
        specsByNpcTypeDictionary = null;

        if (!ModLoader.TryGetMod("InfernumFables", out Mod infernumFables))
            return;

        Type registryType = infernumFables.Code?.GetType("InfernumFables.InfernumBossIntroRegistry");
        specsByNpcTypeField = registryType?.GetField("SpecsByNpcType", BindingFlags.NonPublic | BindingFlags.Static);
        specsByNpcTypeDictionary = specsByNpcTypeField?.GetValue(null) as IDictionary;
        Type introSpecType = infernumFables.Code?.GetType("InfernumFables.InfernumBossIntroRegistry+IntroSpec");
        infernumIntroKeyField = introSpecType?.GetField("InfernumIntroKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    }

    public static void Unload()
    {
        specsByNpcTypeField = null;
        infernumIntroKeyField = null;
        specsByNpcTypeDictionary = null;
    }

    public static bool HasCustomIntro(int npcType)
    {
        if (specsByNpcTypeDictionary is not IDictionary dictionary)
            return false;

        return dictionary.Contains(npcType);
    }

    public static void SuppressExoMechCustomIntros()
    {
        specsByNpcTypeDictionary = specsByNpcTypeField?.GetValue(null) as IDictionary;
        if (specsByNpcTypeDictionary is not IDictionary dictionary)
            return;

        if (!ModLoader.TryGetMod("CalamityMod", out Mod calamity))
            return;

        RemoveRegistryEntry(dictionary, calamity, "AresBody");
        RemoveRegistryEntry(dictionary, calamity, "Artemis");
        RemoveRegistryEntry(dictionary, calamity, "Apollo");
        RemoveRegistryEntry(dictionary, calamity, "ThanatosHead");
        RemoveRegistryEntry(dictionary, calamity, "ExoMechdusaHead");
    }

    public static void SuppressSlimeGodCoreCustomIntro()
    {
        specsByNpcTypeDictionary = specsByNpcTypeField?.GetValue(null) as IDictionary;
        if (specsByNpcTypeDictionary is not IDictionary dictionary)
            return;

        if (!ModLoader.TryGetMod("CalamityMod", out Mod calamity))
            return;

        RemoveRegistryEntry(dictionary, calamity, "SlimeGodCore");
    }

    public static void SuppressAllCustomIntros()
    {
        specsByNpcTypeDictionary = specsByNpcTypeField?.GetValue(null) as IDictionary;
        if (specsByNpcTypeDictionary is not IDictionary dictionary)
            return;

        dictionary.Clear();
    }

    public static void SuppressMismatchedRepresentativeIntros()
    {
        specsByNpcTypeDictionary = specsByNpcTypeField?.GetValue(null) as IDictionary;
        if (specsByNpcTypeDictionary is not IDictionary dictionary || infernumIntroKeyField is null)
            return;

        List<object> removalKeys = new();

        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is not int npcType || entry.Value is null)
                continue;

            if (NPCLoader.GetNPC(npcType) is not ModNPC modNpc)
                continue;

            if (infernumIntroKeyField.GetValue(entry.Value) is not string infernumIntroKey || string.IsNullOrWhiteSpace(infernumIntroKey))
                continue;

            string introBossName = GetIntroBossName(infernumIntroKey);
            if (string.IsNullOrWhiteSpace(introBossName))
                continue;

            if (!ShouldSuppressRepresentativeEntry(modNpc.Name, introBossName, infernumIntroKey))
                continue;

            removalKeys.Add(entry.Key);
        }

        foreach (object key in removalKeys)
            dictionary.Remove(key);
    }

    private static void RemoveRegistryEntry(IDictionary dictionary, Mod calamity, string npcName)
    {
        if (!calamity.TryFind<ModNPC>(npcName, out ModNPC modNpc))
            return;

        if (dictionary.Contains(modNpc.Type))
            dictionary.Remove(modNpc.Type);
    }

    private static string GetIntroBossName(string infernumIntroKey)
    {
        string raw = Language.GetTextValue($"Mods.InfernumMode.IntroScreen.{infernumIntroKey}.TextToDisplay");
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        string[] lines = raw
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return lines.Length >= 2 ? lines[1] : "";
    }

    private static bool ShouldSuppressRepresentativeEntry(string npcInternalName, string introBossName, string infernumIntroKey)
    {
        string[] npcTokens = TokenizeIdentifier(npcInternalName);
        string[] introTokens = TokenizeIdentifier(introBossName);

        if (npcTokens.Length == 0 || introTokens.Length == 0)
            return false;

        int overlaps = CountOverlaps(npcTokens, introTokens);
        if (overlaps > 0)
            return false;

        string compactNpc = string.Concat(npcTokens);
        string compactIntro = string.Concat(introTokens);
        if (compactNpc.Contains(compactIntro, StringComparison.OrdinalIgnoreCase) ||
            compactIntro.Contains(compactNpc, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string lowerNpcName = npcInternalName.ToLowerInvariant();
        if (lowerNpcName.Contains("commander") ||
            lowerNpcName.Contains("core") ||
            lowerNpcName.Contains("hive") ||
            lowerNpcName.Contains("body") ||
            lowerNpcName.Contains("head") ||
            lowerNpcName.Contains("start"))
        {
            return false;
        }

        string lowerKey = infernumIntroKey.ToLowerInvariant();
        return lowerKey.Contains("twins") ||
               lowerKey.Contains("draedon") ||
               lowerKey.Contains("arsenal") ||
               lowerKey.Contains("mech");
    }

    private static string[] TokenizeIdentifier(string text)
    {
        List<string> tokens = new();
        StringBuilder current = new();
        char previous = '\0';

        foreach (char c in text)
        {
            bool boundary =
                current.Length > 0 &&
                char.IsLetterOrDigit(c) &&
                char.IsUpper(c) &&
                char.IsLetterOrDigit(previous) &&
                (char.IsLower(previous) || char.IsDigit(previous));

            if (boundary)
                FlushToken(tokens, current);

            if (char.IsLetterOrDigit(c))
            {
                current.Append(char.ToLowerInvariant(c));
            }
            else
            {
                FlushToken(tokens, current);
            }

            previous = c;
        }

        FlushToken(tokens, current);
        return tokens.ToArray();
    }

    private static void FlushToken(List<string> tokens, StringBuilder current)
    {
        if (current.Length == 0)
            return;

        string token = current.ToString();
        current.Clear();

        if (token is "the" or "of" or "and" or "a" or "an")
            return;

        tokens.Add(token);
    }

    private static int CountOverlaps(string[] left, string[] right)
    {
        int overlaps = 0;

        foreach (string leftToken in left)
        {
            foreach (string rightToken in right)
            {
                if (leftToken == rightToken)
                {
                    overlaps++;
                    break;
                }
            }
        }

        return overlaps;
    }
}
