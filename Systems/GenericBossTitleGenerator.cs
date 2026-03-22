using Microsoft.Xna.Framework;
using Terraria;

namespace InfernumFablesExpansion.Systems;

internal static class GenericBossTitleGenerator
{
    public static string GetTitle(NPC npc, Color accentColor)
    {
        string name = npc.FullName;
        string lowerName = name.ToLowerInvariant();

        string prefix = GetPrefix(lowerName, accentColor);
        string suffix = GetSuffix(lowerName, npc);
        return $"{prefix} {suffix}";
    }

    private static string GetPrefix(string lowerName, Color accentColor)
    {
        if (ContainsAny(lowerName, "brim", "infer", "fire", "flame", "dragon", "yhar"))
            return "The Infernal";

        if (ContainsAny(lowerName, "ice", "frost", "cryo", "snow"))
            return "The Frozen";

        if (ContainsAny(lowerName, "storm", "thunder", "electric", "tempest"))
            return "The Tempestuous";

        if (ContainsAny(lowerName, "void", "astr", "cosmic", "star", "moon", "eclipse", "signus"))
            return "The Voidborn";

        if (ContainsAny(lowerName, "plague", "venom", "toxic", "acid", "pest"))
            return "The Pestilent";

        if (ContainsAny(lowerName, "holy", "profan", "guardian", "sun", "seraph"))
            return "The Blazing";

        if (ContainsAny(lowerName, "old", "ancient", "forgotten", "eidolon"))
            return "The Primordial";

        if (ContainsAny(lowerName, "ghost", "phantom", "spirit", "wraith", "polter"))
            return "The Restless";

        if (ContainsAny(lowerName, "mech", "ares", "artemis", "apollo", "thanatos", "destroyer", "prime"))
            return "The Adamant";

        Vector3 hsl = Main.rgbToHsl(accentColor);
        float hue = hsl.X;
        float saturation = hsl.Y;
        float lightness = hsl.Z;

        if (saturation < 0.18f)
            return lightness < 0.4f ? "The Umbral" : "The Pallid";

        if (hue < 0.04f || hue >= 0.94f)
            return "The Crimson";

        if (hue < 0.12f)
            return "The Emberborn";

        if (hue < 0.19f)
            return "The Golden";

        if (hue < 0.35f)
            return "The Verdant";

        if (hue < 0.48f)
            return "The Tidal";

        if (hue < 0.62f)
            return "The Azure";

        if (hue < 0.78f)
            return "The Arcane";

        return "The Eldritch";
    }

    private static string GetSuffix(string lowerName, NPC npc)
    {
        if (ContainsAny(lowerName, "worm", "wyrm", "serpent", "scourge", "eater", "devour"))
            return "Serpent";

        if (ContainsAny(lowerName, "eye", "retinazer", "spazmatism"))
            return "Gaze";

        if (ContainsAny(lowerName, "brain", "mind"))
            return "Consciousness";

        if (ContainsAny(lowerName, "slime", "gel"))
            return "Monstrosity";

        if (ContainsAny(lowerName, "queen", "bee", "hive", "hornet", "wasp"))
            return "Swarm Sovereign";

        if (ContainsAny(lowerName, "skele", "bone", "skull"))
            return "Bone Tyrant";

        if (ContainsAny(lowerName, "plant", "bloom", "flower", "thorn"))
            return "Bloom";

        if (ContainsAny(lowerName, "ghost", "phantom", "spirit", "wraith", "polter"))
            return "Apparition";

        if (ContainsAny(lowerName, "levi", "duke", "shark", "fish", "aqua", "tide", "ocean", "sea"))
            return "Tide Tyrant";

        if (ContainsAny(lowerName, "plague", "venom", "toxic", "acid", "pest"))
            return "Harbinger";

        if (ContainsAny(lowerName, "void", "astr", "cosmic", "star", "moon", "eclipse", "signus"))
            return "Omen";

        if (ContainsAny(lowerName, "guardian", "sentinel", "watcher"))
            return "Sentinel";

        if (ContainsAny(lowerName, "mech", "ares", "artemis", "apollo", "thanatos", "destroyer", "prime"))
            return "War Machine";

        if (ContainsAny(lowerName, "dragon", "wyvern", "drake"))
            return "Overlord";

        if (ContainsAny(lowerName, "giant", "goliath", "coloss", "titan"))
            return "Colossus";

        if (npc.lifeMax > 50000)
            return "Cataclysm";

        if (npc.realLife == npc.whoAmI || npc.boss)
            return "Tyrant";

        return "Behemoth";
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
