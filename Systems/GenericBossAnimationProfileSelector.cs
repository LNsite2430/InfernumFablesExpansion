using Microsoft.Xna.Framework;
using Terraria;

namespace InfernumFablesExpansion.Systems;

internal static class GenericBossAnimationProfileSelector
{
    internal readonly record struct AnimationProfile(
        int DelayTicks,
        int Duration,
        float EdgeOpacity,
        float NameBrighten,
        float NameSecondaryBrighten,
        float TitleBrightness);

    public static AnimationProfile Select(NPC npc, Color accentColor)
    {
        string lowerName = npc.FullName.ToLowerInvariant();

        if (ContainsAny(lowerName, "worm", "wyrm", "serpent", "scourge", "devour", "eater"))
        {
            return new AnimationProfile(
                DelayTicks: 16,
                Duration: 132,
                EdgeOpacity: 0.52f,
                NameBrighten: 0.68f,
                NameSecondaryBrighten: 0.12f,
                TitleBrightness: 0.95f);
        }

        if (ContainsAny(lowerName, "moon", "void", "eidolon", "leviathan", "titan", "coloss", "goliath") || npc.lifeMax >= 200000)
        {
            return new AnimationProfile(
                DelayTicks: 24,
                Duration: 150,
                EdgeOpacity: 0.58f,
                NameBrighten: 0.72f,
                NameSecondaryBrighten: 0.24f,
                TitleBrightness: 1f);
        }

        if (ContainsAny(lowerName, "ares", "artemis", "apollo", "thanatos", "mech", "destroyer", "prime", "probe"))
        {
            return new AnimationProfile(
                DelayTicks: 18,
                Duration: 108,
                EdgeOpacity: 0.62f,
                NameBrighten: 0.55f,
                NameSecondaryBrighten: 0.1f,
                TitleBrightness: 0.92f);
        }

        if (ContainsAny(lowerName, "twins", "brothers", "sisters", "guardians"))
        {
            return new AnimationProfile(
                DelayTicks: 16,
                Duration: 118,
                EdgeOpacity: 0.5f,
                NameBrighten: 0.63f,
                NameSecondaryBrighten: 0.16f,
                TitleBrightness: 0.94f);
        }

        if (ContainsAny(lowerName, "mage", "cultist", "sorcer", "witch", "phantom", "spirit", "polter", "signus"))
        {
            return new AnimationProfile(
                DelayTicks: 14,
                Duration: 126,
                EdgeOpacity: 0.46f,
                NameBrighten: 0.66f,
                NameSecondaryBrighten: 0.22f,
                TitleBrightness: 0.97f);
        }

        if (ContainsAny(lowerName, "queen", "bee", "hive", "wasp", "hornet", "fishron", "shark", "duke", "dragon", "wyvern"))
        {
            return new AnimationProfile(
                DelayTicks: 14,
                Duration: 114,
                EdgeOpacity: 0.49f,
                NameBrighten: 0.61f,
                NameSecondaryBrighten: 0.14f,
                TitleBrightness: 0.93f);
        }

        if (npc.width >= 180 || npc.height >= 180)
        {
            return new AnimationProfile(
                DelayTicks: 20,
                Duration: 138,
                EdgeOpacity: 0.56f,
                NameBrighten: 0.7f,
                NameSecondaryBrighten: 0.2f,
                TitleBrightness: 0.98f);
        }

        return new AnimationProfile(
            DelayTicks: 10,
            Duration: 114,
            EdgeOpacity: 0.45f,
            NameBrighten: 0.6f,
            NameSecondaryBrighten: 0.18f,
            TitleBrightness: 0.9f);
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        foreach (string value in values)
        {
            if (text.Contains(value))
                return true;
        }

        return false;
    }
}
