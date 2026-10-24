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
        "1.3.3")]
    [BepInDependency(
        ULTRADROIDControls.PluginGuid,
        BepInDependency.DependencyFlags.HardDependency)]
    public sealed class WeaponApiExamplePlugin : BasePlugin
    {
        private ButtonHandle weapon7Button;
        private ButtonHandle weapon8Button;
        private ButtonHandle weapon9Button;

        public override void Load()
        {
            Log.LogInfo("ULTRADROID Weapon API Example loading.");

            try
            {
                RegisterBladesOfChaos();
                RegisterCustomGun();
                RegisterGoldRevolverTest();

                StartWeaponRuntime();

                RegisterWeaponButton();
                RegisterWeapon8Button();
                RegisterWeapon9Button();

                VerifyWeaponRegistration();
            }
            catch (Exception ex)
            {
                Log.LogError("Weapon API example failed: " + ex);
            }
        }

        private WeaponModelDefinition CreateModel(
            string path,
            Vector3 position,
            Vector3 rotation,
            Vector3 scale)
        {
            return new WeaponModelDefinition
            {
                Path = path,
                Position = position,
                RotationEuler = rotation,
                Scale = scale,
                LoadTextures = true,
                CreateColliders = false,
                HideTemplateMeshes = true
            };
        }

        private void RegisterBladesOfChaos()
        {
            WeaponDefinition blades = new WeaponDefinition(
                "chaos_blades",
                "Blades of Chaos")
            {
                Category = "Melee",
                PreferredSlot = 7,

                // Optional shared model for the base weapon.
                // Put ULTRADROID_BLADES_OF_CHAOS.glb in the plugin folder.
                Model = CreateModel(
                    "ULTRADROID_BLADES_OF_CHAOS.glb",
                    new Vector3(0f, 0f, 1f),
                    new Vector3(90f, 0f, -90f),
                    new Vector3(150f, 150f, 150f))
            };

            WeaponVariantDefinition blue =
                new WeaponVariantDefinition("blue", "Blue Blades")
                {
                    Color = WeaponVariantColor.Blue,
                    CustomTint = new Color(0.10f, 0.35f, 1.0f),
                    IconTint = new Color(0.10f, 0.35f, 1.0f),
                    WheelTintEnabled = true,
                    WheelTint = new Color(0.10f, 0.35f, 1.0f),

                    PrimaryAttack = new WeaponAttackDefinition(
                        "blue_chain_slash",
                        WeaponAttackKind.Melee)
                    {
                        Damage = 25f,
                        Range = 3.5f,
                        CooldownSeconds = 0.45f,
                        ProjectileSpeed = 0f,
                        Execute = context =>
                            Log.LogInfo(
                                "Blue Blades primary callback: " +
                                context.WeaponId + " / " + context.VariantId)
                    },

                    AlternateAttack = new WeaponAttackDefinition(
                        "blue_chain_throw",
                        WeaponAttackKind.Projectile)
                    {
                        Damage = 40f,
                        Range = 15f,
                        CooldownSeconds = 1.5f,
                        ProjectileSpeed = 20f,
                        Execute = context =>
                            Log.LogInfo(
                                "Blue Blades alternate callback: " +
                                context.WeaponId + " / " + context.VariantId)
                    }
                };

            blue.AddAttack(new WeaponAttackDefinition(
                "blue_spin",
                WeaponAttackKind.Area)
            {
                Damage = 20f,
                Range = 4f,
                CooldownSeconds = 2.5f,
                Execute = context =>
                    Log.LogInfo(
                        "Blue Blades extra attack callback: " +
                        context.WeaponId + " / " + context.VariantId)
            });

            WeaponVariantDefinition green =
                new WeaponVariantDefinition("green", "Green Blades")
                {
                    Color = WeaponVariantColor.Green,
                    CustomTint = new Color(0.10f, 1.0f, 0.20f),
                    IconTint = new Color(0.10f, 1.0f, 0.20f),
                    WheelTintEnabled = true,
                    WheelTint = new Color(0.10f, 1.0f, 0.20f),

                    PrimaryAttack = new WeaponAttackDefinition(
                        "green_chain_slash",
                        WeaponAttackKind.Melee)
                    {
                        Damage = 28f,
                        Range = 3.6f,
                        CooldownSeconds = 0.42f,
                        Execute = context =>
                            Log.LogInfo(
                                "Green Blades primary callback: " +
                                context.WeaponId + " / " + context.VariantId)
                    },

                    AlternateAttack = new WeaponAttackDefinition(
                        "green_chain_throw",
                        WeaponAttackKind.Projectile)
                    {
                        Damage = 42f,
                        Range = 15.5f,
                        CooldownSeconds = 1.4f,
                        ProjectileSpeed = 21f,
                        Execute = context =>
                            Log.LogInfo(
                                "Green Blades alternate callback: " +
                                context.WeaponId + " / " + context.VariantId)
                    }
                };

            green.AddAttack(new WeaponAttackDefinition(
                "green_chain_pull",
                WeaponAttackKind.Custom)
            {
                Damage = 0f,
                Range = 12f,
                CooldownSeconds = 2f,
                Execute = context =>
                    Log.LogInfo(
                        "Green Blades extra attack callback: " +
                        context.WeaponId + " / " + context.VariantId)
            });

            WeaponVariantDefinition red =
                new WeaponVariantDefinition("red", "Red Blades")
                {
                    Color = WeaponVariantColor.Red,
                    CustomTint = new Color(1.0f, 0.08f, 0.08f),
                    IconTint = new Color(1.0f, 0.08f, 0.08f),
                    WheelTintEnabled = true,
                    WheelTint = new Color(1.0f, 0.08f, 0.08f),

                    PrimaryAttack = new WeaponAttackDefinition(
                        "red_chain_slash",
                        WeaponAttackKind.Melee)
                    {
                        Damage = 32f,
                        Range = 3.8f,
                        CooldownSeconds = 0.5f,
                        Execute = context =>
                            Log.LogInfo(
                                "Red Blades primary callback: " +
                                context.WeaponId + " / " + context.VariantId)
                    },

                    AlternateAttack = new WeaponAttackDefinition(
                        "red_chain_throw",
                        WeaponAttackKind.Projectile)
                    {
                        Damage = 48f,
                        Range = 16f,
                        CooldownSeconds = 1.6f,
                        ProjectileSpeed = 22f,
                        Execute = context =>
                            Log.LogInfo(
                                "Red Blades alternate callback: " +
                                context.WeaponId + " / " + context.VariantId)
                    }
                };

            red.AddAttack(new WeaponAttackDefinition(
                "red_ground_slam",
                WeaponAttackKind.Area)
            {
                Damage = 55f,
                Range = 6f,
                CooldownSeconds = 2.8f,
                Execute = context =>
                    Log.LogInfo(
                        "Red Blades extra attack callback: " +
                        context.WeaponId + " / " + context.VariantId)
            });

            WeaponVariantDefinition blackBlades =
                new WeaponVariantDefinition("black_blades", "Black Blades")
                {
                    Color = WeaponVariantColor.Default,
                    CustomTint = new Color(0.025f, 0.025f, 0.035f),
                    IconTint = new Color(0.025f, 0.025f, 0.035f),
                    WheelTintEnabled = true,
                    WheelTint = new Color(0.025f, 0.025f, 0.035f),

                    PrimaryAttack = new WeaponAttackDefinition(
                        "black_chain_slash",
                        WeaponAttackKind.Melee)
                    {
                        Damage = 35f,
                        Range = 4f,
                        CooldownSeconds = 0.55f,
                        Execute = context =>
                            Log.LogInfo(
                                "Black Blades primary callback: " +
                                context.WeaponId + " / " + context.VariantId)
                    },

                    AlternateAttack = new WeaponAttackDefinition(
                        "black_chain_throw",
                        WeaponAttackKind.Projectile)
                    {
                        Damage = 50f,
                        Range = 17f,
                        CooldownSeconds = 1.7f,
                        ProjectileSpeed = 23f,
                        Execute = context =>
                            Log.LogInfo(
                                "Black Blades alternate callback: " +
                                context.WeaponId + " / " + context.VariantId)
                    }
                };

            blackBlades.AddAttack(new WeaponAttackDefinition(
                "black_spin",
                WeaponAttackKind.Area)
            {
                Damage = 35f,
                Range = 4.5f,
                CooldownSeconds = 2.6f,
                Execute = context =>
                    Log.LogInfo(
                        "Black Blades extra attack callback: " +
                        context.WeaponId + " / " + context.VariantId)
            });

            // A variant may override the base model with its own GLB.
            // Leave Model null to use the shared weapon model.
            blades.AddVariant(blue);
            blades.AddVariant(green);
            blades.AddVariant(red);
            blades.AddVariant(blackBlades);

            blades.PrimaryAttack = blue.PrimaryAttack;
            blades.AlternateAttack = blue.AlternateAttack;

            WeaponRegistry.Register(blades);

            Log.LogInfo(
                "Registered Blades of Chaos with Blue, Green, Red and Black variants.");
            Log.LogInfo(
                "Blades of Chaos variant count: " + blades.Variants.Count);
        }

        private void RegisterCustomGun()
        {
            WeaponDefinition customGun = new WeaponDefinition(
                "custom_gun",
                "Custom Gun")
            {
                Category = "Ranged",
                PreferredSlot = 8,

                Model = CreateModel(
                    "ULTRADROID_CUSTOM_GUN.glb",
                    new Vector3(0f, 0f, 1f),
                    new Vector3(0f, 0f, 0f),
                    new Vector3(1f, 1f, 1f))
            };

            WeaponVariantDefinition pink =
                new WeaponVariantDefinition("pink", "Pink Custom Gun")
                {
                    Color = WeaponVariantColor.Default,
                    CustomTint = new Color(1.0f, 0.15f, 0.55f),
                    IconTint = new Color(1.0f, 0.15f, 0.55f),
                    WheelTintEnabled = true,
                    WheelTint = new Color(1.0f, 0.15f, 0.55f),

                    PrimaryAttack = new WeaponAttackDefinition(
                        "pink_primary",
                        WeaponAttackKind.Projectile)
                    {
                        Damage = 30f,
                        Range = 50f,
                        CooldownSeconds = 0.25f,
                        ProjectileSpeed = 40f,
                        Execute = context =>
                            Log.LogInfo(
                                "Pink Custom Gun primary callback: " +
                                context.WeaponId + " / " + context.VariantId)
                    },

                    AlternateAttack = new WeaponAttackDefinition(
                        "pink_alt",
                        WeaponAttackKind.Custom)
                    {
                        Damage = 20f,
                        Range = 30f,
                        CooldownSeconds = 1f,
                        Execute = context =>
                            Log.LogInfo(
                                "Pink Custom Gun alternate callback: " +
                                context.WeaponId + " / " + context.VariantId)
                    }
                };

            pink.AddAttack(new WeaponAttackDefinition(
                "pink_burst",
                WeaponAttackKind.Projectile)
            {
                Damage = 15f,
                Range = 45f,
                CooldownSeconds = 1.8f,
                Execute = context =>
                    Log.LogInfo(
                        "Pink Custom Gun extra attack callback: " +
                        context.WeaponId + " / " + context.VariantId)
            });

            WeaponVariantDefinition purple =
                new WeaponVariantDefinition("purple", "Purple Custom Gun")
                {
                    Color = WeaponVariantColor.Default,
                    CustomTint = new Color(0.55f, 0.12f, 0.95f),
                    IconTint = new Color(0.55f, 0.12f, 0.95f),
                    WheelTintEnabled = true,
                    WheelTint = new Color(0.55f, 0.12f, 0.95f),

                    PrimaryAttack = new WeaponAttackDefinition(
                        "purple_primary",
                        WeaponAttackKind.Projectile)
                    {
                        Damage = 34f,
                        Range = 52f,
                        CooldownSeconds = 0.28f,
                        ProjectileSpeed = 42f,
                        Execute = context =>
                            Log.LogInfo(
                                "Purple Custom Gun primary callback: " +
                                context.WeaponId + " / " + context.VariantId)
                    },

                    AlternateAttack = new WeaponAttackDefinition(
                        "purple_alt",
                        WeaponAttackKind.Custom)
                    {
                        Damage = 25f,
                        Range = 32f,
                        CooldownSeconds = 1.1f,
                        Execute = context =>
                            Log.LogInfo(
                                "Purple Custom Gun alternate callback: " +
                                context.WeaponId + " / " + context.VariantId)
                    }
                };

            purple.AddAttack(new WeaponAttackDefinition(
                "purple_pulse",
                WeaponAttackKind.Area)
            {
                Damage = 22f,
                Range = 5f,
                CooldownSeconds = 2f,
                Execute = context =>
                    Log.LogInfo(
                        "Purple Custom Gun extra attack callback: " +
                        context.WeaponId + " / " + context.VariantId)
            });

            WeaponVariantDefinition white =
                new WeaponVariantDefinition("white", "White Custom Gun")
                {
                    Color = WeaponVariantColor.Default,
                    CustomTint = Color.white,
                    IconTint = Color.white,
                    WheelTintEnabled = true,
                    WheelTint = Color.white,

                    PrimaryAttack = new WeaponAttackDefinition(
                        "white_primary",
                        WeaponAttackKind.Projectile)
                    {
                        Damage = 28f,
                        Range = 55f,
                        CooldownSeconds = 0.22f,
                        ProjectileSpeed = 45f,
                        Execute = context =>
                            Log.LogInfo(
                                "White Custom Gun primary callback: " +
                                context.WeaponId + " / " + context.VariantId)
                    },

                    AlternateAttack = new WeaponAttackDefinition(
                        "white_alt",
                        WeaponAttackKind.Custom)
                    {
                        Damage = 18f,
                        Range = 35f,
                        CooldownSeconds = 0.9f,
                        Execute = context =>
                            Log.LogInfo(
                                "White Custom Gun alternate callback: " +
                                context.WeaponId + " / " + context.VariantId)
                    }
                };

            white.AddAttack(new WeaponAttackDefinition(
                "white_ray",
                WeaponAttackKind.Projectile)
            {
                Damage = 40f,
                Range = 60f,
                CooldownSeconds = 2.2f,
                Execute = context =>
                    Log.LogInfo(
                        "White Custom Gun extra attack callback: " +
                        context.WeaponId + " / " + context.VariantId)
            });

            WeaponVariantDefinition blackGun =
                new WeaponVariantDefinition("black_gun", "Black Custom Gun")
                {
                    Color = WeaponVariantColor.Default,
                    CustomTint = new Color(0.025f, 0.025f, 0.035f),
                    IconTint = new Color(0.025f, 0.025f, 0.035f),
                    WheelTintEnabled = true,
                    WheelTint = new Color(0.025f, 0.025f, 0.035f),

                    PrimaryAttack = new WeaponAttackDefinition(
                        "black_gun_primary",
                        WeaponAttackKind.Projectile)
                    {
                        Damage = 38f,
                        Range = 48f,
                        CooldownSeconds = 0.32f,
                        ProjectileSpeed = 38f,
                        Execute = context =>
                            Log.LogInfo(
                                "Black Custom Gun primary callback: " +
                                context.WeaponId + " / " + context.VariantId)
                    },

                    AlternateAttack = new WeaponAttackDefinition(
                        "black_gun_alt",
                        WeaponAttackKind.Custom)
                    {
                        Damage = 30f,
                        Range = 28f,
                        CooldownSeconds = 1.3f,
                        Execute = context =>
                            Log.LogInfo(
                                "Black Custom Gun alternate callback: " +
                                context.WeaponId + " / " + context.VariantId)
                    }
                };

            blackGun.AddAttack(new WeaponAttackDefinition(
                "black_gun_blast",
                WeaponAttackKind.Area)
            {
                Damage = 35f,
                Range = 7f,
                CooldownSeconds = 2.4f,
                Execute = context =>
                    Log.LogInfo(
                        "Black Custom Gun extra attack callback: " +
                        context.WeaponId + " / " + context.VariantId)
            });

            customGun.AddVariant(pink);
            customGun.AddVariant(purple);
            customGun.AddVariant(white);
            customGun.AddVariant(blackGun);

            customGun.PrimaryAttack = pink.PrimaryAttack;
            customGun.AlternateAttack = pink.AlternateAttack;

            WeaponRegistry.Register(customGun);

            Log.LogInfo(
                "Registered Custom Gun with Pink, Purple, White and Black variants.");
            Log.LogInfo(
                "Custom Gun variant count: " + customGun.Variants.Count);
        }

        private void RegisterGoldRevolverTest()
        {
            WeaponDefinition goldRevolver = new WeaponDefinition(
                "gold_revolver_test",
                "Vanilla Revolver Gold Extension")
            {
                Category = "Ranged",
                ExtendExistingSlot = 1
            };

            WeaponVariantDefinition gold =
                new WeaponVariantDefinition("gold", "Gold Revolver")
                {
                    Color = WeaponVariantColor.Gold,
                    CustomTint = new Color(1.0f, 0.62f, 0.08f),
                    IconTint = new Color(1.0f, 0.62f, 0.08f),
                    WheelTintEnabled = true,
                    WheelTint = new Color(1.0f, 0.62f, 0.08f),

                    // Optional model override for the gold variant.
                    // Leave null if you only want to use native weapon geometry.
                    Model = null,

                    PrimaryAttack = new WeaponAttackDefinition(
                        "gold_revolver_primary",
                        WeaponAttackKind.Projectile)
                    {
                        Damage = 35f,
                        Range = 50f,
                        ProjectileSpeed = 40f,
                        CooldownSeconds = 0.25f,
                        Execute = context =>
                            Log.LogInfo(
                                "Gold Revolver primary callback: " +
                                context.WeaponId + " / " + context.VariantId)
                    },

                    AlternateAttack = new WeaponAttackDefinition(
                        "gold_revolver_alternate",
                        WeaponAttackKind.Custom)
                    {
                        Damage = 45f,
                        Range = 20f,
                        CooldownSeconds = 1f,
                        Execute = context =>
                            Log.LogInfo(
                                "Gold Revolver alternate callback: " +
                                context.WeaponId + " / " + context.VariantId)
                    }
                };

            gold.AddAttack(new WeaponAttackDefinition(
                "gold_revolver_ricochet",
                WeaponAttackKind.Projectile)
            {
                Damage = 50f,
                Range = 60f,
                ProjectileSpeed = 55f,
                CooldownSeconds = 1.8f,
                Execute = context =>
                    Log.LogInfo(
                        "Gold Revolver ricochet callback: " +
                        context.WeaponId + " / " + context.VariantId)
            });

            goldRevolver.AddVariant(gold);
            goldRevolver.PrimaryAttack = gold.PrimaryAttack;
            goldRevolver.AlternateAttack = gold.AlternateAttack;

            WeaponRegistry.Register(goldRevolver);

            Log.LogInfo(
                "Registered Gold as an additional variant in the vanilla Revolver family (slot 1).");
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
                weapon7Button = ULTRADROIDControls.AddButton(new ButtonConfig
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
                Log.LogError("Could not register W7 button: " + ex);
            }
        }

        private void SelectChaosBlades()
        {
            if (!WeaponGameRuntime.SelectWeapon("chaos_blades"))
                Log.LogWarning("Blades of Chaos is not ready in slot 7 yet.");
        }

        private void RegisterWeapon8Button()
        {
            try
            {
                weapon8Button = ULTRADROIDControls.AddButton(new ButtonConfig
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
                Log.LogError("Could not register W8 button: " + ex);
            }
        }

        private void SelectCustomGun()
        {
            if (!WeaponGameRuntime.SelectWeapon("custom_gun"))
                Log.LogWarning("Custom Gun is not ready in slot 8 yet.");
        }

        private void RegisterWeapon9Button()
        {
            try
            {
                weapon9Button = ULTRADROIDControls.AddButton(new ButtonConfig
                {
                    Id = "ULTRADROID.WeaponApi.SelectGoldRevolverTest",
                    Label = "Gold",
                    PositionOffset = new Vector2(120f, 230f),
                    ScaleMultiplier = 1f,
                    ApplyTint = false,
                    OnClick = SelectGoldRevolverTest
                });

                Log.LogInfo("Gold selection button registered.");
            }
            catch (Exception ex)
            {
                Log.LogError("Could not register Gold button: " + ex);
            }
        }

        private void SelectGoldRevolverTest()
        {
            if (!WeaponGameRuntime.SelectVariant(
                "gold_revolver_test",
                "gold"))
            {
                Log.LogWarning(
                    "Gold Revolver variant is not ready in the vanilla Revolver family yet.");
            }
        }

        private void VerifyWeaponRegistration()
        {
            VerifyWeapon(
                "chaos_blades",
                "blue",
                "green",
                "red",
                "black_blades");

            VerifyWeapon(
                "custom_gun",
                "pink",
                "purple",
                "white",
                "black_gun");

            VerifyWeapon("gold_revolver_test", "gold");

            Log.LogInfo(
                "Definition verification finished. Attack callbacks are metadata only; they do not implement actual damage, projectiles, animations, or attack input.");
        }

        private void VerifyWeapon(
            string weaponId,
            params string[] variantIds)
        {
            if (!WeaponRegistry.TryGet(
                weaponId,
                out WeaponDefinition registered))
            {
                Log.LogError(
                    "Weapon registration verification failed: " + weaponId);
                return;
            }

            Log.LogInfo(
                "Verified weapon: " + registered.DisplayName +
                " (ID: " + registered.Id +
                "). Variant count: " + registered.Variants.Count);

            Log.LogInfo(
                "Legacy primary: " +
                (registered.PrimaryAttack?.Id ?? "none") +
                "; alternate: " +
                (registered.AlternateAttack?.Id ?? "none"));

            foreach (string variantId in variantIds)
                VerifyVariant(registered, variantId);
        }

        private void VerifyVariant(
            WeaponDefinition weapon,
            string variantId)
        {
            WeaponVariantDefinition variant =
                weapon.GetVariant(variantId);

            if (variant == null)
            {
                Log.LogError(
                    "Variant lookup failed: " +
                    weapon.Id + "/" + variantId);
                return;
            }

            Log.LogInfo(
                "Verified variant: " + variant.DisplayName +
                " (ID: " + variant.Id +
                "); native color=" + variant.Color +
                "; custom tint=" +
                (variant.CustomTint.HasValue
                    ? variant.CustomTint.Value.ToString()
                    : "none") +
                "; attacks=" + variant.Attacks.Count +
                "; model=" +
                (variant.Model?.Path ?? weapon.Model?.Path ?? "none"));

            foreach (WeaponAttackDefinition attack in variant.Attacks)
            {
                Log.LogInfo(
                    "Attack: " + attack.Id +
                    " | Kind: " + attack.Kind +
                    " | Damage: " + attack.Damage +
                    " | Range: " + attack.Range +
                    " | Cooldown: " + attack.CooldownSeconds + "s" +
                    " | Projectile speed: " + attack.ProjectileSpeed);
            }
        }
    }
}
