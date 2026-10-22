
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

namespace ULTRADROID.FourthVariantTest
{
    [BepInPlugin(
        "somerandommaid.ultradroid.fourthvarianttest",
        "ULTRADROID Fourth Variant Test",
        "0.1.0")]
    [BepInDependency(
        ULTRADROIDControls.PluginGuid,
        BepInDependency.DependencyFlags.HardDependency)]
    public sealed class FourthVariantPlugin : BasePlugin
    {
        public override void Load()
        {
            FourthVariantRuntime.Log = Log;
            AddComponent<FourthVariantRuntime>();
            Log.LogInfo("[FourthVariant] Plugin loaded.");
        }
    }

    public sealed class FourthVariantRuntime : MonoBehaviour
    {
        internal static ManualLogSource Log;

        private object gunControl;
        private GameObject goldRevolver;
        private GameObject pierceTemplate;
        private IntPtr lastGunPointer = IntPtr.Zero;
        private string lastRevolverListState = null;
        private string lastColorDiagnostics;

        private int lastCheckFrame = -120;
        private bool buttonRegistered;
        private bool setupComplete;

        public FourthVariantRuntime(IntPtr ptr) : base(ptr) { }

        public void Update()
        {
            if (Log == null)
                return;

            try
            {
                string scene = SceneManager.GetActiveScene().name;

                if (scene == "Bootstrap" || scene == "Intro")
                    return;

                RegisterButton();

                if (Time.frameCount - lastCheckFrame < 120)
                    return;

                lastCheckFrame = Time.frameCount;

                object found = Helpers.FindReadyGunControl();

                if (found == null)
                {
                    if (setupComplete)
                        Log.LogWarning(
                            "[FourthVariant] GunControl is not ready.");

                    gunControl = null;
                    goldRevolver = null;
                    lastGunPointer = IntPtr.Zero;
                    setupComplete = false;
                    return;
                }

                IntPtr pointer = Helpers.GetPointer(found);

                if (pointer == IntPtr.Zero)
                    return;

                if (pointer != lastGunPointer)
                {
                    lastGunPointer = pointer;
                    gunControl = found;
                    goldRevolver = null;
                    lastRevolverListState = null;
                    setupComplete = false;

                    Log.LogInfo(
                        "[FourthVariant] Found GunControl instance.");
                }
                else
                {
                    gunControl = found;
                }

                // Repair the fourth variant if the game rebuilds its lists.
                setupComplete = EnsureFourthVariant();

                if (setupComplete)
                    LogColorDiagnostics();
            }
            catch (Exception ex)
            {
                Log.LogError(
                    "[FourthVariant] Update error: " +
                    Helpers.Unwrap(ex));
            }
        }

        private void RegisterButton()
        {
            if (buttonRegistered)
                return;

            try
            {
                ULTRADROIDControls.AddButton(new ButtonConfig
                {
                    Id = "ULTRADROID.FourthVariant.Select",
                    Label = "G4",
                    PositionOffset = new Vector2(120f, 135f),
                    ScaleMultiplier = 1f,
                    ApplyTint = false,
                    OnClick = SelectGoldRevolver
                });

                buttonRegistered = true;

                Log.LogInfo(
                    "[FourthVariant] G4 button registered.");
            }
            catch (Exception ex)
            {
                Log.LogError(
                    "[FourthVariant] Button registration failed: " +
                    Helpers.Unwrap(ex));
            }
        }

