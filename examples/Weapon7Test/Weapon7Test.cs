using System.IO;
using ULTRADROID.Models;
using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using ULTRADROID.API;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ULTRADROID.Weapon7Test
{
    [BepInPlugin(
        "somerandommaid.ultradroid.weapon7test",
        "ULTRADROID Weapon 7 Test",
        "0.1.0")]
    [BepInDependency(
        ULTRADROIDControls.PluginGuid,
        BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Weapon7Plugin : BasePlugin
    {
        public override void Load()
        {
            Weapon7Runtime.Log = new ManualLogSourceProxy(Log);
            AddComponent<Weapon7Runtime>();
            Log.LogInfo("[Weapon7] Test plugin loaded.");
        }
    }

    internal sealed class ManualLogSourceProxy
    {
        private readonly ManualLogSource source;

        internal ManualLogSourceProxy(ManualLogSource source)
        {
            this.source = source;
        }

        internal void Info(string message) => source.LogInfo(message);
        internal void Warn(string message) => source.LogWarning(message);
        internal void Error(string message) => source.LogError(message);
    }

    public sealed class Weapon7Runtime : MonoBehaviour
    {
        internal static ManualLogSourceProxy Log;

        private ButtonHandle button;
        private object gunControl;
        private GameObject testWeapon;
        private GameObject loadedCustomModel;

        private IntPtr lastGunPointer = IntPtr.Zero;

        private int lastCheckFrame = -120;
        private int lastWaitingLogFrame = -120;
        private int equippedSeenFrame = -1;
        private int lastFireInputLogFrame = -1;
        private int lastLiveStateLogFrame = -120;

        // Fire-input probe caching
        private object cachedPlayerInput;
        private int lastInputSearchFrame = -120;
        private int lastProbeWarningFrame = -120;

        private bool buttonRegistered;
        private bool methodDiagnosticsLogged;
        private bool slotSetupComplete;
        private bool refreshAttempted;
        private bool descriptorConfigured;
        private bool postEquipDiagnosticsLogged;

        public Weapon7Runtime(IntPtr ptr) : base(ptr) { }

        public void Update()
        {
            if (Log == null)
                return;

            try
            {
                ProbeIndependentFireInput();

                string scene = SceneManager.GetActiveScene().name;

                if (scene == "Bootstrap" || scene == "Intro")
                    return;

                RegisterButton();

                if (Time.frameCount - lastCheckFrame >= 120)
                {
                    lastCheckFrame = Time.frameCount;

                    object found =
                        Weapon7Helpers.FindReadyGunControl();

                    if (found == null)
                    {
                        if (slotSetupComplete)
                        {
                            Log.Warn(
                                "[Weapon7] GunControl is no longer ready; " +
                                "waiting for reinitialization.");

                            ResetInstanceState();
                        }
                        else if (
                            Time.frameCount -
                            lastWaitingLogFrame >= 600)
                        {
                            lastWaitingLogFrame = Time.frameCount;

                            Log.Warn(
                                "[Weapon7] Waiting for initialized GunControl.");
                        }

                        return;
                    }

                    IntPtr pointer =
                        Weapon7Helpers.GetPointer(found);

                    if (pointer == IntPtr.Zero)
                        return;

                    if (pointer != lastGunPointer)
                    {
                        IntPtr previousPointer = lastGunPointer;

                        ResetInstanceState();

                        lastGunPointer = pointer;
                        gunControl = found;

                        Log.Info(
                            "[Weapon7] GunControl instance changed: old=0x" +
                            previousPointer.ToString("X") +
                            ", new=0x" + pointer.ToString("X"));

                        Log.Info(
                            "[Weapon7] Found initialized GunControl.");
                    }
                    else
                    {
                        gunControl = found;
                    }

                    if (!slotSetupComplete)
                        EnsureSlot7();
                }

                if (slotSetupComplete)
                {
                    CheckEquippedWeaponDiagnostics();
                    LogLiveWeaponState();
                }
            }
            catch (Exception ex)
            {
                Log.Error(
                    "[Weapon7] Runtime error: " +
                    Weapon7Helpers.Unwrap(ex));
            }
        }

        private void ResetInstanceState()
        {
            lastGunPointer = IntPtr.Zero;
            gunControl = null;
            testWeapon = null;

            slotSetupComplete = false;
            refreshAttempted = false;
            descriptorConfigured = false;
            methodDiagnosticsLogged = false;

            equippedSeenFrame = -1;
            postEquipDiagnosticsLogged = false;
            lastLiveStateLogFrame = -120;

            // Reset fire-input probe state
            lastFireInputLogFrame = -1;
            cachedPlayerInput = null;
            lastInputSearchFrame = -120;
        }

        private void RegisterButton()
        {
            if (buttonRegistered)
                return;

            try
            {
                button = ULTRADROIDControls.AddButton(new ButtonConfig
                {
                    Id = "ULTRADROID.Weapon7.Select",
                    Label = "W7",
                    PositionOffset = new Vector2(120f, 90f),
                    ScaleMultiplier = 1f,
                    ApplyTint = false,
                    OnClick = SwitchToSlot7
                });

                buttonRegistered = true;

                Log.Info("[Weapon7] W7 HUD button registered.");
            }
            catch (Exception ex)
            {
                Log.Error(
                    "[Weapon7] Button registration failed: " +
                    Weapon7Helpers.Unwrap(ex));
            }
        }

        private void EnsureCustomModel()
        {
            if (testWeapon == null)
                return;

            if (loadedCustomModel != null &&
                loadedCustomModel.transform.parent == testWeapon.transform)
            {
                HideVanillaWeaponMeshes();
                return;
            }

            if (loadedCustomModel != null)
            {
                UnityEngine.Object.Destroy(loadedCustomModel);
                loadedCustomModel = null;
            }

            string path = Path.Combine(
                Paths.PluginPath,
                "ULTRADROID_TEST_WEAPON.glb");

            if (!File.Exists(path))
            {
                Log.Warn("[Weapon7 Model] GLB not found: " + path);
                return;
            }

            try
            {
                ModelLoadOptions options = new ModelLoadOptions
                {
                    Name = "ULTRADROID_TestWeapon_Model",
                    Position = Vector3.zero,
                    RotationEuler = Vector3.zero,
                    Scale = new Vector3(0.05f, 0.05f, 0.05f),
                    LoadTextures = true,
                    CreateColliders = false
                };

                loadedCustomModel = ModelAPI.LoadGLB(
                    path,
                    testWeapon.transform,
                    options);

                if (loadedCustomModel == null)
                {
                    Log.Error(
                        "[Weapon7 Model] ModelAPI.LoadGLB returned null.");
                    return;
                }

                Log.Info(
                    "[Weapon7 Model] GLB loaded: " +
                    loadedCustomModel.name);

                HideVanillaWeaponMeshes();

                Log.Info(
                    "[Weapon7 Model] Custom model renderers=" +
                    loadedCustomModel.GetComponentsInChildren<Renderer>(true).Length);
            }
            catch (Exception ex)
            {
                Log.Error(
                    "[Weapon7 Model] Failed to load GLB: " +
                    Weapon7Helpers.Unwrap(ex));
            }
        }

        private void HideVanillaWeaponMeshes()
        {
            if (testWeapon == null || loadedCustomModel == null)
                return;

            Renderer[] renderers =
                testWeapon.GetComponentsInChildren<Renderer>(true);

            int hidden = 0;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                Transform rendererTransform = renderer.transform;
                Transform modelTransform = loadedCustomModel.transform;

                // Preserve every renderer belonging to the custom GLB.
                if (rendererTransform == modelTransform ||
                    rendererTransform.IsChildOf(modelTransform))
                    continue;

                // Hide only mesh renderers, leaving other weapon effects alone.
                if (!(renderer is MeshRenderer) &&
                    !(renderer is SkinnedMeshRenderer))
                    continue;

                renderer.enabled = false;
                hidden++;
            }

            Log.Info(
                "[Weapon7 Model] Hidden vanilla mesh renderers=" +
                hidden);
        }

        private bool EnsureSlot7()
        {
            if (gunControl == null)
                return false;

            try
            {
                object slots = Weapon7Helpers.GetMember(
                    gunControl, "slots");

                if (slots == null)
                {
                    Log.Warn("[Weapon7] GunControl.slots unavailable.");
                    return false;
                }

                int slotCount = Weapon7Helpers.Count(slots);

                if (slotCount < 6)
                    return false;

                object slot7 = slotCount >= 7
                    ? Weapon7Helpers.Item(slots, 6)
                    : null;

                if (slotCount >= 7 && slot7 == null)
                {
                    Log.Warn("[Weapon7] Slot 7 could not be read.");
                    return false;
                }

                if (slot7 != null && testWeapon == null)
                {
                    for (int i = 0;
                         i < Weapon7Helpers.Count(slot7);
                         i++)
                    {
                        GameObject item =
                            Weapon7Helpers.Item(slot7, i) as GameObject;

                        if (item != null &&
                            item.name == "ULTRADROID_TEST_WEAPON7")
                        {
                            testWeapon = item;
                            break;
                        }
                    }
                }

                if (slotCount < 7)
                {
                    Type slotsType = slots.GetType();

                    if (!slotsType.IsGenericType)
                    {
                        Log.Error(
                            "[Weapon7] Unexpected slots collection type.");
                        return false;
                    }

                    Type[] args = slotsType.GetGenericArguments();

                    if (args.Length != 1)
                        return false;

                    object newSlot = Activator.CreateInstance(args[0]);

                    if (newSlot == null)
                        return false;

                    Weapon7Helpers.Add(slots, newSlot);
                    slot7 = newSlot;

                    Log.Info("[Weapon7] Added slot 7.");
                }

                if (slot7 == null)
                    slot7 = Weapon7Helpers.Item(slots, 6);

                if (slot7 == null)
                    return false;

                if (testWeapon == null)
                {
                    GameObject template =
                        Weapon7Helpers.FindTemplateWeapon(slots);

                    if (template == null)
                    {
                        Log.Warn("[Weapon7] No weapon template found.");
                        return false;
                    }

                    testWeapon = UnityEngine.Object.Instantiate(
                        template, template.transform.parent);

                    if (testWeapon == null)
                        return false;

                    testWeapon.name = "ULTRADROID_TEST_WEAPON7";

                    Weapon7Helpers.LogWeaponComponents(testWeapon, Log);

                    testWeapon.SetActive(false);

                    Log.Info(
                        "[Weapon7] Cloned template: " + template.name);
                }

                if (!descriptorConfigured)
                {
                    descriptorConfigured =
                        Weapon7Helpers.AssignUniqueDescriptor(
                            testWeapon,
                            "ULTRADROID Test Weapon 7",
                            Log);

                    if (!descriptorConfigured)
                    {
                        Log.Warn(
                            "[Weapon7] Unique descriptor setup failed; " +
                            "will retry.");
                        return false;
                    }
                }

                if (!Weapon7Helpers.Contains(slot7, testWeapon))
                    Weapon7Helpers.Add(slot7, testWeapon);

                if (!methodDiagnosticsLogged)
                {
                    methodDiagnosticsLogged = true;

                    Weapon7Helpers.LogRelevantMethods(
                        gunControl.GetType(), Log);
                }

                if (!refreshAttempted)
                {
                    refreshAttempted = true;

                    try
                    {
                        Weapon7Helpers.Invoke(
                            gunControl, "CalculateSlotCount");

                        Log.Info(
                            "[Weapon7] CalculateSlotCount invoked.");
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(
                            "[Weapon7] CalculateSlotCount failed: " +
                            Weapon7Helpers.Unwrap(ex));
                    }

                    Weapon7Helpers.LogWeaponDescriptors(
                        gunControl, Log);

                    Weapon7Helpers.LogWheelSegments(Log);
                }

                EnsureCustomModel();

                slotSetupComplete = true;

                Log.Info(
                    "[Weapon7] Setup complete: slots=" +
                    Weapon7Helpers.Count(slots) +
                    ", slot7 weapons=" +
                    Weapon7Helpers.Count(slot7));

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(
                    "[Weapon7] Slot setup failed: " +
                    Weapon7Helpers.Unwrap(ex));

                return false;
            }
        }

        private void ProbeIndependentFireInput()
        {
            try
            {
                if (cachedPlayerInput == null ||
                    Time.frameCount - lastInputSearchFrame >= 120)
                {
                    lastInputSearchFrame = Time.frameCount;
                    cachedPlayerInput = null;

                    Type inputManagerType =
                        Weapon7Helpers.FindType("InputManager");

                    if (inputManagerType == null)
                    {
                        if (Time.frameCount - lastProbeWarningFrame >= 300)
                        {
                            lastProbeWarningFrame = Time.frameCount;
                            Log.Warn("[Weapon7] InputManager type not found.");
                        }

                        return;
                    }

                    // Find MonoSingleton<InputManager> in the inheritance chain.
                    Type singletonType = null;

                    for (Type current = inputManagerType;
                         current != null;
                         current = current.BaseType)
                    {
                        if (current.IsGenericType &&
                            current.GetGenericTypeDefinition().Name ==
                                "MonoSingleton`1" &&
                            current.GetGenericArguments().Length == 1 &&
                            current.GetGenericArguments()[0] == inputManagerType)
                        {
                            singletonType = current;
                            break;
                        }
                    }

                    if (singletonType == null)
                    {
                        if (Time.frameCount - lastProbeWarningFrame >= 300)
                        {
                            lastProbeWarningFrame = Time.frameCount;
                            Log.Warn(
                                "[Weapon7] MonoSingleton<InputManager> not found.");
                        }

                        return;
                    }

                    // Locate the singleton Instance property, including inherited non-public members.
                    PropertyInfo instanceProperty = null;

                    for (Type current = singletonType;
                         current != null && instanceProperty == null;
                         current = current.BaseType)
                    {
                        instanceProperty = current.GetProperty(
                            "Instance",
                            BindingFlags.Public |
                            BindingFlags.NonPublic |
                            BindingFlags.Static |
                            BindingFlags.DeclaredOnly);
                    }

                    if (instanceProperty == null)
                    {
                        if (Time.frameCount - lastProbeWarningFrame >= 300)
                        {
                            lastProbeWarningFrame = Time.frameCount;
                            Log.Warn(
                                "[Weapon7] InputManager.Instance property not found.");
                        }

                        return;
                    }

                    MethodInfo getter = instanceProperty.GetGetMethod(true);

                    if (getter == null || !getter.IsStatic)
                    {
                        Log.Warn(
                            "[Weapon7] InputManager.Instance has no static getter.");
                        return;
                    }

                    object manager = getter.Invoke(null, null);

                    if (manager == null)
                    {
                        if (Time.frameCount - lastProbeWarningFrame >= 300)
                        {
                            lastProbeWarningFrame = Time.frameCount;
                            Log.Warn(
                                "[Weapon7] InputManager.Instance is null.");
                        }

                        return;
                    }

                    // PlayerInput is a regular managed game object, not a component.
                    cachedPlayerInput = Weapon7Helpers.GetMember(
                        manager, "InputSource");

                    if (cachedPlayerInput == null)
                    {
                        if (Time.frameCount - lastProbeWarningFrame >= 300)
                        {
                            lastProbeWarningFrame = Time.frameCount;
                            Log.Warn(
                                "[Weapon7] InputManager.InputSource is null.");
                        }

                        return;
                    }

                    Log.Info(
                        "[Weapon7] Acquired PlayerInput through InputManager.Instance.");
                }

                object fire1 = Weapon7Helpers.GetMember(
                    cachedPlayerInput, "Fire1");

                if (fire1 == null)
                {
                    cachedPlayerInput = null;
                    return;
                }

                object performedValue = Weapon7Helpers.GetMember(
                    fire1, "WasPerformedThisFrame");

                object pressedValue = Weapon7Helpers.GetMember(
                    fire1, "IsPressed");

                bool performed =
                    performedValue is bool p && p;

                bool pressed =
                    pressedValue is bool h && h;

                if ((performed || pressed) &&
                    Time.frameCount - lastFireInputLogFrame >= 30)
                {
                    lastFireInputLogFrame = Time.frameCount;

                    Log.Info(
                        "[Weapon7] GLOBAL Fire1: " +
                        "performed=" + performed +
                        ", pressed=" + pressed);

                    LogWeapon7InputComparison(fire1, performed, pressed);
                }
            }
            catch (Exception ex)
            {
                if (Time.frameCount - lastProbeWarningFrame >= 300)
                {
                    lastProbeWarningFrame = Time.frameCount;

                    Log.Warn(
                        "[Weapon7] Fire input probe failed: " +
                        Weapon7Helpers.Unwrap(ex));
                }

                cachedPlayerInput = null;
            }
        }

        private void LogWeapon7InputComparison(
            object globalFire1,
            bool globalPerformed,
            bool globalPressed)
        {
            try
            {
                if (testWeapon == null)
                {
                    Log.Info(
                        "[Weapon7] Input comparison skipped: " +
                        "testWeapon is null.");
                    return;
                }

                Type revolverType =
                    Weapon7Helpers.FindType("Revolver");

                if (revolverType == null)
                {
                    Log.Warn(
                        "[Weapon7] Input comparison: Revolver type not found.");
                    return;
                }

                object components = Weapon7Helpers.Invoke(
                    testWeapon,
                    "GetComponentsInChildren",
                    Il2CppType.From(revolverType),
                    true);

                if (Weapon7Helpers.Count(components) == 0)
                {
                    Log.Warn(
                        "[Weapon7] Input comparison: no Revolver component.");
                    return;
                }

                object revolver = Weapon7Helpers.WrapForDiagnostics(
                    revolverType,
                    Weapon7Helpers.Item(components, 0));

                if (revolver == null)
                {
                    Log.Warn(
                        "[Weapon7] Input comparison: could not wrap Revolver.");
                    return;
                }

                object inman =
                    Weapon7Helpers.GetMember(revolver, "inman");

                object cloneInput =
                    Weapon7Helpers.GetMember(inman, "InputSource");

                object cloneFire1 =
                    Weapon7Helpers.GetMember(cloneInput, "Fire1");

                object clonePerformedValue =
                    Weapon7Helpers.GetMember(
                        cloneFire1, "WasPerformedThisFrame");

                object clonePressedValue =
                    Weapon7Helpers.GetMember(
                        cloneFire1, "IsPressed");

                string clonePerformed =
                    clonePerformedValue == null
                        ? "unavailable"
                        : clonePerformedValue.ToString();

                string clonePressed =
                    clonePressedValue == null
                        ? "unavailable"
                        : clonePressedValue.ToString();

                IntPtr globalInputPointer =
                    Weapon7Helpers.GetPointer(cachedPlayerInput);

                IntPtr cloneInputPointer =
                    Weapon7Helpers.GetPointer(cloneInput);

                IntPtr globalFirePointer =
                    Weapon7Helpers.GetPointer(globalFire1);

                IntPtr cloneFirePointer =
                    Weapon7Helpers.GetPointer(cloneFire1);

                bool sameInput =
                    globalInputPointer != IntPtr.Zero &&
                    cloneInputPointer != IntPtr.Zero
                        ? globalInputPointer == cloneInputPointer
                        : ReferenceEquals(cachedPlayerInput, cloneInput);

                bool sameFire =
                    globalFirePointer != IntPtr.Zero &&
                    cloneFirePointer != IntPtr.Zero
                        ? globalFirePointer == cloneFirePointer
                        : ReferenceEquals(globalFire1, cloneFire1);

                object currentWeapon =
                    Weapon7Helpers.GetMember(
                        gunControl, "currentWeapon");

                bool isCurrent =
                    Weapon7Helpers.GetPointer(currentWeapon) ==
                    Weapon7Helpers.GetPointer(testWeapon);

                Log.Info(
                    "[Weapon7] W7 INPUT COMPARISON frame=" +
                    Time.frameCount);

                Log.Info(
                    "[Weapon7] Global Fire1: performed=" +
                    globalPerformed + ", pressed=" + globalPressed);

                Log.Info(
                    "[Weapon7] W7 Fire1: performed=" +
                    clonePerformed + ", pressed=" + clonePressed);

                Log.Info(
                    "[Weapon7] Same PlayerInput object=" + sameInput +
                    " | global=0x" + globalInputPointer.ToString("X") +
                    " | W7=0x" + cloneInputPointer.ToString("X"));

                Log.Info(
                    "[Weapon7] Same Fire1 object=" + sameFire +
                    " | global=0x" + globalFirePointer.ToString("X") +
                    " | W7=0x" + cloneFirePointer.ToString("X"));

                Log.Info(
                    "[Weapon7] W7 is current weapon=" + isCurrent);
            }
            catch (Exception ex)
            {
                Log.Warn(
                    "[Weapon7] Input comparison failed: " +
                    Weapon7Helpers.Unwrap(ex));
            }
        }

        private void ApplyWeapon7GoldTint(GameObject weapon)
        {
            if (weapon == null)
            {
                Log.Warn("[W7 GOLD] Weapon is null.");
                return;
            }

            object renderers = Weapon7Helpers.Invoke(
                weapon,
                "GetComponentsInChildren",
                Il2CppType.From(typeof(Renderer)),
                true);

            Color gold = new Color(1.0f, 0.68f, 0.12f, 1.0f);
            int changed = 0;

            Type getterType = Weapon7Helpers.FindType("GunColorGetter");

            if (getterType == null)
            {
                Log.Warn("[W7 GOLD] GunColorGetter type not found.");
                return;
            }

            for (int i = 0; i < Weapon7Helpers.Count(renderers); i++)
            {
                Renderer renderer = Weapon7Helpers.WrapForDiagnostics(
                    typeof(Renderer),
                    Weapon7Helpers.Item(renderers, i)) as Renderer;

                if (renderer == null)
                    continue;

                object getter = Weapon7Helpers.Invoke(
                    renderer,
                    "GetComponent",
                    Il2CppType.From(getterType));

                if (getter == null)
                    continue;

                // renderer.materials provides renderer-specific material instances.
                object materials = Weapon7Helpers.GetMember(
                    renderer, "materials");

                if (materials == null)
                {
                    Log.Warn(
                        "[W7 GOLD] Could not retrieve materials for renderer " +
                        renderer.name);
                    continue;
                }

                for (int j = 0; j < Weapon7Helpers.Count(materials); j++)
                {
                    Material material = Weapon7Helpers.WrapForDiagnostics(
                        typeof(Material),
                        Weapon7Helpers.Item(materials, j)) as Material;

                    if (material == null)
                        continue;

                    bool hasColor = (bool)Weapon7Helpers.Invoke(
                        material, "HasProperty", "_Color");

                    Log.Info(
                        "[W7 GOLD] Renderer=" + renderer.name +
                        ", Material=" + material.name +
                        ", Shader=" +
                        (material.shader != null
                            ? material.shader.name
                            : "null") +
                        ", Has _Color=" + hasColor);

                    if (hasColor)
                    {
                        Weapon7Helpers.Invoke(
                            material, "SetColor", "_Color", gold);
                        changed++;
                    }
                }
            }

            Log.Info(
                "[W7 GOLD] Applied gold tint to " +
                changed + " material(s).");
        }

        private void LogWeapon7Renderers(GameObject weapon)
        {
            if (weapon == null)
            {
                Log.Warn("[W7 COLOR] Weapon clone is null.");
                return;
            }

            object renderers = Weapon7Helpers.Invoke(
                weapon,
                "GetComponentsInChildren",
                Il2CppType.From(typeof(Renderer)),
                true);

            Log.Info(
                "[W7 COLOR] Root=" + weapon.name +
                ", renderers=" + Weapon7Helpers.Count(renderers));

            Type getterType =
                Weapon7Helpers.FindType("GunColorGetter");

            for (int i = 0; i < Weapon7Helpers.Count(renderers); i++)
            {
                Renderer renderer =
                    Weapon7Helpers.WrapForDiagnostics(
                        typeof(Renderer),
                        Weapon7Helpers.Item(renderers, i)) as Renderer;

                if (renderer == null)
                    continue;

                object materials =
                    Weapon7Helpers.GetMember(renderer, "sharedMaterials");

                Log.Info(
                    "[W7 COLOR] Renderer=" + renderer.name +
                    ", active=" + renderer.gameObject.activeInHierarchy +
                    ", materials=" + Weapon7Helpers.Count(materials));

                for (int j = 0; j < Weapon7Helpers.Count(materials); j++)
                {
                    Material material =
                        Weapon7Helpers.WrapForDiagnostics(
                            typeof(Material),
                            Weapon7Helpers.Item(materials, j)) as Material;

                    if (material == null)
                        continue;

                    string shaderName = material.shader != null
                        ? material.shader.name
                        : "<null>";

                    Log.Info(
                        "[W7 COLOR] Material=" + material.name +
                        ", shader=" + shaderName);
                }

                if (getterType != null)
                {
                    object getter = Weapon7Helpers.Invoke(
                        renderer,
                        "GetComponent",
                        Il2CppType.From(getterType));

                    if (getter != null)
                    {
                        Log.Info(
                            "[W7 COLOR] GunColorGetter found on " +
                            renderer.name);
                    }
                }
            }
        }

        private void LogWeapon7ColorGetterState(GameObject weapon)
        {
            if (weapon == null)
                return;

            Type getterType = Weapon7Helpers.FindType("GunColorGetter");

            if (getterType == null)
            {
                Log.Warn("[W7 COLOR STATE] GunColorGetter type not found.");
                return;
            }

            try
            {
                object getters = Weapon7Helpers.Invoke(
                    weapon,
                    "GetComponentsInChildren",
                    Il2CppType.From(getterType),
                    true);

                int count = Weapon7Helpers.Count(getters);

                Log.Info("[W7 COLOR STATE] Getter count=" + count);

                for (int i = 0; i < count; i++)
                {
                    object getter = Weapon7Helpers.WrapForDiagnostics(
                        getterType,
                        Weapon7Helpers.Item(getters, i));

                    if (getter == null)
                        continue;

                    Log.Info("[W7 COLOR STATE] Getter[" + i + "]");

                    string[] fields =
                    {
                        "weaponNumber",
                        "altVersion",
                        "hasCustomColors",
                        "currentColors",
                        "defaultMaterials",
                        "coloredMaterials",
                        "customColors"
                    };

                    foreach (string field in fields)
                    {
                        try
                        {
                            object value = Weapon7Helpers.GetMember(
                                getter, field);

                            Log.Info(
                                "[W7 COLOR STATE] " + field + "=" +
                                (value == null ? "null" : value.ToString()));

                            if (field == "currentColors" && value != null)
                            {
                                foreach (string colorName in new[]
                                         { "color1", "color2", "color3" })
                                {
                                    object color = Weapon7Helpers.GetMember(
                                        value, colorName);

                                    Log.Info(
                                        "[W7 COLOR STATE] " + colorName + "=" +
                                        (color == null
                                            ? "null"
                                            : color.ToString()));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warn(
                                "[W7 COLOR STATE] Could not read " +
                                field + ": " + Weapon7Helpers.Unwrap(ex));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(
                    "[W7 COLOR STATE] Diagnostic failed: " +
                    Weapon7Helpers.Unwrap(ex));
            }
        }

        private void CheckEquippedWeaponDiagnostics()
        {
            if (postEquipDiagnosticsLogged ||
                gunControl == null ||
                testWeapon == null)
                return;

            object currentWeapon =
                Weapon7Helpers.GetMember(gunControl, "currentWeapon");

            IntPtr currentPointer =
                Weapon7Helpers.GetPointer(currentWeapon);

            IntPtr testPointer =
                Weapon7Helpers.GetPointer(testWeapon);

            if (testPointer == IntPtr.Zero ||
                currentPointer != testPointer)
            {
                equippedSeenFrame = -1;
                return;
            }

            if (equippedSeenFrame < 0)
            {
                equippedSeenFrame = Time.frameCount;

                Log.Info(
                    "[Weapon7] Detected Weapon 7 as currentWeapon. " +
                    "Waiting 30 frames before post-equip diagnostics.");

                return;
            }

            if (Time.frameCount - equippedSeenFrame < 30)
                return;

            postEquipDiagnosticsLogged = true;

            Log.Info(
                "[Weapon7] ===== POST-EQUIP DIAGNOSTICS =====");

            Log.Info(
                "[Weapon7] Current slot = " +
                (Weapon7Helpers.GetMember(
                    gunControl, "currentSlot") ?? "null"));

            Log.Info(
                "[Weapon7] Weapon object name = " +
                testWeapon.name);

            Log.Info(
                "[Weapon7] Weapon pointer = 0x" +
                testPointer.ToString("X"));

            Log.Info(
                "[Weapon7] activeSelf = " +
                testWeapon.activeSelf);

            Log.Info(
                "[Weapon7] activeInHierarchy = " +
                testWeapon.activeInHierarchy);

            Log.Info(
                "[Weapon7] Current weapon pointer = 0x" +
                currentPointer.ToString("X"));

            Weapon7Helpers.LogWeaponComponents(testWeapon, Log);
            Weapon7Helpers.LogRevolverRuntimeState(testWeapon, Log);
            LogWeapon7Renderers(testWeapon);
            LogWeapon7Hierarchy(testWeapon);
            LogWeapon7ColorGetterState(testWeapon);
            ApplyWeapon7GoldTint(testWeapon);

            Log.Info(
                "[Weapon7] ===== END POST-EQUIP DIAGNOSTICS =====");
        }

        private void LogWeapon7Hierarchy(GameObject weapon)
        {
            if (weapon == null)
            {
                Log.Warn("[W7 MODEL] Weapon is null.");
                return;
            }

            Log.Info("[W7 MODEL] ===== HIERARCHY BEGIN =====");
            Log.Info("[W7 MODEL] Root: " + weapon.name);

            Transform[] transforms =
                weapon.GetComponentsInChildren<Transform>(true);

            Log.Info(
                "[W7 MODEL] Total transforms: " + transforms.Length);

            foreach (Transform child in transforms)
            {
                if (child == null)
                    continue;

                GameObject obj = child.gameObject;

                string path = obj.name;
                Transform parent = child.parent;

                while (parent != null && parent != weapon.transform)
                {
                    path = parent.name + "/" + path;
                    parent = parent.parent;
                }

                if (parent == weapon.transform)
                    path = weapon.name + "/" + path;

                MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
                MeshRenderer meshRenderer = obj.GetComponent<MeshRenderer>();
                SkinnedMeshRenderer skinnedRenderer =
                    obj.GetComponent<SkinnedMeshRenderer>();

                string meshName =
                    meshFilter != null && meshFilter.sharedMesh != null
                        ? meshFilter.sharedMesh.name
                        : "<none>";

                string skinnedMeshName =
                    skinnedRenderer != null &&
                    skinnedRenderer.sharedMesh != null
                        ? skinnedRenderer.sharedMesh.name
                        : "<none>";

                Log.Info(
                    "[W7 MODEL] Object=" + path +
                    " | activeSelf=" + obj.activeSelf +
                    " | activeInHierarchy=" + obj.activeInHierarchy +
                    " | MeshFilter=" + (meshFilter != null) +
                    " | Mesh=" + meshName +
                    " | MeshRenderer=" + (meshRenderer != null) +
                    " | SkinnedMeshRenderer=" + (skinnedRenderer != null) +
                    " | SkinnedMesh=" + skinnedMeshName);
            }

            Log.Info("[W7 MODEL] ===== HIERARCHY END =====");
        }

        private void LogLiveWeaponState()
        {
            if (gunControl == null || testWeapon == null)
                return;

            if (Time.frameCount - lastLiveStateLogFrame < 30)
                return;

            object currentWeapon =
                Weapon7Helpers.GetMember(gunControl, "currentWeapon");

            IntPtr currentPointer =
                Weapon7Helpers.GetPointer(currentWeapon);

            IntPtr testPointer =
                Weapon7Helpers.GetPointer(testWeapon);

            if (testPointer == IntPtr.Zero ||
                currentPointer != testPointer)
                return;

            lastLiveStateLogFrame = Time.frameCount;

            try
            {
                Type revolverType =
                    Weapon7Helpers.FindType("Revolver");

                if (revolverType == null)
                    return;

                object components = Weapon7Helpers.Invoke(
                    testWeapon,
                    "GetComponentsInChildren",
                    Il2CppType.From(revolverType),
                    true);

                if (Weapon7Helpers.Count(components) == 0)
                    return;

                object revolver =
                    Weapon7Helpers.WrapForDiagnostics(
                        revolverType,
                        Weapon7Helpers.Item(components, 0));

                if (revolver == null)
                    return;

                Log.Info(
                    "[Weapon7] ===== LIVE FIRING STATE frame=" +
                    Time.frameCount + " =====");

                string[] fields =
                {
                    "enabled",
                    "gunReady",
                    "shootReady",
                    "pierceReady",
                    "chargingPierce",
                    "shootCharge",
                    "pierceCharge",
                    "pierceShotCharge",
                    "gunVariation",
                    "altVersion"
                };

                foreach (string field in fields)
                {
                    object value =
                        Weapon7Helpers.GetMember(revolver, field);

                    Log.Info(
                        "[Weapon7] LIVE " + field + "=" +
                        (value == null ? "<null/unavailable>" : value));
                }

                object inman =
                    Weapon7Helpers.GetMember(revolver, "inman");

                object inputSource =
                    Weapon7Helpers.GetMember(inman, "InputSource");

                object fire1 =
                    Weapon7Helpers.GetMember(inputSource, "Fire1");

                object globalFire1 =
                    Weapon7Helpers.GetMember(
                        cachedPlayerInput, "Fire1");

                Log.Info(
                    "[Weapon7] LIVE inman=0x" +
                    Weapon7Helpers.GetPointer(inman).ToString("X") +
                    " InputSource=0x" +
                    Weapon7Helpers.GetPointer(inputSource).ToString("X") +
                    " Fire1=0x" +
                    Weapon7Helpers.GetPointer(fire1).ToString("X"));

                Log.Info(
                    "[Weapon7] LIVE global Fire1=0x" +
                    Weapon7Helpers.GetPointer(globalFire1).ToString("X"));

                if (fire1 != null)
                {
                    object performed =
                        Weapon7Helpers.GetMember(
                            fire1, "WasPerformedThisFrame");

                    object pressed =
                        Weapon7Helpers.GetMember(fire1, "IsPressed");

                    Log.Info(
                        "[Weapon7] LIVE W7 Fire1 performed=" +
                        (performed == null ? "unavailable" : performed) +
                        " pressed=" +
                        (pressed == null ? "unavailable" : pressed));
                }

                object wid =
                    Weapon7Helpers.GetMember(revolver, "wid");

                object gc =
                    Weapon7Helpers.GetMember(revolver, "gc");

                object wpos =
                    Weapon7Helpers.GetMember(revolver, "wpos");

                object wc =
                    Weapon7Helpers.GetMember(revolver, "wc");

                Log.Info(
                    "[Weapon7] LIVE refs: wid=0x" +
                    Weapon7Helpers.GetPointer(wid).ToString("X") +
                    " gc=0x" +
                    Weapon7Helpers.GetPointer(gc).ToString("X") +
                    " wpos=0x" +
                    Weapon7Helpers.GetPointer(wpos).ToString("X") +
                    " wc=0x" +
                    Weapon7Helpers.GetPointer(wc).ToString("X"));

                Log.Info(
                    "[Weapon7] ===== END LIVE FIRING STATE =====");
            }
            catch (Exception ex)
            {
                Log.Warn(
                    "[Weapon7] Live firing-state diagnostic failed: " +
                    Weapon7Helpers.Unwrap(ex));
            }
        }

        private void SwitchToSlot7()
        {
            if (Log == null)
                return;

            try
            {
                object found =
                    Weapon7Helpers.FindReadyGunControl();

                if (found == null)
                {
                    Log.Warn("[Weapon7] GunControl not ready.");
                    return;
                }

                IntPtr pointer =
                    Weapon7Helpers.GetPointer(found);

                if (pointer != lastGunPointer)
                {
                    ResetInstanceState();

                    lastGunPointer = pointer;
                    gunControl = found;
                }
                else
                {
                    gunControl = found;
                }

                if (!slotSetupComplete && !EnsureSlot7())
                {
                    Log.Warn(
                        "[Weapon7] Slot 7 setup is incomplete.");
                    return;
                }

                Weapon7Helpers.Invoke(
                    gunControl, "SwitchWeapon", 7, false);

                Log.Info(
                    "[Weapon7] SwitchWeapon(7, false) invoked.");
            }
            catch (Exception ex)
            {
                Log.Error(
                    "[Weapon7] Switch failed: " +
                    Weapon7Helpers.Unwrap(ex));
            }
        }
    }

    internal static class Weapon7Helpers
    {
        internal static Type FindType(string name)
        {
            foreach (Assembly assembly in
                     AppDomain.CurrentDomain.GetAssemblies())
            {
                Type direct = assembly.GetType(name, false);

                if (direct != null)
                    return direct;

                try
                {
                    foreach (Type candidate in assembly.GetTypes())
                    {
                        if (candidate != null &&
                            candidate.Name == name)
                            return candidate;
                    }
                }
                catch (ReflectionTypeLoadException ex)
                {
                    if (ex.Types == null)
                        continue;

                    foreach (Type candidate in ex.Types)
                    {
                        if (candidate != null &&
                            candidate.Name == name)
                            return candidate;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        internal static object FindReadyGunControl()
        {
            Type type = FindType("GunControl");

            if (type == null)
                return null;

            object found = UnityEngine.Object.FindObjectsOfType(
                Il2CppType.From(type));

            for (int i = 0; i < Count(found); i++)
            {
                object candidate =
                    WrapInstance(type, Item(found, i));

                if (candidate == null)
                    continue;

                object slots = GetMember(candidate, "slots");

                if (slots != null && Count(slots) >= 6)
                    return candidate;
            }

            return null;
        }

        private static object WrapInstance(Type type, object raw)
        {
            if (raw == null)
                return null;

            if (type.IsInstanceOfType(raw))
                return raw;

            Il2CppObjectBase native = raw as Il2CppObjectBase;

            if (native == null || native.Pointer == IntPtr.Zero)
                return null;

            ConstructorInfo ctor = type.GetConstructor(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic,
                null,
                new[] { typeof(IntPtr) },
                null);

            return ctor == null
                ? null
                : ctor.Invoke(new object[] { native.Pointer });
        }

        internal static object WrapForDiagnostics(
            Type type,
            object raw)
        {
            return WrapInstance(type, raw);
        }

        internal static IntPtr GetPointer(object value)
        {
            Il2CppObjectBase native = value as Il2CppObjectBase;

            return native == null
                ? IntPtr.Zero
                : native.Pointer;
        }

        internal static object GetMember(
            object instance, string name)
        {
            if (instance == null)
                return null;

            for (Type type = instance.GetType();
                 type != null;
                 type = type.BaseType)
            {
                FieldInfo field = type.GetField(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);

                if (field != null)
                    return field.GetValue(instance);

                PropertyInfo property = type.GetProperty(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);

                if (property != null &&
                    property.GetIndexParameters().Length == 0)
                {
                    MethodInfo getter = property.GetGetMethod(true);

                    if (getter != null)
                        return property.GetValue(instance, null);
                }
            }

            return null;
        }

        internal static bool SetMember(
            object instance, string name, object value)
        {
            if (instance == null)
                return false;

            for (Type type = instance.GetType();
                 type != null;
                 type = type.BaseType)
            {
                FieldInfo field = type.GetField(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);

                if (field != null)
                {
                    try
                    {
                        field.SetValue(instance, value);
                        return true;
                    }
                    catch
                    {
                    }
                }

                PropertyInfo property = type.GetProperty(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);

                if (property != null &&
                    property.GetIndexParameters().Length == 0)
                {
                    MethodInfo setter = property.GetSetMethod(true);

                    if (setter != null)
                    {
                        try
                        {
                            property.SetValue(instance, value, null);
                            return true;
                        }
                        catch
                        {
                        }
                    }
                }
            }

            return false;
        }

        private static object CreateDescriptor(Type descriptorType)
        {
            if (descriptorType == null)
                return null;

            foreach (MethodInfo method in typeof(ScriptableObject).GetMethods(
                         BindingFlags.Public |
                         BindingFlags.NonPublic |
                         BindingFlags.Static))
            {
                if (method.Name != "CreateInstance" ||
                    !method.IsGenericMethodDefinition ||
                    method.GetGenericArguments().Length != 1 ||
                    method.GetParameters().Length != 0)
                    continue;

                try
                {
                    MethodInfo closed =
                        method.MakeGenericMethod(descriptorType);

                    object result = closed.Invoke(null, null);

                    if (result != null)
                        return result;
                }
                catch
                {
                }
            }

            object il2CppType = null;

            try
            {
                il2CppType = Il2CppType.From(descriptorType);
            }
            catch
            {
            }

            foreach (MethodInfo method in typeof(ScriptableObject).GetMethods(
                         BindingFlags.Public |
                         BindingFlags.NonPublic |
                         BindingFlags.Static))
            {
                if (method.Name != "CreateInstance" ||
                    method.IsGenericMethod ||
                    method.GetParameters().Length != 1)
                    continue;

                Type parameterType =
                    method.GetParameters()[0].ParameterType;

                object argument = null;

                if (parameterType == typeof(Type))
                    argument = descriptorType;
                else if (il2CppType != null &&
                         parameterType.IsInstanceOfType(il2CppType))
                    argument = il2CppType;

                if (argument == null)
                    continue;

                try
                {
                    object result = method.Invoke(
                        null, new[] { argument });

                    if (result != null)
                        return result;
                }
                catch
                {
                }
            }

            return null;
        }

        internal static bool AssignUniqueDescriptor(
            GameObject weapon,
            string uniqueName,
            ManualLogSourceProxy log)
        {
            if (weapon == null)
                return false;

            Type iconType = FindType("WeaponIcon");
            Type descriptorType = FindType("WeaponDescriptor");

            if (iconType == null || descriptorType == null)
            {
                log.Error(
                    "[Weapon7] WeaponIcon or WeaponDescriptor type " +
                    "was not found.");

                return false;
            }

            object components;

            try
            {
                components = Invoke(
                    weapon,
                    "GetComponentsInChildren",
                    Il2CppType.From(iconType),
                    true);
            }
            catch (Exception ex)
            {
                log.Error(
                    "[Weapon7] Could not find WeaponIcon components: " +
                    Unwrap(ex));

                return false;
            }

            if (Count(components) == 0)
            {
                log.Error(
                    "[Weapon7] The cloned weapon has no WeaponIcon.");
                return false;
            }

            object firstIcon =
                WrapInstance(iconType, Item(components, 0));

            if (firstIcon == null)
                return false;

            object originalDescriptor = GetMember(
                firstIcon, "weaponDescriptor");

            object originalName = GetMember(
                originalDescriptor, "weaponName");

            if (originalName != null &&
                string.Equals(
                    originalName.ToString(),
                    uniqueName,
                    StringComparison.Ordinal))
            {
                log.Info(
                    "[Weapon7] Unique descriptor already assigned.");

                return true;
            }

            object newDescriptor =
                CreateDescriptor(descriptorType);

            if (newDescriptor == null)
            {
                log.Error(
                    "[Weapon7] ScriptableObject.CreateInstance failed.");

                return false;
            }

            if (originalDescriptor != null)
            {
                SetMember(
                    newDescriptor,
                    "icon",
                    GetMember(originalDescriptor, "icon"));

                SetMember(
                    newDescriptor,
                    "glowIcon",
                    GetMember(originalDescriptor, "glowIcon"));

                Type variantType = FindType("WeaponVariant");

                if (variantType != null &&
                    variantType.IsEnum)
                {
                    object goldVariant = Enum.Parse(
                        variantType,
                        "GoldVariant");

                    if (!SetMember(
                            newDescriptor,
                            "variationColor",
                            goldVariant))
                    {
                        log.Warn(
                            "[Weapon7] Could not assign GoldVariant.");
                    }
                    else
                    {
                        log.Info(
                            "[Weapon7] Descriptor variationColor = " +
                            goldVariant);
                    }
                }
                else
                {
                    log.Warn(
                        "[Weapon7] WeaponVariant enum not found.");
                }
            }

            if (!SetMember(
                    newDescriptor, "weaponName", uniqueName))
            {
                log.Error(
                    "[Weapon7] Could not set descriptor weaponName.");

                return false;
            }

            int assigned = 0;

            for (int i = 0; i < Count(components); i++)
            {
                object icon =
                    WrapInstance(iconType, Item(components, i));

                if (icon == null)
                    continue;

                if (SetMember(
                        icon, "weaponDescriptor", newDescriptor))
                    assigned++;
            }

            if (assigned == 0)
            {
                log.Error(
                    "[Weapon7] Could not assign the new descriptor " +
                    "to any WeaponIcon.");

                return false;
            }

            log.Info(
                "[Weapon7] Unique descriptor assigned: " +
                uniqueName +
                " (icons=" + assigned + ").");

            return true;
        }

        internal static int Count(object collection)
        {
            if (collection == null)
                return 0;

            if (collection is Array array)
                return array.Length;

            object count = GetMember(collection, "Count");

            return count == null
                ? 0
                : Convert.ToInt32(count);
        }

        internal static object Item(object collection, int index)
        {
            if (collection == null || index < 0)
                return null;

            if (collection is Array array)
            {
                return index < array.Length
                    ? array.GetValue(index)
                    : null;
            }

            try
            {
                PropertyInfo item = collection.GetType().GetProperty(
                    "Item",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

                if (item != null)
                    return item.GetValue(
                        collection, new object[] { index });
            }
            catch
            {
            }

            try
            {
                foreach (MethodInfo method in collection.GetType().GetMethods(
                             BindingFlags.Instance |
                             BindingFlags.Public |
                             BindingFlags.NonPublic))
                {
                    if (method.Name != "get_Item")
                        continue;

                    ParameterInfo[] p = method.GetParameters();

                    if (p.Length == 1 &&
                        p[0].ParameterType == typeof(int))
                    {
                        return method.Invoke(
                            collection, new object[] { index });
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        internal static void Add(object collection, object value)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            foreach (MethodInfo method in collection.GetType().GetMethods(
                         BindingFlags.Instance |
                         BindingFlags.Public |
                         BindingFlags.NonPublic))
            {
                if (method.Name != "Add")
                    continue;

                ParameterInfo[] p = method.GetParameters();

                if (p.Length != 1)
                    continue;

                if (value != null &&
                    !p[0].ParameterType.IsInstanceOfType(value) &&
                    p[0].ParameterType != value.GetType())
                    continue;

                method.Invoke(collection, new[] { value });
                return;
            }

            throw new MissingMethodException(
                collection.GetType().FullName, "Add");
        }

        internal static bool Contains(
            object collection, GameObject target)
        {
            for (int i = 0; i < Count(collection); i++)
            {
                if (Item(collection, i) is GameObject item &&
                    item == target)
                    return true;
            }

            return false;
        }

        internal static GameObject FindTemplateWeapon(object slots)
        {
            int limit = Math.Min(6, Count(slots));

            for (int i = 0; i < limit; i++)
            {
                object slot = Item(slots, i);

                for (int j = 0; j < Count(slot); j++)
                {
                    GameObject weapon =
                        Item(slot, j) as GameObject;

                    if (weapon != null &&
                        weapon.name != "ULTRADROID_TEST_WEAPON7")
                        return weapon;
                }
            }

            return null;
        }

        internal static object Invoke(
            object instance, string name, params object[] arguments)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            for (Type type = instance.GetType();
                 type != null;
                 type = type.BaseType)
            {
                foreach (MethodInfo method in type.GetMethods(
                             BindingFlags.Instance |
                             BindingFlags.Public |
                             BindingFlags.NonPublic |
                             BindingFlags.DeclaredOnly))
                {
                    if (method.Name != name)
                        continue;

                    ParameterInfo[] p = method.GetParameters();

                    if (p.Length != arguments.Length)
                        continue;

                    bool match = true;

                    for (int i = 0; i < p.Length; i++)
                    {
                        if (arguments[i] == null)
                        {
                            if (p[i].ParameterType.IsValueType &&
                                Nullable.GetUnderlyingType(
                                    p[i].ParameterType) == null)
                                match = false;

                            continue;
                        }

                        if (!p[i].ParameterType.IsAssignableFrom(
                                arguments[i].GetType()) &&
                            p[i].ParameterType != arguments[i].GetType())
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                        return method.Invoke(instance, arguments);
                }
            }

            throw new MissingMethodException(
                instance.GetType().FullName, name);
        }

        internal static void LogRevolverRuntimeState(
            GameObject weapon,
            ManualLogSourceProxy log)
        {
            if (weapon == null || log == null)
                return;

            try
            {
                Type revolverType = FindType("Revolver");

                if (revolverType == null)
                {
                    log.Warn("[Weapon7] Revolver type not found.");
                    return;
                }

                object components = Invoke(
                    weapon,
                    "GetComponentsInChildren",
                    Il2CppType.From(revolverType),
                    true);

                int count = Count(components);

                if (count == 0)
                {
                    log.Warn(
                        "[Weapon7] No Revolver components found on Weapon 7.");
                    return;
                }

                log.Info(
                    "[Weapon7] Weapon activeSelf = " + weapon.activeSelf);

                log.Info(
                    "[Weapon7] Weapon activeInHierarchy = " +
                    weapon.activeInHierarchy);

                for (int i = 0; i < count; i++)
                {
                    object revolver = WrapInstance(
                        revolverType,
                        Item(components, i));

                    if (revolver == null)
                    {
                        log.Warn(
                            "[Weapon7] Could not wrap Revolver component[" +
                            i + "].");
                        continue;
                    }

                    log.Info(
                        "[Weapon7] ===== REVOLVER RUNTIME STATE =====");

                    LogRuntimeMember(revolver, "enabled", log);
                    LogRuntimeMember(revolver, "gunReady", log);
                    LogRuntimeMember(revolver, "shootReady", log);
                    LogRuntimeMember(revolver, "gunVariation", log);
                    LogRuntimeMember(revolver, "altVersion", log);

                    LogRuntimeMember(revolver, "inman", log);
                    LogRuntimeMember(revolver, "wid", log);
                    LogRuntimeMember(revolver, "gc", log);
                    LogRuntimeMember(revolver, "wpos", log);
                    LogRuntimeMember(revolver, "wc", log);

                    object inman = GetMember(revolver, "inman");

                    if (inman == null)
                    {
                        log.Warn(
                            "[Weapon7] Revolver.inman is null.");
                    }
                    else
                    {
                        object inputSource =
                            GetMember(inman, "InputSource");

                        if (inputSource == null)
                        {
                            log.Warn(
                                "[Weapon7] Revolver.inman.InputSource is null.");
                        }
                        else
                        {
                            object fire1 =
                                GetMember(inputSource, "Fire1");

                            log.Info(
                                "[Weapon7] InputSource.Fire1 = " +
                                (fire1 == null ? "null" : "available"));

                            if (fire1 != null)
                            {
                                object wasPerformed = GetMember(
                                    fire1, "WasPerformedThisFrame");

                                object isPressed = GetMember(
                                    fire1, "IsPressed");

                                log.Info(
                                    "[Weapon7] Fire1.WasPerformedThisFrame = " +
                                    (wasPerformed == null
                                        ? "unavailable"
                                        : wasPerformed.ToString()));

                                log.Info(
                                    "[Weapon7] Fire1.IsPressed = " +
                                    (isPressed == null
                                        ? "unavailable"
                                        : isPressed.ToString()));
                            }
                        }
                    }

                    log.Info(
                        "[Weapon7] ===== END REVOLVER STATE =====");
                }
            }
            catch (Exception ex)
            {
                log.Error(
                    "[Weapon7] Revolver runtime diagnostics failed: " +
                    Unwrap(ex));
            }
        }

        private static void LogRuntimeMember(
            object instance,
            string memberName,
            ManualLogSourceProxy log)
        {
            try
            {
                object value = GetMember(instance, memberName);

                log.Info(
                    "[Weapon7] " + memberName + " = " +
                    (value == null ? "null" : value.ToString()));
            }
            catch (Exception ex)
            {
                log.Warn(
                    "[Weapon7] Could not read " + memberName +
                    ": " + Unwrap(ex));
            }
        }

        internal static void LogWeaponComponents(
            GameObject weapon,
            ManualLogSourceProxy log)
        {
            if (weapon == null || log == null)
                return;

            log.Info(
                "[Weapon7] COMPONENT DIAGNOSTICS: " + weapon.name);

            string[] names =
            {
                "Revolver",
                "WeaponIcon",
                "WeaponIdentifier",
                "WeaponPos",
                "WeaponCharges",
                "GunSetter",
                "GunControl"
            };

            foreach (string name in names)
            {
                try
                {
                    Type componentType = FindType(name);

                    if (componentType == null)
                    {
                        log.Info(
                            "[Weapon7] Type not found: " + name);
                        continue;
                    }

                    object components = Invoke(
                        weapon,
                        "GetComponentsInChildren",
                        Il2CppType.From(componentType),
                        true);

                    int count = Count(components);

                    log.Info(
                        "[Weapon7] " + name +
                        " components found: " + count);

                    for (int i = 0; i < count; i++)
                    {
                        object component = WrapInstance(
                            componentType,
                            Item(components, i));

                        if (component == null)
                            continue;

                        log.Info(
                            "[Weapon7] " + name +
                            "[" + i + "] pointer=0x" +
                            GetPointer(component).ToString("X"));

                        if (name == "Revolver")
                        {
                            string[] members =
                            {
                                "inman",
                                "wid",
                                "gunVariation",
                                "altVersion",
                                "gunAud",
                                "gunBarrel",
                                "revolverBeam",
                                "revolverBeamSuper",
                                "chargeEffect",
                                "coin",
                                "gc",
                                "wpos",
                                "wc"
                            };

                            foreach (string member in members)
                                LogMember(component, member, log);

                            LogComponentMethods(
                                componentType,
                                new[]
                                {
                                    "Shoot",
                                    "ThrowCoin",
                                    "ReadyGun",
                                    "Click",
                                    "InstaClick",
                                    "MaxCharge",
                                    "OnEnable",
                                    "OnDisable",
                                    "Update"
                                },
                                log);
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Warn(
                        "[Weapon7] Inspection failed for " +
                        name + ": " + Unwrap(ex));
                }
            }
        }

        private static void LogMember(
            object instance,
            string memberName,
            ManualLogSourceProxy log)
        {
            try
            {
                object value = GetMember(instance, memberName);

                log.Info(
                    "[Weapon7]   " + memberName + " = " +
                    (value == null ? "null" : TypeName(value)));
            }
            catch (Exception ex)
            {
                log.Warn(
                    "[Weapon7]   Could not inspect " +
                    memberName + ": " + Unwrap(ex));
            }
        }

        private static void LogComponentMethods(
            Type type,
            string[] wantedNames,
            ManualLogSourceProxy log)
        {
            if (type == null)
                return;

            for (Type current = type;
                 current != null;
                 current = current.BaseType)
            {
                foreach (MethodInfo method in current.GetMethods(
                             BindingFlags.Instance |
                             BindingFlags.Public |
                             BindingFlags.NonPublic |
                             BindingFlags.DeclaredOnly))
                {
                    bool wanted = false;

                    foreach (string wantedName in wantedNames)
                    {
                        if (method.Name == wantedName)
                        {
                            wanted = true;
                            break;
                        }
                    }

                    if (!wanted)
                        continue;

                    ParameterInfo[] parameters =
                        method.GetParameters();

                    string signature = method.Name + "(";

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (i > 0)
                            signature += ", ";

                        signature += parameters[i].ParameterType.Name;
                    }

                    signature += ")";

                    log.Info(
                        "[Weapon7]   Revolver method: " +
                        signature);
                }
            }
        }

        internal static void LogWeaponDescriptors(
            object gunControl, ManualLogSourceProxy log)
        {
            object icons = GetMember(
                gunControl, "currentWeaponIcons");

            log.Info(
                "[Weapon7] currentWeaponIcons count = " +
                Count(icons));

            for (int i = 0; i < Count(icons); i++)
            {
                object icon = Item(icons, i);
                object descriptor = GetMember(
                    icon, "weaponDescriptor");

                object name = GetMember(
                    descriptor, "weaponName");

                log.Info(
                    "[Weapon7] Icon[" + i + "] descriptor=" +
                    TypeName(descriptor) +
                    " name=" + (name ?? "null"));
            }
        }

        internal static void LogWheelSegments(
            ManualLogSourceProxy log)
        {
            Type type = FindType("WeaponWheel");

            if (type == null)
            {
                log.Warn("[Weapon7] WeaponWheel type not found.");
                return;
            }

            object wheel = null;

            try
            {
                for (Type current = type;
                     current != null && wheel == null;
                     current = current.BaseType)
                {
                    PropertyInfo property = current.GetProperty(
                        "Instance",
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.Static |
                        BindingFlags.DeclaredOnly);

                    if (property == null)
                        continue;

                    MethodInfo getter = property.GetGetMethod(true);

                    if (getter != null)
                        wheel = getter.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                log.Warn(
                    "[Weapon7] Singleton lookup failed: " +
                    Unwrap(ex));
            }

            if (wheel == null)
            {
                log.Warn(
                    "[Weapon7] WeaponWheel.Instance is null " +
                    "or inaccessible.");

                return;
            }

            object segments = GetMember(wheel, "segments");

            log.Info(
                "[Weapon7] Singleton wheel found; segments=" +
                Count(segments));

            log.Info(
                "[Weapon7] segmentCount=" +
                (GetMember(wheel, "segmentCount") ?? "null"));

            for (int i = 0; i < Count(segments); i++)
            {
                object segment = Item(segments, i);

                object descriptor = GetMember(
                    segment, "descriptor");

                object name = GetMember(
                    descriptor, "weaponName");

                object slot = GetMember(
                    segment, "slotIndex");

                log.Info(
                    "[Weapon7] Segment[" + i + "] slot=" +
                    (slot ?? "null") +
                    " descriptor=" + TypeName(descriptor) +
                    " name=" + (name ?? "null"));
            }
        }

        internal static void LogRelevantMethods(
            Type type, ManualLogSourceProxy log)
        {
            log.Info(
                "[Weapon7] Inspecting relevant GunControl methods:");

            for (Type current = type;
                 current != null;
                 current = current.BaseType)
            {
                foreach (MethodInfo method in current.GetMethods(
                             BindingFlags.Instance |
                             BindingFlags.Public |
                             BindingFlags.NonPublic |
                             BindingFlags.DeclaredOnly))
                {
                    string name = method.Name;

                    if (name.IndexOf(
                            "weapon",
                            StringComparison.OrdinalIgnoreCase) < 0 &&
                        name.IndexOf(
                            "slot",
                            StringComparison.OrdinalIgnoreCase) < 0 &&
                        name.IndexOf(
                            "switch",
                            StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    ParameterInfo[] p = method.GetParameters();
                    string signature = name + "(";

                    for (int i = 0; i < p.Length; i++)
                    {
                        if (i > 0)
                            signature += ", ";

                        signature += p[i].ParameterType.Name;
                    }

                    log.Info(
                        "[Weapon7] GunControl method: " +
                        signature + ")");
                }
            }
        }

        internal static string TypeName(object value)
        {
            if (value == null)
                return "null";

            Type managedType = value.GetType();
            string fullName = managedType.FullName;

            if (!string.IsNullOrEmpty(fullName) &&
                fullName != "UnityEngine.Component" &&
                fullName != "UnityEngine.Object")
            {
                return fullName;
            }

            Il2CppObjectBase native = value as Il2CppObjectBase;

            if (native != null)
            {
                return managedType.FullName +
                       " [IL2CPP pointer=0x" +
                       native.Pointer.ToString("X") + "]";
            }

            return managedType.FullName ?? managedType.Name;
        }

        internal static Exception Unwrap(Exception ex)
        {
            while (ex is TargetInvocationException &&
                   ex.InnerException != null)
                ex = ex.InnerException;

            return ex;
        }
    }
}
