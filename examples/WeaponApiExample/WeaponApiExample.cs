
using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using ULTRADROID.Weapon.API;

namespace ULTRADROID.WeaponApiExample
{
    [BepInPlugin(
        "somerandommaid.ultradroid.weaponapiexample",
        "ULTRADROID Weapon API Example",
        "1.1.0")]
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

                // Additional attack 3: a stronger, slower melee attack.
                standard.AddAttack(
                    new WeaponAttackDefinition(
                        "heavy_slash",
                        WeaponAttackKind.Melee)
                    {
                        Damage = 40f,
                        Range = 4f,
                        CooldownSeconds = 0.9f,
                        Execute = context =>
                        {
                            Log.LogInfo(
                                "Heavy slash callback invoked for " +
                                context.WeaponId + " / " +
                                context.VariantId + ".");
                        }
                    });

                // Additional attack 4: a custom chain-pull action.
                standard.AddAttack(
                    new WeaponAttackDefinition(
                        "chain_pull",
                        WeaponAttackKind.Custom)
                    {
                        Range = 12f,
                        CooldownSeconds = 2f,
                        Execute = context =>
                        {
                            Log.LogInfo(
                                "Chain pull callback invoked for " +
                                context.WeaponId + " / " +
                                context.VariantId + ".");
                        }
                    });

                // Additional attack 5: an area attack.
                standard.AddAttack(
                    new WeaponAttackDefinition(
                        "spin_attack",
                        WeaponAttackKind.Area)
                    {
                        Damage = 20f,
                        Range = 4f,
                        CooldownSeconds = 2.5f,
                        Execute = context =>
                        {
                            Log.LogInfo(
                                "Spin attack callback invoked for " +
                                context.WeaponId + " / " +
                                context.VariantId + ".");
                        }
                    });

                blades.AddVariant(standard);

                // Preserve the legacy weapon-level attack properties.
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

                    WeaponVariantDefinition registeredStandard =
                        registered.GetVariant("standard");

                    if (registeredStandard == null)
                    {
                        Log.LogError(
                            "The standard variant could not be retrieved.");
                    }
                    else
                    {
                        Log.LogInfo(
                            "Standard variant: " +
                            registeredStandard.DisplayName);

                        Log.LogInfo(
                            "Total attacks on standard variant: " +
                            registeredStandard.Attacks.Count);

                        foreach (WeaponAttackDefinition attack
                            in registeredStandard.Attacks)
                        {
                            Log.LogInfo(
                                "Attack: " + attack.Id +
                                " | Kind: " + attack.Kind +
                                " | Damage: " + attack.Damage +
                                " | Range: " + attack.Range +
                                " | Cooldown: " +
                                attack.CooldownSeconds +
                                "s | Projectile speed: " +
                                attack.ProjectileSpeed);
                        }

                        string[] expectedAttackIds =
                        {
                            "chain_slash",
                            "chain_throw",
                            "heavy_slash",
                            "chain_pull",
                            "spin_attack"
                        };

                        foreach (string attackId in expectedAttackIds)
                        {
                            if (registeredStandard.GetAttack(attackId) == null)
                            {
                                Log.LogError(
                                    "Attack lookup failed: " + attackId);
                            }
                            else
                            {
                                Log.LogInfo(
                                    "Attack lookup successful: " + attackId);
                            }
                        }
                    }
                }
                else
                {
                    Log.LogError(
                        "Weapon registration completed, but lookup failed.");
                }

                Log.LogInfo(
                    "Registration test complete. Attack definitions and " +
                    "lookup are being tested; attack callbacks are still " +
                    "not connected to game input, damage, or projectiles.");
            }
            catch (Exception ex)
            {
                Log.LogError(
                    "Weapon API example failed: " + ex);
            }
        }
    }
}
