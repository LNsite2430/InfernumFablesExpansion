using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.Localization;

namespace InfernumFablesExpansion.Systems;

internal static class InfernumIntroLookup
{
    private static Dictionary<string, string> titlesByBossName;
    private static Dictionary<string, string> bossNamesByLookupKey;
    private static List<IntroEntry> introEntries;
    private static Dictionary<string, string> matchedBossNamesByLookupKey;

    public static void Clear()
    {
        titlesByBossName = null;
        bossNamesByLookupKey = null;
        introEntries = null;
        matchedBossNamesByLookupKey = null;
    }

    public static bool TryGetTitle(NPC npc, out string title)
    {
        title = "";
        EnsureCache();

        if (titlesByBossName is null || bossNamesByLookupKey is null || introEntries is null || matchedBossNamesByLookupKey is null)
            return false;

        string fullName = npc.FullName;
        string normalizedFullName = NormalizeName(fullName);
        if (TryGetExactMatch(normalizedFullName, out title, out string matchedBossName))
        {
            if (!ShouldAcceptMatchedBossNameForTitle(npc, matchedBossName))
                title = "";
            else
            {
            RememberMatchedBossName(normalizedFullName, matchedBossName);
            return true;
            }
        }

        string compactFullName = CompactName(fullName);
        if (TryGetExactMatch(compactFullName, out title, out matchedBossName))
        {
            if (!ShouldAcceptMatchedBossNameForTitle(npc, matchedBossName))
                title = "";
            else
            {
            RememberMatchedBossName(compactFullName, matchedBossName);
            return true;
            }
        }

        string internalName = npc.ModNPC?.Name ?? "";
        if (!string.IsNullOrWhiteSpace(internalName))
        {
            string normalizedInternalName = NormalizeName(internalName);
            if (TryGetExactMatch(normalizedInternalName, out title, out matchedBossName))
            {
                if (!ShouldAcceptMatchedBossNameForTitle(npc, matchedBossName))
                    title = "";
                else
                {
                RememberMatchedBossName(normalizedInternalName, matchedBossName);
                return true;
                }
            }

            string compactInternalName = CompactName(internalName);
            if (TryGetExactMatch(compactInternalName, out title, out matchedBossName))
            {
                if (!ShouldAcceptMatchedBossNameForTitle(npc, matchedBossName))
                    title = "";
                else
                {
                RememberMatchedBossName(compactInternalName, matchedBossName);
                return true;
                }
            }
        }

        foreach (string alias in GetLookupAliases(npc))
        {
            string normalizedAlias = NormalizeName(alias);
            if (TryGetExactMatch(normalizedAlias, out title, out matchedBossName))
            {
                if (!ShouldAcceptMatchedBossNameForTitle(npc, matchedBossName))
                    title = "";
                else
                {
                RememberMatchedBossName(normalizedFullName, matchedBossName);
                RememberMatchedBossName(compactFullName, matchedBossName);
                return true;
                }
            }

            string compactAlias = CompactName(alias);
            if (TryGetExactMatch(compactAlias, out title, out matchedBossName))
            {
                if (!ShouldAcceptMatchedBossNameForTitle(npc, matchedBossName))
                    title = "";
                else
                {
                RememberMatchedBossName(normalizedFullName, matchedBossName);
                RememberMatchedBossName(compactFullName, matchedBossName);
                return true;
                }
            }
        }

        int bestScore = 0;
        foreach (IntroEntry entry in introEntries)
        {
            int score = ScoreEntryMatch(entry, normalizedFullName, compactFullName, internalName, npc);
            if (score <= bestScore)
                continue;

            if (!ShouldAcceptMatchedBossNameForTitle(npc, entry.BossName))
                continue;

            bestScore = score;
            title = entry.Title;
            RememberMatchedBossName(entry.CompactName, entry.BossName);
        }

        return bestScore >= 3;
    }

