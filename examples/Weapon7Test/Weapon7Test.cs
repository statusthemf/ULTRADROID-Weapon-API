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

        private IntPtr lastGunPointer = IntPtr.Zero;

        private int lastCheckFrame = -120;
        private int lastWaitingLogFrame = -120;
        private int equippedSeenFrame = -1;

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
                        ResetInstanceState();

                        lastGunPointer = pointer;
                        gunControl = found;

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
                    CheckEquippedWeaponDiagnostics();
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

            Log.Info(
                "[Weapon7] ===== END POST-EQUIP DIAGNOSTICS =====");
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

                SetMember(
                    newDescriptor,
                    "variationColor",
                    GetMember(originalDescriptor, "variationColor"));
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