        private bool EnsureFourthVariant()
        {
            object slots = Helpers.GetMember(gunControl, "slots");

            if (slots == null || Helpers.Count(slots) < 6)
            {
                Log.LogWarning(
                    "[FourthVariant] GunControl.slots is unavailable.");
                return false;
            }

            // slots[0] is the Revolver family.
            object revolverSlot = Helpers.Item(slots, 0);

            if (revolverSlot == null)
                return false;

            int count = Helpers.Count(revolverSlot);
            bool goldFoundInList = false;
            bool hasPierce = false;
            bool hasRicochet = false;
            bool hasTwirl = false;

            string listState = "Count=" + count;

            for (int i = 0; i < count; i++)
            {
                GameObject item =
                    Helpers.Item(revolverSlot, i) as GameObject;

                string itemName = item != null
                    ? item.name
                    : "<null>";

                listState += " | Revolver[" + i + "]=" + itemName;

                if (item == null)
                    continue;

                if (item.name == "ULTRADROID_FOURTH_GOLD_REVOLVER")
                {
                    goldRevolver = item;
                    goldFoundInList = true;
                    continue;
                }

                if (itemName.IndexOf(
                        "Pierce", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hasPierce = true;

                    if (pierceTemplate == null)
                    {
                        GameObject template =
                            UnityEngine.Object.Instantiate(item);

                        if (template != null)
                        {
                            template.name = "ULTRADROID_PIERCE_TEMPLATE";
                            template.transform.SetParent(null);
                            template.SetActive(false);

                            UnityEngine.Object.DontDestroyOnLoad(template);

                            pierceTemplate = template;

                            Log.LogInfo(
                                "[FourthVariant] Created persistent Pierce template.");
                        }
                        else
                        {
                            Log.LogWarning(
                                "[FourthVariant] Failed to create Pierce template.");
                        }
                    }
                }

                if (itemName.IndexOf(
                        "Ricochet", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hasRicochet = true;
                }

                if (itemName.IndexOf(
                        "Twirl", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hasTwirl = true;
                }
            }

            if (listState != lastRevolverListState)
            {
                lastRevolverListState = listState;

                Log.LogInfo(
                    "[FourthVariant] Revolver list changed: " +
                    listState);
            }

            if (goldFoundInList)
                return true;

            if (goldRevolver != null)
            {
                if (Helpers.Add(revolverSlot, goldRevolver))
                {
                    lastRevolverListState = null;

                    Log.LogInfo(
                        "[FourthVariant] Restored existing Gold Revolver. " +
                        "New count=" + Helpers.Count(revolverSlot));

                    return true;
                }

                Log.LogWarning(
                    "[FourthVariant] Existing Gold object could not be restored. " +
                    "Will try creating a replacement.");

                goldRevolver = null;
            }

            bool validTwoVariantList =
                count == 2 &&
                hasPierce &&
                hasTwirl &&
                !hasRicochet;

            bool validThreeVariantList =
                count == 3 &&
                hasPierce &&
                hasRicochet &&
                hasTwirl;

            if (pierceTemplate == null)
            {
                Log.LogWarning(
                    "[FourthVariant] Pierce template is unavailable. Count=" +
                    count);

                return false;
            }

            if (!validTwoVariantList && !validThreeVariantList)
            {
                Log.LogInfo(
                    "[FourthVariant] Vanilla list is incomplete (Count=" +
                    count +
                    "). Attempting recovery using the cached Pierce template.");
            }

            GameObject newGold =
                UnityEngine.Object.Instantiate(pierceTemplate);

            if (newGold == null)
            {
                Log.LogError(
                    "[FourthVariant] Failed to clone Pierce template.");
                return false;
            }

            newGold.name = "ULTRADROID_FOURTH_GOLD_REVOLVER";
            newGold.SetActive(false);

            if (!Helpers.AssignGoldDescriptor(
                    newGold,
                    "ULTRADROID Gold Revolver IV"))
            {
                UnityEngine.Object.Destroy(newGold);

                Log.LogError(
                    "[FourthVariant] Gold descriptor setup failed.");

                return false;
            }

            if (!Helpers.Add(revolverSlot, newGold))
            {
                UnityEngine.Object.Destroy(newGold);

                Log.LogError(
                    "[FourthVariant] Could not append Gold Revolver.");

                return false;
            }

            goldRevolver = newGold;
            lastRevolverListState = null;

            Log.LogInfo(
                "[FourthVariant] Gold Revolver registered successfully. " +
                "Vanilla count=" +
                (validTwoVariantList ? "2 (Marksman disabled)" :
                                       "3 (Marksman enabled)") +
                " | Total count=" + Helpers.Count(revolverSlot));

            return true;
        }

        private void LogColorDiagnostics()
        {
            try
            {
                Type settingsType = Helpers.FindType("ColorBlindSettings");

                if (settingsType == null)
                {
                    Log.LogWarning(
                        "[FourthVariant] ColorBlindSettings type not found.");
                    return;
                }

                object settingsObjects =
                    UnityEngine.Object.FindObjectsOfType(
                        Il2CppType.From(settingsType));

                object settings = null;

                for (int i = 0; i < Helpers.Count(settingsObjects); i++)
                {
                    settings = Helpers.Wrap(
                        settingsType,
                        Helpers.Item(settingsObjects, i));

                    if (settings != null)
                        break;
                }

                if (settings == null)
                {
                    Log.LogInfo(
                        "[FourthVariant] ColorBlindSettings instance not found yet.");
                    return;
                }

                object colors = Helpers.GetMember(
                    settings, "variationColors");

                Type iconType = Helpers.FindType("WeaponIcon");
                string iconInfo = "WeaponIcon type not found";

                if (iconType != null && goldRevolver != null)
                {
                    object icons = Helpers.Invoke(
                        goldRevolver,
                        "GetComponentsInChildren",
                        Il2CppType.From(iconType),
                        true);

                    if (Helpers.Count(icons) > 0)
                    {
                        object icon = Helpers.Wrap(
                            iconType,
                            Helpers.Item(icons, 0));

                        object descriptor = Helpers.GetMember(
                            icon, "weaponDescriptor");

                        object variant = Helpers.GetMember(
                            descriptor, "variationColor");

                        object renderers = Helpers.GetMember(
                            icon, "variationColoredRenderers");

                        object images = Helpers.GetMember(
                            icon, "variationColoredImages");

                        iconInfo =
                            "DescriptorVariant=" + variant +
                            " | RendererEntries=" + Helpers.Count(renderers) +
                            " | ImageEntries=" + Helpers.Count(images);
                    }
                }

                string state =
                    "ColorArrayLength=" + Helpers.Count(colors) +
                    " | " + iconInfo;

                if (state != lastColorDiagnostics)
                {
                    lastColorDiagnostics = state;

                    Log.LogInfo(
                        "[FourthVariant] Color diagnostics: " + state);
                }
            }
            catch (Exception ex)
            {
                Log.LogError(
                    "[FourthVariant] Color diagnostics failed: " +
                    Helpers.Unwrap(ex));
            }
        }

        private void SelectGoldRevolver()
        {
            try
            {
                object found = Helpers.FindReadyGunControl();

                if (found == null)
                {
                    Log.LogWarning(
                        "[FourthVariant] GunControl is not ready.");
                    return;
                }

                gunControl = found;
                setupComplete = EnsureFourthVariant();

                if (!setupComplete || goldRevolver == null)
                {
                    Log.LogWarning(
                        "[FourthVariant] Cannot select Gold: setup failed.");
                    return;
                }

                Log.LogInfo("[FourthVariant] === BEFORE SWITCH ===");
                LogSelectionState();

                Helpers.Invoke(gunControl, "SwitchWeapon", 1, false);
                Helpers.Invoke(gunControl, "SwitchVariant", 4);

                Log.LogInfo("[FourthVariant] === AFTER SWITCH ===");
                LogSelectionState();

                Log.LogInfo(
                    "[FourthVariant] SwitchVariant(4) call completed.");
            }
            catch (Exception ex)
            {
                Log.LogError(
                    "[FourthVariant] Selection failed: " +
                    Helpers.Unwrap(ex));
            }
        }

        private void LogSelectionState()
        {
            try
            {
                object slot = Helpers.GetMember(gunControl, "currentSlot");
                object variation = Helpers.GetMember(
                    gunControl, "currentVariation");
                object current = Helpers.GetMember(
                    gunControl, "currentWeapon");

                GameObject currentObject = current as GameObject;

                string currentName = currentObject != null
                    ? currentObject.name
                    : current == null
                        ? "<null>"
                        : current.GetType().FullName;

                string currentPointer = currentObject != null
                    ? currentObject.GetInstanceID().ToString()
                    : "n/a";

                string goldName = goldRevolver != null
                    ? goldRevolver.name
                    : "<null>";

                string goldPointer = goldRevolver != null
                    ? goldRevolver.GetInstanceID().ToString()
                    : "n/a";

                string goldActive = goldRevolver != null
                    ? goldRevolver.activeSelf.ToString()
                    : "n/a";

                string goldActiveInHierarchy = goldRevolver != null
                    ? goldRevolver.activeInHierarchy.ToString()
                    : "n/a";

                bool currentIsGold =
                    currentObject != null &&
                    goldRevolver != null &&
                    currentObject.GetInstanceID() ==
                    goldRevolver.GetInstanceID();

                Log.LogInfo(
                    "[FourthVariant] State: " +
                    "currentSlot=" + slot +
                    " | currentVariation=" + variation +
                    " | currentWeapon=" + currentName +
                    " | currentWeaponID=" + currentPointer +
                    " | CurrentIsGold=" + currentIsGold +
                    " | GoldName=" + goldName +
                    " | GoldID=" + goldPointer +
                    " | GoldActiveSelf=" + goldActive +
                    " | GoldActiveInHierarchy=" +
                    goldActiveInHierarchy);
            }
            catch (Exception ex)
            {
                Log.LogError(
                    "[FourthVariant] State diagnostics failed: " +
                    Helpers.Unwrap(ex));
            }
        }
    }

    internal static class Helpers
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
                object candidate = Wrap(type, Item(found, i));
                object slots = GetMember(candidate, "slots");

                if (slots != null && Count(slots) >= 6)
                    return candidate;
            }

            return null;
        }

