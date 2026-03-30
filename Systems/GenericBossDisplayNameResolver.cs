using Terraria;

namespace InfernumFablesExpansion.Systems;

internal static class GenericBossDisplayNameResolver
{
    internal static bool TryGetIndependentBossId(NPC npc, out string bossId)
    {
        bossId = "";

        if (npc.type is Terraria.ID.NPCID.Retinazer)
        {
            bossId = "retinazer";
            return true;
        }

        if (npc.type is Terraria.ID.NPCID.Spazmatism)
        {
            bossId = "spazmatism";
            return true;
        }

        string[] modNameTokens = TokenizeIdentifier(npc.ModNPC?.Name ?? "");
        if (TryGetSingleMatchingId(modNameTokens, out bossId))
            return true;

        string[] fullNameTokens = TokenizeIdentifier(npc.FullName);
        if (TryGetSingleMatchingId(fullNameTokens, out bossId))
            return true;

        return false;
    }

    public static string GetDisplayName(NPC npc)
    {
        if (TryGetPreferredIndividualDisplayName(npc, out string preferredDisplayName))
            return preferredDisplayName;

        string infernumName = InfernumIntroLookup.GetMatchedBossName(npc);
        if (!string.IsNullOrWhiteSpace(infernumName) && ShouldUseInfernumMatchedNameForDisplay(npc, infernumName))
            return infernumName;

        string fullName = npc.FullName;

        if (TryGetCompositeBossName(fullName, out string compositeName))
            return compositeName;

        return fullName;
    }

    private static bool TryGetCompositeBossName(string fullName, out string displayName)
    {
        displayName = fullName;

        string normalized = NormalizeSpacing(fullName);
        if (TryTrimSegmentSuffix(normalized, out string trimmedName))
        {
            displayName = trimmedName;
            return true;
        }

        string lower = normalized.ToLowerInvariant();

        string[] suffixes =
        {
            " core",
            " head",
            " body",
            " tail",
            " hand",
            " eye",
            " arm",
            " claw",
            " fist",
            " cannon",
            " laser",
            " gauntlet"
        };

        foreach (string suffix in suffixes)
        {
            if (!lower.EndsWith(suffix))
                continue;

            displayName = normalized[..^suffix.Length].TrimEnd();
            return !string.IsNullOrWhiteSpace(displayName);
        }

        string[] infixes =
        {
            " core of ",
            " hand of ",
            " eye of ",
            " head of ",
            " body of ",
            " tail of "
        };

        foreach (string infix in infixes)
        {
            int index = lower.IndexOf(infix, System.StringComparison.Ordinal);
            if (index < 0)
                continue;

            string root = normalized[(index + infix.Length)..].Trim();
            if (string.IsNullOrWhiteSpace(root))
                continue;

            displayName = root;
            return true;
        }

        return false;
    }

    private static bool TryTrimSegmentSuffix(string fullName, out string trimmedName)
    {
        trimmedName = "";
        string normalized = NormalizeSpacing(fullName);
        string lower = normalized.ToLowerInvariant();

        string[] markers =
        {
            "'s core",
            "'s hand",
            "'s head",
            "'s body",
            "'s tail",
            "'s eye",
            "'s arm",
            "'s claw",
            "'s fist",
            "'s cannon",
            "'s laser",
            "'s gauntlet",
            " core",
            " hand",
            " head",
            " body",
            " tail",
            " eye",
            " arm",
            " claw",
            " fist",
            " cannon",
            " laser",
            " gauntlet"
        };

        foreach (string marker in markers)
        {
            if (!lower.EndsWith(marker))
                continue;

            string candidate = normalized[..^marker.Length].TrimEnd(' ', '\'');
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            trimmedName = candidate;
            return true;
        }

        return false;
    }

    private static bool TryGetPreferredIndividualDisplayName(NPC npc, out string displayName)
    {
        displayName = "";

        if (!TryGetIndependentBossId(npc, out string bossId) || bossId is "retinazer" or "spazmatism")
            return false;

        string normalized = NormalizeSpacing(npc.FullName);
        displayName = string.IsNullOrWhiteSpace(normalized) ? npc.FullName : normalized;
        return true;
    }

    private static bool ShouldUseInfernumMatchedNameForDisplay(NPC npc, string infernumName)
    {
        if (string.IsNullOrWhiteSpace(infernumName))
            return false;

        string probe = $"{npc.ModNPC?.Name} {npc.FullName}".ToLowerInvariant();
        string normalizedMatchedName = NormalizeSpacing(infernumName).ToLowerInvariant();

        bool independentTwinLikeBoss =
            npc.type is Terraria.ID.NPCID.Retinazer or Terraria.ID.NPCID.Spazmatism ||
            probe.Contains("artemis") ||
            probe.Contains("apollo") ||
            probe.Contains("ares") ||
            probe.Contains("thanatos") ||
            probe.Contains("exomechdusa");

        if (!independentTwinLikeBoss)
            return true;

        string[] collectiveMarkers =
        {
            "twins",
            "exo twins",
            "exo mechs",
            "draedon",
            "arsenal",
            " and "
        };

        foreach (string marker in collectiveMarkers)
        {
            if (normalizedMatchedName.Contains(marker))
                return false;
        }

        return true;
    }

    private static string NormalizeSpacing(string value)
    {
        return string.Join(" ", value.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool TryGetSingleMatchingId(string[] tokens, out string bossId)
    {
        bossId = "";
        string[] ids =
        {
            "ares",
            "artemis",
            "apollo",
            "thanatos",
            "exomechdusa"
        };

        string match = "";
        int matchCount = 0;

        foreach (string id in ids)
        {
            foreach (string token in tokens)
            {
                if (token != id)
                    continue;

                match = id;
                matchCount++;
                break;
            }
        }

        if (matchCount != 1)
            return false;

        bossId = match;
        return true;
    }

    private static string[] TokenizeIdentifier(string text)
    {
        var tokens = new System.Collections.Generic.List<string>();
        var current = new System.Text.StringBuilder();
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
                current.Append(char.ToLowerInvariant(c));
            else
                FlushToken(tokens, current);

            previous = c;
        }

        FlushToken(tokens, current);
        return tokens.ToArray();
    }

    private static void FlushToken(System.Collections.Generic.List<string> tokens, System.Text.StringBuilder current)
    {
        if (current.Length == 0)
            return;

        tokens.Add(current.ToString());
        current.Clear();
    }
}
