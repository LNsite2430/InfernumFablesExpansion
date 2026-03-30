using Terraria.ModLoader;

namespace InfernumFablesExpansion.Systems;

public sealed class InfernumFablesExpansionSetupSystem : ModSystem
{
    public override void PostSetupContent()
    {
        if (InfernumFablesExpansionConfig.ShouldSuppressAllCustomIntros())
        {
            InfernumFablesReflection.SuppressAllCustomIntros();
            return;
        }

        InfernumFablesReflection.SuppressExoMechCustomIntros();
        InfernumFablesReflection.SuppressSlimeGodCoreCustomIntro();
        InfernumFablesReflection.SuppressMismatchedRepresentativeIntros();
    }
}