        internal static object Wrap(Type type, object raw)
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

        internal static IntPtr GetPointer(object value)
        {
            Il2CppObjectBase native = value as Il2CppObjectBase;

            return native == null
                ? IntPtr.Zero
                : native.Pointer;
        }

        internal static object GetMember(object instance, string name)
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

        internal static int Count(object collection)
        {
            if (collection == null)
                return 0;

            if (collection is Array array)
                return array.Length;

            object count = GetMember(collection, "Count");

            if (count != null)
            {
                try
                {
                    return Convert.ToInt32(count);
                }
                catch
                {
                }
            }

            object length = GetMember(collection, "Length");

            if (length != null)
            {
                try
                {
                    return Convert.ToInt32(length);
                }
                catch
                {
                }
            }

            return 0;
        }

        internal static object Item(object collection, int index)
        {
            if (collection == null || index < 0)
                return null;

            if (collection is Array array)
                return index < array.Length
                    ? array.GetValue(index)
                    : null;

            foreach (MethodInfo method in collection.GetType().GetMethods(
                         BindingFlags.Instance |
                         BindingFlags.Public |
                         BindingFlags.NonPublic))
            {
                if (method.Name != "get_Item" ||
                    method.GetParameters().Length != 1)
                    continue;

                try
                {
                    return method.Invoke(collection, new object[] { index });
                }
                catch
                {
                }
            }