    public static string GetMatchedBossName(NPC npc)
    {
        EnsureCache();

        if (matchedBossNamesByLookupKey is null)
            return "";

        string normalizedFullName = NormalizeName(npc.FullName);
        if (matchedBossNamesByLookupKey.TryGetValue(normalizedFullName, out string matchedName))
            return matchedName;

        string compactFullName = CompactName(npc.FullName);
        if (matchedBossNamesByLookupKey.TryGetValue(compactFullName, out matchedName))
            return matchedName;

        string internalName = npc.ModNPC?.Name ?? "";
        if (!string.IsNullOrWhiteSpace(internalName))
        {
            string normalizedInternalName = NormalizeName(internalName);
            if (matchedBossNamesByLookupKey.TryGetValue(normalizedInternalName, out matchedName))
                return matchedName;

            string compactInternalName = CompactName(internalName);
            if (matchedBossNamesByLookupKey.TryGetValue(compactInternalName, out matchedName))
                return matchedName;
        }

        foreach (string alias in GetLookupAliases(npc))
        {
            string normalizedAlias = NormalizeName(alias);
            if (matchedBossNamesByLookupKey.TryGetValue(normalizedAlias, out matchedName))
                return matchedName;

            string compactAlias = CompactName(alias);
            if (matchedBossNamesByLookupKey.TryGetValue(compactAlias, out matchedName))
                return matchedName;
        }

        if (TryGetTitle(npc, out _))
            return GetMatchedBossName(npc);

        return "";
    }

    private static bool ShouldAcceptMatchedBossNameForTitle(NPC npc, string matchedBossName)
    {
        if (string.IsNullOrWhiteSpace(matchedBossName))
            return false;

        if (!GenericBossDisplayNameResolver.TryGetIndependentBossId(npc, out string bossId))
            return true;

        if (bossId is "retinazer" or "spazmatism")
            return true;

        string normalized = NormalizeName(matchedBossName);
        string[] collectiveMarkers =
        {
            "draedon",
            "exo mechs",
            "arsenal",
            "twins",
            " and "
        };

        foreach (string marker in collectiveMarkers)
        {
            if (normalized.Contains(marker))
                return false;
        }

        return true;
    }

    private static void EnsureCache()
    {
        if (titlesByBossName is not null)
            return;

        titlesByBossName = new Dictionary<string, string>();
        bossNamesByLookupKey = new Dictionary<string, string>();
        introEntries = new List<IntroEntry>();
        matchedBossNamesByLookupKey = new Dictionary<string, string>();

        FieldInfo localizedTextsField = typeof(LanguageManager).GetField("_localizedTexts", BindingFlags.Instance | BindingFlags.NonPublic);
        if (localizedTextsField?.GetValue(LanguageManager.Instance) is not Dictionary<string, LocalizedText> localizedTexts)
            return;

        foreach (var pair in localizedTexts)
        {
            if (!pair.Key.StartsWith("Mods.InfernumMode.IntroScreen."))
                continue;

            if (!pair.Key.EndsWith(".TextToDisplay"))
                continue;

            string raw = pair.Value?.Value;
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            string[] lines = raw
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

            if (lines.Length < 2)
                continue;

            string title = lines[0];
            string bossName = lines[1];
            string normalizedName = NormalizeName(bossName);
            string compactName = CompactName(bossName);
            string[] tokens = Tokenize(bossName);

            if (!titlesByBossName.ContainsKey(normalizedName))
                titlesByBossName[normalizedName] = title;
            if (!bossNamesByLookupKey.ContainsKey(normalizedName))
                bossNamesByLookupKey[normalizedName] = bossName;

            if (!titlesByBossName.ContainsKey(compactName))
                titlesByBossName[compactName] = title;
            if (!bossNamesByLookupKey.ContainsKey(compactName))
                bossNamesByLookupKey[compactName] = bossName;

            RememberMatchedBossName(normalizedName, bossName);
            RememberMatchedBossName(compactName, bossName);

            introEntries.Add(new IntroEntry
            {
                Title = title,
                BossName = bossName,
                NormalizedName = normalizedName,
                CompactName = compactName,
                Tokens = tokens
            });
        }
    }

    private static string NormalizeName(string name)
    {
        return string.Join(" ", Tokenize(name));
    }

    private static string CompactName(string name)
    {
        string[] tokens = Tokenize(name);
        return string.Concat(tokens);
    }

