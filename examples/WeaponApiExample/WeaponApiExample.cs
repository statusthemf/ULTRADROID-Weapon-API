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

            RegisterWeaponButtons();
            VerifyWeaponRegistration();
        }
        catch (Exception ex)
        {
            Log.LogError(
                "Weapon API example failed: " + ex);
        }
    }

    // --------------------------------------------------
    // WEAPON 7: BLADES OF CHAOS
    // --------------------------------------------------

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

        // Preserve compatibility with weapon-level attacks.
        blades.PrimaryAttack = standard.PrimaryAttack;
        blades.AlternateAttack = standard.AlternateAttack;

        WeaponRegistry.Register(blades);

        Log.LogInfo(
            "Registered weapon definition: " +
            blades.DisplayName +
            " (ID: " + blades.Id +
            ", requested slot: " +
            blades.PreferredSlot + ").");
    }

    // --------------------------------------------------
    // WEAPON 8: SECOND TEST WEAPON
    // --------------------------------------------------

    private void RegisterCustomGun()
    {
        WeaponDefinition customGun = new WeaponDefinition(
            "custom_gun",
            "Custom Gun")
        {
            Category = "Ranged",
            PreferredSlot = 8
        };

        customGun.PrimaryAttack =
            new WeaponAttackDefinition(
                "custom_shot",
                WeaponAttackKind.Projectile)
            {
                Damage = 35f,
                Range = 30f,
                ProjectileSpeed = 25f,
                CooldownSeconds = 0.8f,
                Execute = context =>
                {
                    Log.LogInfo(
                        "Custom Gun primary callback invoked: " +
                        context.WeaponId);
                }
            };

        customGun.AlternateAttack =
            new WeaponAttackDefinition(
                "charged_shot",
                WeaponAttackKind.Projectile)
            {
                Damage = 75f,
                Range = 40f,
                ProjectileSpeed = 35f,
                CooldownSeconds = 2f,
                Execute = context =>
                {
                    Log.LogInfo(
                        "Custom Gun alternate callback invoked: " +
                        context.WeaponId);
                }
            };

        WeaponRegistry.Register(customGun);

        Log.LogInfo(
            "Registered weapon definition: " +
            customGun.DisplayName +
            " (ID: " + customGun.Id +
            ", requested slot: " +
            customGun.PreferredSlot + ").");
    }

    // --------------------------------------------------
    // RUNTIME
    // --------------------------------------------------

    private void StartWeaponRuntime()
    {
        WeaponGameRuntime.Start(
            message => Log.LogInfo(message));

        Log.LogInfo(
            "Weapon runtime startup requested.");
    }

    // --------------------------------------------------
    // HUD BUTTONS
    // --------------------------------------------------

    private void RegisterWeaponButtons()
    {
        RegisterWeapon7Button();
        RegisterWeapon8Button();
    }

    private void RegisterWeapon7Button()
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
                "Could not register W7 button: " + ex);
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
                    PositionOffset = new Vector2(200f, 90f),
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
                "Could not register W8 button: " + ex);
        }
    }

    private void SelectChaosBlades()
    {
        if (!WeaponGameRuntime.SelectWeapon("chaos_blades"))
        {
            Log.LogWarning(
                "Blades of Chaos is not ready for selection.");
        }
    }

    private void SelectCustomGun()
    {
        if (!WeaponGameRuntime.SelectWeapon("custom_gun"))
        {
            Log.LogWarning(
                "Custom Gun is not ready for selection.");
        }
    }

    // --------------------------------------------------
    // REGISTRATION VERIFICATION
    // --------------------------------------------------

    private void VerifyWeaponRegistration()
    {
        VerifyBladesOfChaos();
        VerifyCustomGun();

        Log.LogInfo(
            "Weapon definition verification finished. " +
            "Attack callbacks are diagnostic only; they do not " +
            "yet implement actual game damage, projectiles, " +
            "or attack input.");
    }

    private void VerifyBladesOfChaos()
    {
        if (!WeaponRegistry.TryGet(
            "chaos_blades",
            out WeaponDefinition registered))
        {
            Log.LogError(
                "Blades of Chaos registration verification failed.");
            return;
        }

        Log.LogInfo(
            "Verified weapon: " +
            registered.DisplayName +
            " (ID: " + registered.Id +
            ", requested slot: " +
            registered.PreferredSlot + ").");

        Log.LogInfo(
            "Category: " + registered.Category +
            "; variants: " + registered.Variants.Count +
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

        VerifyAttacks(standard.Attacks, "Blades of Chaos");

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
            if (standard.GetAttack(attackId) == null)
            {
                Log.LogError(
                    "Blades attack lookup failed: " + attackId);
            }
            else
            {
                Log.LogInfo(
                    "Blades attack lookup successful: " +
                    attackId);
            }
        }
    }

    private void VerifyCustomGun()
    {
        if (!WeaponRegistry.TryGet(
            "custom_gun",
            out WeaponDefinition registered))
        {
            Log.LogError(
                "Custom Gun registration verification failed.");
            return;
        }

        Log.LogInfo(
            "Verified weapon: " +
            registered.DisplayName +
            " (ID: " + registered.Id +
            ", requested slot: " +
            registered.PreferredSlot + ").");

        VerifyAttacks(registered.Attacks, "Custom Gun");
    }

    private void VerifyAttacks(
        System.Collections.Generic.IReadOnlyList<
            WeaponAttackDefinition> attacks,
        string weaponName)
    {
        Log.LogInfo(
            weaponName + " attack count: " + attacks.Count);

        foreach (WeaponAttackDefinition attack in attacks)
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
