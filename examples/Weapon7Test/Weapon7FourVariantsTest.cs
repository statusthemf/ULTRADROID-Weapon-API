using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using ULTRADROID.API;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ULTRADROID.Weapon7Test
{
    [BepInPlugin(
        "somerandommaid.ultradroid.weapon7fourvariantstest",
        "ULTRADROID Weapon 7 Four Variants Test",
        "0.1.0")]
    [BepInDependency(
        ULTRADROIDControls.PluginGuid,
        BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Weapon7FourVariantsPlugin : BasePlugin
    {
        public override void Load()
        {
            Weapon7FourVariantsRuntime.Log =
                new ManualLogSourceProxy(Log);

            AddComponent<Weapon7FourVariantsRuntime>();

            Log.LogInfo(
                "[Weapon7FourVariants] Test plugin loaded.");
        }
    }

    public sealed class Weapon7FourVariantsRuntime : MonoBehaviour
    {
        internal static ManualLogSourceProxy Log;

        private readonly GameObject[] variants =
            new GameObject[4];

        private readonly bool[] buttonsRegistered =
            new bool[4];

        private object gunControl;
        private IntPtr lastGunPointer = IntPtr.Zero;
        private int lastCheckFrame = -120;

        public Weapon7FourVariantsRuntime(IntPtr ptr)
            : base(ptr) { }

        public void Update()
        {
            if (Log == null)
                return;

            string scene =
                SceneManager.GetActiveScene().name;

            if (scene == "Bootstrap" || scene == "Intro")
                return;

            RegisterButtons();

            if (Time.frameCount - lastCheckFrame < 120)
                return;

            lastCheckFrame = Time.frameCount;

            try
            {
                object found =
                    Weapon7Helpers.FindReadyGunControl();

                if (found == null)
                    return;

                IntPtr pointer =
                    Weapon7Helpers.GetPointer(found);

                if (pointer == IntPtr.Zero)
                    return;

                if (pointer != lastGunPointer)
                {
                    lastGunPointer = pointer;
                    gunControl = found;

                    for (int i = 0; i < variants.Length; i++)
                        variants[i] = null;

                    Log.Info(
                        "[Weapon7FourVariants] Found GunControl instance.");
                }
                else
                {
                    gunControl = found;
                }

                EnsureFourVariants();
            }
            catch (Exception ex)
            {
                Log.Error(
                    "[Weapon7FourVariants] Update failed: " +
                    Weapon7Helpers.Unwrap(ex));
            }
        }

        private void RegisterButtons()
        {
            for (int i = 0; i < 4; i++)
            {
                if (buttonsRegistered[i])
                    continue;

                int variantIndex = i + 1;

                try
                {
                    ULTRADROIDControls.AddButton(
                        new ButtonConfig
                        {
                            Id =
                                "ULTRADROID.Weapon7FourVariants.V" +
                                variantIndex,

                            Label = "W7V" + variantIndex,

                            PositionOffset = new Vector2(
                                120f, 135f + (i * 42f)),

                            ScaleMultiplier = 1f,
                            ApplyTint = false,

                            OnClick = () =>
                                SelectVariant(variantIndex)
                        });

                    buttonsRegistered[i] = true;

                    Log.Info(
                        "[Weapon7FourVariants] Registered V" +
                        variantIndex + " button.");
                }
                catch (Exception ex)
                {
                    Log.Error(
                        "[Weapon7FourVariants] Could not register V" +
                        variantIndex + " button: " +
                        Weapon7Helpers.Unwrap(ex));
                }
            }
        }

        private bool EnsureFourVariants()
        {
            object slots =
                Weapon7Helpers.GetMember(gunControl, "slots");

            if (slots == null ||
                Weapon7Helpers.Count(slots) < 7)
            {
                Log.Info(
                    "[Weapon7FourVariants] Waiting for Weapon 7 slot.");

                return false;
            }

            // Slot 7 is index 6 because C# collections are zero-based.
            object slot7 = Weapon7Helpers.Item(slots, 6);

            if (slot7 == null)
                return false;

            // Wait for the original Weapon7Test plugin.
            GameObject baseWeapon = FindByName(
                slot7, "ULTRADROID_TEST_WEAPON7");

            if (baseWeapon == null)
            {
                Log.Info(
                    "[Weapon7FourVariants] Waiting for " +
                    "ULTRADROID_TEST_WEAPON7 in slot 7.");

                return false;
            }

            variants[0] = baseWeapon;

            bool changed = false;

            for (int i = 1; i < 4; i++)
            {
                string objectName =
                    "ULTRADROID_TEST_WEAPON7_VARIANT_" +
                    (i + 1);

                GameObject existing =
                    FindByName(slot7, objectName);

                if (existing != null)
                {
                    variants[i] = existing;
                    continue;
                }

                GameObject variant =
                    UnityEngine.Object.Instantiate(
                        baseWeapon,
                        baseWeapon.transform.parent);

                if (variant == null)
                {
                    Log.Error(
                        "[Weapon7FourVariants] Failed to clone variant " +
                        (i + 1) + ".");

                    return false;
                }

                variant.name = objectName;
                variant.SetActive(false);

                bool descriptorAssigned =
                    Weapon7Helpers.AssignUniqueDescriptor(
                        variant,
                        "ULTRADROID Weapon 7 Variant " +
                        (i + 1),
                        Log);

                if (!descriptorAssigned)
                {
                    UnityEngine.Object.Destroy(variant);

                    Log.Error(
                        "[Weapon7FourVariants] Descriptor setup failed " +
                        "for variant " + (i + 1) + ".");

                    return false;
                }

                variants[i] = variant;

                Weapon7Helpers.Add(slot7, variant);

                changed = true;

                Log.Info(
                    "[Weapon7FourVariants] Created variant " +
                    (i + 1) +
                    " from the existing Weapon 7 template.");
            }

            if (changed)
            {
                try
                {
                    Weapon7Helpers.Invoke(
                        gunControl, "CalculateSlotCount");

                    Log.Info(
                        "[Weapon7FourVariants] CalculateSlotCount invoked.");
                }
                catch (Exception ex)
                {
                    Log.Warn(
                        "[Weapon7FourVariants] CalculateSlotCount failed: " +
                        Weapon7Helpers.Unwrap(ex));
                }
            }

            Log.Info(
                "[Weapon7FourVariants] Slot 7 variant count=" +
                Weapon7Helpers.Count(slot7) +
                "; four selectable entries are prepared.");

            return true;
        }

        private static GameObject FindByName(
            object collection,
            string objectName)
        {
            for (int i = 0;
                 i < Weapon7Helpers.Count(collection);
                 i++)
            {
                GameObject item =
                    Weapon7Helpers.Item(collection, i)
                    as GameObject;

                if (item != null &&
                    item.name == objectName)
                {
                    return item;
                }
            }

            return null;
        }

        private void SelectVariant(int variantIndex)
        {
            try
            {
                object found =
                    Weapon7Helpers.FindReadyGunControl();

                if (found == null)
                {
                    Log.Warn(
                        "[Weapon7FourVariants] GunControl is not ready.");

                    return;
                }

                gunControl = found;

                if (!EnsureFourVariants())
                {
                    Log.Warn(
                        "[Weapon7FourVariants] Four variants are not ready yet.");

                    return;
                }

                Weapon7Helpers.Invoke(
                    gunControl, "SwitchWeapon", 7, false);

                Weapon7Helpers.Invoke(
                    gunControl, "SwitchVariant", variantIndex);

                Log.Info(
                    "[Weapon7FourVariants] Requested slot 7 variant " +
                    variantIndex + ".");

                for (int i = 0; i < variants.Length; i++)
                {
                    GameObject variant = variants[i];

                    Log.Info(
                        "[Weapon7FourVariants] V" + (i + 1) +
                        " | name=" +
                        (variant != null ? variant.name : "NULL") +
                        " | activeSelf=" +
                        (variant != null && variant.activeSelf) +
                        " | activeInHierarchy=" +
                        (variant != null && variant.activeInHierarchy));
                }
            }
            catch (Exception ex)
            {
                Log.Error(
                    "[Weapon7FourVariants] Could not select variant " +
                    variantIndex + ": " +
                    Weapon7Helpers.Unwrap(ex));
            }
        }
    }
}
