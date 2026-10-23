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
"1.3.0")]
[BepInDependency(
ULTRADROIDControls.PluginGuid,
BepInDependency.DependencyFlags.HardDependency)]
public sealed class WeaponApiExamplePlugin : BasePlugin
{
private ButtonHandle weapon7Button;
private ButtonHandle weapon8Button;

    public override void Load()
    {
        Log.LogInfo(
            "ULTRADROID Weapon API Example loading.");

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

        // Variant 1: Standard Blades.
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
                            "Standard primary callback: " +
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
                            "Standard alternate callback: " +
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
                        "Heavy slash callback: " +
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
                        "Chain pull callback: " +
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
                        "Spin attack callback: " +
                        context.WeaponId + " / " +
                        context.VariantId);
                }
            });

        blades.AddVariant(standard);

        // Variant 2: Heavy Blades.
        // Its attack definitions are independent from Standard.
        WeaponVariantDefinition heavy =
            new WeaponVariantDefinition(
                "heavy",
                "Heavy Blades")
            {
                PrimaryAttack = new WeaponAttackDefinition(
                    "heavy_primary",
                    WeaponAttackKind.Melee)
                {
                    Damage = 65f,
                    Range = 4.5f,
                    CooldownSeconds = 1.1f,
                    Execute = context =>
                    {
                        Log.LogInfo(
                            "Heavy variant primary callback: " +
                            context.WeaponId + " / " +
                            context.VariantId);
                    }
                },

                AlternateAttack = new WeaponAttackDefinition(
                    "heavy_shockwave",
                    WeaponAttackKind.Area)
                {
                    Damage = 50f,
                    Range = 7f,
                    CooldownSeconds = 2.2f,
                    Execute = context =>
                    {
                        Log.LogInfo(
                            "Heavy variant alternate callback: " +
                            context.WeaponId + " / " +
                            context.VariantId);
                    }
                }
            };

        heavy.AddAttack(
            new WeaponAttackDefinition(
                "ground_slam",
                WeaponAttackKind.Area)
            {
                Damage = 80f,
                Range = 6f,
                CooldownSeconds = 3f,
                Execute = context =>
                {
                    Log.LogInfo(
                        "Ground slam callback: " +
                        context.WeaponId + " / " +
                        context.VariantId);
                }
            });

        blades.AddVariant(heavy);

        // Legacy weapon-level attacks remain compatible.
        blades.PrimaryAttack = standard.PrimaryAttack;
        blades.AlternateAttack = standard.AlternateAttack;

        WeaponRegistry.Register(blades);

        Log.LogInfo(
            "Registered weapon: " +
            blades.DisplayName + " (" + blades.Id + ").");

        Log.LogInfo(
            "Blades of Chaos variants registered: " +
            blades.Variants.Count);
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
                            "Custom Gun primary callback: " +
                            context.WeaponId + " / " +
                            context.VariantId);
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
                            "Custom Gun alternate callback: " +
                            context.WeaponId + " / " +
                            context.VariantId);
                    }
                }
            };

        customGun.AddVariant(standard);

        customGun.PrimaryAttack = standard.PrimaryAttack;
        customGun.AlternateAttack = standard.AlternateAttack;

        WeaponRegistry.Register(customGun);

        Log.LogInfo(
            "Registered weapon: " +
            customGun.DisplayName + " (" +
            customGun.Id + ").");
    }

    private void StartWeaponRuntime()
    {
        // WeaponGameRuntime.Start requires BOTH arguments.
        WeaponGameRuntime.Start(
            message => Log.LogInfo(message),
            () => AddComponent<WeaponGameRuntimeBehaviour>());

        Log.LogInfo(
            "Weapon runtime startup requested.");
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

            Log.LogInfo(
                "W7 selection button registered.");
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

            Log.LogInfo(
                "W8 selection button registered.");
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
            "Verified weapon: " +
            registered.DisplayName +
            " (ID: " + registered.Id + ").");

        Log.LogInfo(
            "Variant count: " +
            registered.Variants.Count);

        Log.LogInfo(
            "Legacy primary attack: " +
            (registered.PrimaryAttack?.Id ?? "none"));

        Log.LogInfo(
            "Legacy alternate attack: " +
            (registered.AlternateAttack?.Id ?? "none"));

        VerifyVariant(registered, "standard");

        VerifyVariant(registered, "heavy");

        Log.LogInfo(
            "Definition verification finished. " +
            "Attack callbacks only log messages; they do not " +
            "yet implement actual damage, projectiles, " +
            "animation, or attack input.");
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
                "Variant lookup failed: " + variantId);
            return;
        }

        Log.LogInfo(
            "Verified variant: " +
            variant.DisplayName +
            " (ID: " + variant.Id + ").");

        Log.LogInfo(
            "Variant attack count: " +
            variant.Attacks.Count);

        foreach (WeaponAttackDefinition attack in variant.Attacks)
        {
            Log.LogInfo(
                "Attack: " + attack.Id +
                " | Kind: " + attack.Kind +
                " | Damage: " + attack.Damage +
                " | Range: " + attack.Range +
                " | Cooldown: " + attack.CooldownSeconds +
                "s | Projectile speed: " +
                attack.ProjectileSpeed);
        }
    }
}

}