    private static string[] Tokenize(string name)
    {
        List<string> tokens = new();
        StringBuilder current = new();

        foreach (char c in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                current.Append(c);
                continue;
            }

            FlushToken(tokens, current);
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

    private static int ScoreEntryMatch(IntroEntry entry, string normalizedFullName, string compactFullName, string internalName, NPC npc)
    {
        int score = 0;

        if (entry.CompactName == compactFullName)
            return 100;

        if (entry.NormalizedName == normalizedFullName)
            return 90;

        if (!string.IsNullOrWhiteSpace(internalName))
        {
            string normalizedInternalName = NormalizeName(internalName);
            string compactInternalName = CompactName(internalName);

            if (entry.CompactName == compactInternalName)
                return 85;

            if (entry.NormalizedName == normalizedInternalName)
                return 80;

            if (compactInternalName.Contains(entry.CompactName) || entry.CompactName.Contains(compactInternalName))
                score = System.Math.Max(score, 6);
        }

        if (compactFullName.Contains(entry.CompactName) || entry.CompactName.Contains(compactFullName))
            score = System.Math.Max(score, 5);

        foreach (string alias in GetLookupAliases(npc))
        {
            string normalizedAlias = NormalizeName(alias);
            string compactAlias = CompactName(alias);

            if (entry.CompactName == compactAlias)
                return 88;

            if (entry.NormalizedName == normalizedAlias)
                return 86;

            if (compactAlias.Contains(entry.CompactName) || entry.CompactName.Contains(compactAlias))
                score = System.Math.Max(score, 7);
        }

        int tokenMatches = CountTokenMatches(entry.Tokens, Tokenize(normalizedFullName));
        if (tokenMatches >= 2)
            score = System.Math.Max(score, tokenMatches + 1);

        return score;
    }

    private static IEnumerable<string> GetLookupAliases(NPC npc)
    {
        if (npc.type is NPCID.Retinazer or NPCID.Spazmatism)
        {
            yield return "The Twins";
            yield return "Twins";
        }

        if (npc.type is NPCID.WallofFlesh or NPCID.WallofFleshEye)
        {
            yield return "Wall of Flesh";
            yield return "The Wall of Flesh";
            yield return "WallofFlesh";
        }

        string probe = $"{npc.ModNPC?.Name} {npc.FullName}".ToLowerInvariant();

        if (probe.Contains("slimegodcore") || probe.Contains("ebonianpaladin") || probe.Contains("crimulanpaladin"))
        {
            yield return "Slime God";
            yield return "The Slime God";
        }

        if (probe.Contains("exomechdusa"))
        {
            yield return "Exo Mechs";
            yield return "The Exo Mechs";
            yield return "Draedon's Arsenal";
        }
    }

    private static bool TryGetExactMatch(string lookupKey, out string title, out string bossName)
    {
        title = "";
        bossName = "";

        if (titlesByBossName is null || bossNamesByLookupKey is null)
            return false;

        if (!titlesByBossName.TryGetValue(lookupKey, out title))
            return false;

        bossNamesByLookupKey.TryGetValue(lookupKey, out bossName);
        bossName ??= lookupKey;
        return true;
    }

    private static int CountTokenMatches(string[] left, string[] right)
    {
        int matches = 0;
        foreach (string leftToken in left)
        {
            foreach (string rightToken in right)
            {
                if (leftToken == rightToken)
                {
                    matches++;
                    break;
                }

                if (leftToken.Length >= 4 && rightToken.Length >= 4 &&
                    (leftToken.Contains(rightToken) || rightToken.Contains(leftToken)))
                {
                    matches++;
                    break;
                }
            }
        }

        return matches;
    }

    private static void RememberMatchedBossName(string lookupKey, string bossName)
    {
        if (matchedBossNamesByLookupKey is null || string.IsNullOrWhiteSpace(lookupKey) || string.IsNullOrWhiteSpace(bossName))
            return;

        matchedBossNamesByLookupKey[lookupKey] = bossName;
    }

    private sealed class IntroEntry
    {
        public string Title = "";
        public string BossName = "";
        public string NormalizedName = "";
        public string CompactName = "";
        public string[] Tokens = System.Array.Empty<string>();
    }
}
