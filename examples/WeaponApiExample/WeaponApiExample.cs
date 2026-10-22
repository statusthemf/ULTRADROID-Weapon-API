using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using ULTRADROID.Weapon.API;

namespace ULTRADROID.WeaponApiExample
{
    [BepInPlugin(
        "somerandommaid.ultradroid.weaponapiexample",
        "ULTRADROID Weapon API Example",
        "1.0.0")]
    public sealed class WeaponApiExamplePlugin : BasePlugin
    {
        public override void Load()
        {
            Log.LogInfo("ULTRADROID Weapon API Example loading.");

            try
            {
                WeaponDefinition blades = new WeaponDefinition(
                    "chaos_blades",
                    "Blades of Chaos")
                {
                    Category = "Melee",
                    PreferredSlot = -1
                };

                WeaponVariantDefinition standard =
                    new WeaponVariantDefinition(
                        "standard",
                        "Standard Blades")
                    {
                        PrimaryAttack = new WeaponAttackDefinition(
                            "chain_slash",
                            WeaponAttackKind.Melee)
                        {
                            Damage = 25f,
                            Range = 3.5f,
                            CooldownSeconds = 0.45f,
                            Execute = context =>
                            {
                                Log.LogInfo(
                                    "Primary attack callback invoked for " +
                                    context.WeaponId + " / " +
                                    context.VariantId + ".");
                            }
                        },

                        AlternateAttack = new WeaponAttackDefinition(
                            "chain_throw",
                            WeaponAttackKind.Projectile)
                        {
                            Damage = 40f,
                            Range = 15f,
                            ProjectileSpeed = 20f,
                            CooldownSeconds = 1.5f,
                            Execute = context =>
                            {
                                Log.LogInfo(
                                    "Alternate attack callback invoked for " +
                                    context.WeaponId + " / " +
                                    context.VariantId + ".");
                            }
                        }
                    };

                blades.AddVariant(standard);

                blades.PrimaryAttack = standard.PrimaryAttack;
                blades.AlternateAttack = standard.AlternateAttack;

                WeaponRegistry.Register(blades);

                if (WeaponRegistry.TryGet(
                    "chaos_blades",
                    out WeaponDefinition registered))
                {
                    Log.LogInfo(
                        "Registered weapon: " + registered.DisplayName +
                        " (ID: " + registered.Id + ").");

                    Log.LogInfo(
                        "Variants: " + registered.Variants.Count +
                        "; primary attack: " +
                        (registered.PrimaryAttack?.Id ?? "none") +
                        "; alternate attack: " +
                        (registered.AlternateAttack?.Id ?? "none") + ".");
                }
                else
                {
                    Log.LogError(
                        "Weapon registration completed, but lookup failed.");
                }

                Log.LogInfo(
                    "Registration test complete. Attack callbacks are " +
                    "not connected to game input, damage, or projectiles yet.");
            }
            catch (Exception ex)
            {
                Log.LogError(
                    "Weapon API example failed: " + ex);
            }
        }
    }
}