            return null;
        }

        internal static bool Add(object collection, object value)
        {
            if (collection == null || value == null)
                return false;

            foreach (MethodInfo method in collection.GetType().GetMethods(
                         BindingFlags.Instance |
                         BindingFlags.Public |
                         BindingFlags.NonPublic))
            {
                if (method.Name != "Add" ||
                    method.GetParameters().Length != 1)
                    continue;

                try
                {
                    method.Invoke(collection, new[] { value });
                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

        private static object CreateDescriptor(Type descriptorType)
        {
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
                    object result = method
                        .MakeGenericMethod(descriptorType)
                        .Invoke(null, null);

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

                object argument =
                    parameterType == typeof(Type)
                        ? descriptorType
                        : il2CppType != null &&
                          parameterType.IsInstanceOfType(il2CppType)
                            ? il2CppType
                            : null;

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

        internal static bool AssignGoldDescriptor(
            GameObject weapon,
            string uniqueName)
        {
            Type iconType = FindType("WeaponIcon");
            Type descriptorType = FindType("WeaponDescriptor");

            if (weapon == null ||
                iconType == null ||
                descriptorType == null)
                return false;

            object icons;

            try
            {
                icons = Invoke(
                    weapon,
                    "GetComponentsInChildren",
                    Il2CppType.From(iconType),
                    true);
            }
            catch
            {
                return false;
            }

            if (Count(icons) == 0)
                return false;

            object firstIcon = Wrap(iconType, Item(icons, 0));

            if (firstIcon == null)
                return false;

            object original = GetMember(firstIcon, "weaponDescriptor");
            object descriptor = CreateDescriptor(descriptorType);

            if (descriptor == null)
                return false;

            if (original != null)
            {
                SetMember(
                    descriptor, "icon", GetMember(original, "icon"));

                SetMember(
                    descriptor, "glowIcon",
                    GetMember(original, "glowIcon"));
            }

            Type variantType = FindType("WeaponVariant");

            if (variantType == null || !variantType.IsEnum)
                return false;

            object gold = Enum.Parse(variantType, "GoldVariant");

            if (!SetMember(descriptor, "variationColor", gold) ||
                !SetMember(descriptor, "weaponName", uniqueName))
                return false;

            int assigned = 0;

            for (int i = 0; i < Count(icons); i++)
            {
                object icon = Wrap(iconType, Item(icons, i));

                if (icon != null &&
                    SetMember(icon, "weaponDescriptor", descriptor))
                    assigned++;
            }

            return assigned > 0;
        }

        private static bool SetMember(
            object instance,
            string name,
            object value)
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

        internal static object Invoke(
            object instance,
            string name,
            params object[] args)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            foreach (MethodInfo method in instance.GetType().GetMethods(
                         BindingFlags.Instance |
                         BindingFlags.Public |
                         BindingFlags.NonPublic))
            {
                if (method.Name != name ||
                    method.GetParameters().Length != args.Length)
                    continue;

                try
                {
                    return method.Invoke(instance, args);
                }
                catch (ArgumentException)
                {
                }
            }

            throw new MissingMethodException(
                instance.GetType().FullName, name);
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
