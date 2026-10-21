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

            try
            {
                AddComponent<Weapon7Runtime>();
                Log.LogInfo("[Weapon7] Test plugin loaded.");
            }
            catch (Exception ex)
            {
                Log.LogError("[Weapon7] Startup failed: " + ex);
            }
        }
    }

    internal sealed class ManualLogSourceProxy
    {
        private readonly ManualLogSource source;

        internal ManualLogSourceProxy(ManualLogSource value)
        {
            source = value;
        }

        internal void Info(string message)
        {
            source.LogInfo(message);
        }

        internal void Warn(string message)
        {
            source.LogWarning(message);
        }

        internal void Error(string message)
        {
            source.LogError(message);
        }
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

        private bool buttonRegistered;
        private bool methodDiagnosticsLogged;

        // Prevent recalculating the wheel on every periodic check.
        private bool slotSetupComplete;
        private bool refreshAttempted;

        public Weapon7Runtime(IntPtr ptr) : base(ptr)
        {
        }

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

                object found = Weapon7Helpers.FindReadyGunControl();

                if (found == null)
                {
                    if (Time.frameCount - lastWaitingLogFrame >= 600)
                    {
                        lastWaitingLogFrame = Time.frameCount;

                        Log.Warn(
                            "[Weapon7] Waiting for a GunControl instance " +
                            "with six initialized weapon slots.");
                    }

                    return;
                }

                IntPtr pointer = Weapon7Helpers.GetPointer(found);

                if (pointer == IntPtr.Zero)
                    return;

                if (pointer != lastGunPointer)
                {
                    lastGunPointer = pointer;
                    gunControl = found;
                    testWeapon = null;

                    slotSetupComplete = false;
                    refreshAttempted = false;
                    methodDiagnosticsLogged = false;

                    Log.Info(
                        "[Weapon7] Found initialized GunControl: " +
                        Weapon7Helpers.TypeName(found));
                }
                else
                {
                    gunControl = found;
                }

                EnsureSlot7();
            }
            catch (Exception ex)
            {
                Log.Error(
                    "[Weapon7] Runtime error: " +
                    Weapon7Helpers.Unwrap(ex));
            }
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
                    gunControl,
                    "slots");

                if (slots == null)
                {
                    Log.Warn(
                        "[Weapon7] GunControl.slots could not be read.");
                    return false;
                }

                int slotCount = Weapon7Helpers.Count(slots);

                if (slotCount < 6)
                {
                    Log.Warn(
                        "[Weapon7] Slots are not ready. Count = " +
                        slotCount);

                    return false;
                }

                object slot7 = slotCount >= 7
                    ? Weapon7Helpers.Item(slots, 6)
                    : null;

                if (slotCount >= 7 && slot7 == null)
                {
                    Log.Error(
                        "[Weapon7] The seventh slot exists by count, " +
                        "but its list could not be read.");

                    return false;
                }

                if (slot7 != null && testWeapon == null)
                {
                    int existingCount = Weapon7Helpers.Count(slot7);

                    for (int i = 0; i < existingCount; i++)
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
                            "[Weapon7] Unexpected slots collection type: " +
                            slotsType.FullName);

                        return false;
                    }

                    Type[] genericArguments =
                        slotsType.GetGenericArguments();

                    if (genericArguments.Length != 1)
                    {
                        Log.Error(
                            "[Weapon7] Could not determine the slot type.");

                        return false;
                    }

                    Type innerListType = genericArguments[0];

                    object newSlot = Activator.CreateInstance(
                        innerListType);

                    if (newSlot == null)
                    {
                        Log.Error(
                            "[Weapon7] Could not create the seventh slot.");

                        return false;
                    }

                    Weapon7Helpers.Add(slots, newSlot);
                    slot7 = newSlot;

                    Log.Info(
                        "[Weapon7] Added slot 7 to GunControl.slots.");
                }

                if (slot7 == null)
                    slot7 = Weapon7Helpers.Item(slots, 6);

                if (slot7 == null)
                {
                    Log.Error(
                        "[Weapon7] Could not retrieve slot 7.");

                    return false;
                }

                if (testWeapon == null)
                {
                    GameObject template =
                        Weapon7Helpers.FindTemplateWeapon(slots);

                    if (template == null)
                    {
                        Log.Warn(
                            "[Weapon7] No existing weapon is available " +
                            "to clone yet.");

                        return false;
                    }

                    testWeapon = UnityEngine.Object.Instantiate(
                        template,
                        template.transform.parent);

                    if (testWeapon == null)
                    {
                        Log.Error(
                            "[Weapon7] Weapon cloning failed.");

                        return false;
                    }

                    testWeapon.name = "ULTRADROID_TEST_WEAPON7";
                    testWeapon.SetActive(false);

                    Log.Info(
                        "[Weapon7] Created temporary test weapon from: " +
                        template.name);
                }

                if (!Weapon7Helpers.Contains(slot7, testWeapon))
                {
                    Weapon7Helpers.Add(slot7, testWeapon);

                    Log.Info(
                        "[Weapon7] Added temporary weapon to slot 7.");
                }

                if (!methodDiagnosticsLogged)
                {
                    methodDiagnosticsLogged = true;

                    Weapon7Helpers.LogRelevantMethods(
                        gunControl.GetType(),
                        Log);
                }

                // Experimental: recalculate the wheel once after slot setup.
                // This does not change the Controls API.
                if (!refreshAttempted)
                {
                    refreshAttempted = true;

                    try
                    {
                        object result = Weapon7Helpers.Invoke(
                            gunControl,
                            "CalculateSlotCount");

                        Log.Info(
                            "[Weapon7] CalculateSlotCount invoked after " +
                            "slot 7 setup.");
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(
                            "[Weapon7] CalculateSlotCount invocation failed: " +
                            Weapon7Helpers.Unwrap(ex));
                    }
                }

                slotSetupComplete = true;

                Log.Info(
                    "[Weapon7] Slot setup complete. Outer slot count = " +
                    Weapon7Helpers.Count(slots) +
                    ", slot 7 weapon count = " +
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

        private void SwitchToSlot7()
        {
            if (Log == null)
                return;

            try
            {
                object found = Weapon7Helpers.FindReadyGunControl();

                if (found == null)
                {
                    Log.Warn(
                        "[Weapon7] Cannot switch: initialized GunControl " +
                        "was not found.");

                    return;
                }

                gunControl = found;
                lastGunPointer = Weapon7Helpers.GetPointer(found);

                if (!EnsureSlot7())
                {
                    Log.Warn(
                        "[Weapon7] Cannot switch: slot 7 is not ready.");

                    return;
                }

                Weapon7Helpers.Invoke(
                    gunControl,
                    "SwitchWeapon",
                    7,
                    false);

                Log.Info(
                    "[Weapon7] SwitchWeapon(7, false) was invoked.");
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
                Type type = assembly.GetType(name, false);

                if (type != null)
                    return type;

                try
                {
                    foreach (Type candidate in assembly.GetTypes())
                    {
                        if (candidate != null &&
                            candidate.Name == name)
                        {
                            return candidate;
                        }
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
                        {
                            return candidate;
                        }
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

            if (found == null)
                return null;

            int count = Count(found);

            for (int i = 0; i < count; i++)
            {
                object raw = Item(found, i);

                if (raw == null)
                    continue;

                object candidate = WrapInstance(type, raw);

                if (candidate == null)
                    continue;

                object slots = GetMember(candidate, "slots");

                if (slots == null)
                    continue;

                if (Count(slots) >= 6)
                    return candidate;
            }

            return null;
        }

        private static object WrapInstance(Type type, object raw)
        {
            if (type.IsInstanceOfType(raw))
                return raw;

            Il2CppObjectBase native = raw as Il2CppObjectBase;

            if (native == null || native.Pointer == IntPtr.Zero)
                return null;

            ConstructorInfo constructor = type.GetConstructor(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic,
                null,
                new[] { typeof(IntPtr) },
                null);

            if (constructor == null)
                return null;

            return constructor.Invoke(
                new object[] { native.Pointer });
        }

        internal static IntPtr GetPointer(object value)
        {
            Il2CppObjectBase native = value as Il2CppObjectBase;

            return native == null
                ? IntPtr.Zero
                : native.Pointer;
        }

        internal static object GetMember(
            object instance,
            string name)
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

            object value = GetMember(collection, "Count");

            if (value == null)
                return 0;

            return Convert.ToInt32(value);
        }

        internal static object Item(
            object collection,
            int index)
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
                PropertyInfo property = collection.GetType().GetProperty(
                    "Item",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

                if (property != null)
                {
                    return property.GetValue(
                        collection,
                        new object[] { index });
                }
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

                    ParameterInfo[] parameters = method.GetParameters();

                    if (parameters.Length == 1 &&
                        parameters[0].ParameterType == typeof(int))
                    {
                        return method.Invoke(
                            collection,
                            new object[] { index });
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        internal static void Add(
            object collection,
            object value)
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

                ParameterInfo[] parameters = method.GetParameters();

                if (parameters.Length != 1)
                    continue;

                Type expected = parameters[0].ParameterType;

                if (value != null &&
                    !expected.IsInstanceOfType(value) &&
                    expected != value.GetType())
                {
                    continue;
                }

                method.Invoke(collection, new[] { value });
                return;
            }

            throw new MissingMethodException(
                collection.GetType().FullName,
                "Add");
        }

        internal static bool Contains(
            object collection,
            GameObject target)
        {
            if (collection == null || target == null)
                return false;

            int count = Count(collection);

            for (int i = 0; i < count; i++)
            {
                GameObject item = Item(collection, i) as GameObject;

                if (item == target)
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

                if (slot == null)
                    continue;

                int count = Count(slot);

                for (int j = 0; j < count; j++)
                {
                    GameObject weapon = Item(slot, j) as GameObject;

                    if (weapon != null &&
                        weapon.name != "ULTRADROID_TEST_WEAPON7")
                    {
                        return weapon;
                    }
                }
            }

            return null;
        }

        internal static object Invoke(
            object instance,
            string name,
            params object[] arguments)
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

                    ParameterInfo[] parameters = method.GetParameters();

                    if (parameters.Length != arguments.Length)
                        continue;

                    bool matches = true;

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (arguments[i] == null)
                        {
                            if (parameters[i].ParameterType.IsValueType &&
                                Nullable.GetUnderlyingType(
                                    parameters[i].ParameterType) == null)
                            {
                                matches = false;
                                break;
                            }

                            continue;
                        }

                        Type expected = parameters[i].ParameterType;
                        Type actual = arguments[i].GetType();

                        if (!expected.IsAssignableFrom(actual) &&
                            expected != actual)
                        {
                            matches = false;
                            break;
                        }
                    }

                    if (matches)
                        return method.Invoke(instance, arguments);
                }
            }

            throw new MissingMethodException(
                instance.GetType().FullName,
                name);
        }

        internal static void LogRelevantMethods(
            Type type,
            ManualLogSourceProxy log)
        {
            log.Info("[Weapon7] Inspecting relevant GunControl methods:");

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
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = method.GetParameters();

                    string signature = name + "(";

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (i > 0)
                            signature += ", ";

                        signature += parameters[i].ParameterType.Name;
                    }

                    signature += ")";

                    log.Info("[Weapon7] GunControl method: " + signature);
                }
            }
        }

        internal static string TypeName(object value)
        {
            return value == null
                ? "null"
                : value.GetType().FullName;
        }

        internal static Exception Unwrap(Exception ex)
        {
            while (ex is TargetInvocationException &&
                   ex.InnerException != null)
            {
                ex = ex.InnerException;
            }

            return ex;
        }
    }
}
