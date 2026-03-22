using Terraria;

namespace InfernumFablesExpansion.Systems;

internal static class GenericBossDisplayNameResolver
{
    public static string GetDisplayName(NPC npc)
    {
        string infernumName = InfernumIntroLookup.GetMatchedBossName(npc);
        if (!string.IsNullOrWhiteSpace(infernumName))
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
    private static string NormalizeSpacing(string value)
    {
        return string.Join(" ", value.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries));
    }
}
