
using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using ULTRADROID.API;
using ULTRADROID.Weapon.API;
using UnityEngine;

namespace ULTRADROID.WeaponApiExample
{
    [BepInPlugin(
        "somerandommaid.ultradroid.weaponapiexample",
        "ULTRADROID Weapon API Example",
        "1.2.0")]
    [BepInDependency(
        ULTRADROIDControls.PluginGuid,
        BepInDependency.DependencyFlags.HardDependency)]
    public sealed class WeaponApiExamplePlugin : BasePlugin
    {
        private ButtonHandle weapon7Button;
        private ButtonHandle weapon8Button;
        public override void Load()
        {
            Log.LogInfo("ULTRADROID Weapon API Example loading.");

            try
            {
                RegisterBladesOfChaos();
                RegisterCustomGun();
                StartWeaponRuntime();
                RegisterWeaponButton();
                RegisterWeapon8Button();
                VerifyWeaponRegistration();
            }
            catch (Exception ex)
            {
                Log.LogError(
                    "Weapon API example failed: " + ex);
            }
        }

        private void RegisterBladesOfChaos()
        {
            WeaponDefinition blades = new WeaponDefinition(
                "chaos_blades",
                "Blades of Chaos")
            {
                Category = "Melee",
                PreferredSlot = 7
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
                                "Primary attack callback invoked: " +
                                context.WeaponId + " / " +
                                context.VariantId);
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
                                "Alternate attack callback invoked: " +
                                context.WeaponId + " / " +
                                context.VariantId);
                        }
                    }
                };

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
                            "Heavy slash callback invoked: " +
                            context.WeaponId + " / " +
                            context.VariantId);
                    }
                });

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
                            "Chain pull callback invoked: " +
                            context.WeaponId + " / " +
                            context.VariantId);
                    }
                });

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
                            "Spin attack callback invoked: " +
                            context.WeaponId + " / " +
                            context.VariantId);
                    }
                });

            blades.AddVariant(standard);

            // Keep compatibility with weapon-level attack properties.
            blades.PrimaryAttack = standard.PrimaryAttack;
            blades.AlternateAttack = standard.AlternateAttack;

            WeaponRegistry.Register(blades);

            Log.LogInfo(
                "Registered weapon definition: " +
                blades.DisplayName + " (" + blades.Id + ").");
        }

        private void RegisterCustomGun()
        {
            WeaponDefinition customGun = new WeaponDefinition(
                "custom_gun",
                "Custom Gun")
            {
                Category = "Ranged",
                PreferredSlot = 8
            };

            WeaponVariantDefinition standard =
                new WeaponVariantDefinition(
                    "standard",
                    "Standard Custom Gun")
                {
                    PrimaryAttack = new WeaponAttackDefinition(
                        "custom_gun_primary",
                        WeaponAttackKind.Projectile)
                    {
                        Damage = 30f,
                        Range = 50f,
                        ProjectileSpeed = 40f,
                        CooldownSeconds = 0.25f,
                        Execute = context =>
                        {
                            Log.LogInfo(
                                "Custom Gun primary callback invoked: " +
                                context.WeaponId + " / " + context.VariantId);
                        }
                    },

                    AlternateAttack = new WeaponAttackDefinition(
                        "custom_gun_alternate",
                        WeaponAttackKind.Custom)
                    {
                        CooldownSeconds = 1f,
                        Execute = context =>
                        {
                            Log.LogInfo(
                                "Custom Gun alternate callback invoked: " +
                                context.WeaponId + " / " + context.VariantId);
                        }
                    }
                };

            customGun.AddVariant(standard);
            customGun.PrimaryAttack = standard.PrimaryAttack;
            customGun.AlternateAttack = standard.AlternateAttack;

            WeaponRegistry.Register(customGun);

            Log.LogInfo(
                "Registered weapon definition: " +
                customGun.DisplayName + " (" + customGun.Id + ").");
        }

        private void StartWeaponRuntime()
        {
            WeaponGameRuntime.Start(
                message => Log.LogInfo(message),
                () => AddComponent<WeaponGameRuntimeBehaviour>());

            Log.LogInfo("Weapon runtime startup requested.");
        }

        private void RegisterWeaponButton()
        {
            try
            {
                weapon7Button = ULTRADROIDControls.AddButton(
                    new ButtonConfig
                    {
                        Id = "ULTRADROID.WeaponApi.SelectChaosBlades",
                        Label = "W7",
                        PositionOffset = new Vector2(120f, 90f),
                        ScaleMultiplier = 1f,
                        ApplyTint = false,
                        OnClick = SelectChaosBlades
                    });

                Log.LogInfo("W7 selection button registered.");
            }
            catch (Exception ex)
            {
                Log.LogError(
                    "Could not register the W7 button: " + ex);
            }
        }

        private void SelectChaosBlades()
        {
            if (!WeaponGameRuntime.SelectWeapon("chaos_blades"))
            {
                Log.LogWarning(
                    "Blades of Chaos is not ready in slot 7 yet.");
            }
        }

        private void RegisterWeapon8Button()
        {
            try
            {
                weapon8Button = ULTRADROIDControls.AddButton(
                    new ButtonConfig
                    {
                        Id = "ULTRADROID.WeaponApi.SelectCustomGun",
                        Label = "W8",
                        PositionOffset = new Vector2(120f, 160f),
                        ScaleMultiplier = 1f,
                        ApplyTint = false,
                        OnClick = SelectCustomGun
                    });

                Log.LogInfo("W8 selection button registered.");
            }
            catch (Exception ex)
            {
                Log.LogError(
                    "Could not register the W8 button: " + ex);
            }
        }

        private void SelectCustomGun()
        {
            if (!WeaponGameRuntime.SelectWeapon("custom_gun"))
            {
                Log.LogWarning(
                    "Custom Gun is not ready in slot 8 yet.");
            }
        }

        private void VerifyWeaponRegistration()
        {
            if (!WeaponRegistry.TryGet(
                "chaos_blades",
                out WeaponDefinition registered))
            {
                Log.LogError(
                    "Weapon registration verification failed.");
                return;
            }

            Log.LogInfo(
                "Verified weapon: " + registered.DisplayName +
                " (ID: " + registered.Id + ").");

            Log.LogInfo(
                "Variants: " + registered.Variants.Count +
                "; primary: " +
                (registered.PrimaryAttack?.Id ?? "none") +
                "; alternate: " +
                (registered.AlternateAttack?.Id ?? "none") + ".");

            WeaponVariantDefinition standard =
                registered.GetVariant("standard");

            if (standard == null)
            {
                Log.LogError(
                    "The standard variant could not be retrieved.");
                return;
            }

            Log.LogInfo(
                "Verified variant: " + standard.DisplayName);

            Log.LogInfo(
                "Attack count: " + standard.Attacks.Count);

            string[] expectedAttackIds =
            {
                "chain_slash",
                "chain_throw",
                "heavy_slash",
                "chain_pull",
                "spin_attack"
            };

            foreach (WeaponAttackDefinition attack in standard.Attacks)
            {
                Log.LogInfo(
                    "Attack: " + attack.Id +
                    " | Kind: " + attack.Kind +
                    " | Damage: " + attack.Damage +
                    " | Range: " + attack.Range +
                    " | Cooldown: " + attack.CooldownSeconds +
                    "s | Projectile speed: " + attack.ProjectileSpeed);
            }

            foreach (string attackId in expectedAttackIds)
            {
                if (standard.GetAttack(attackId) == null)
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

            Log.LogInfo(
                "Definition verification finished. Attack callbacks " +
                "are test callbacks; they do not yet implement actual " +
                "game damage, projectiles, or attack input.");
        }
    }
}
