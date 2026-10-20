
using System;
using System.Reflection;
using BepInEx;
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
        internal static ManualLogSourceProxy Logger;

        public override void Load()
        {
            Weapon7Runtime.Log = Log;

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

    // Small wrapper keeps the runtime logger independent of the plugin class.
    internal sealed class ManualLogSourceProxy
    {
        private readonly BepInEx.Logging.ManualLogSource source;

        internal ManualLogSourceProxy(
            BepInEx.Logging.ManualLogSource value)
        {
            source = value;
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
        private bool buttonRegistered;

        public Weapon7Runtime(IntPtr ptr) : base(ptr) { }

        public void Update()
        {
            try
            {
                string scene = SceneManager.GetActiveScene().name;

                if (scene == "Bootstrap" || scene == "Intro")
                    return;

                RegisterButton();

                if (Time.frameCount - lastCheckFrame >= 120)
                {
                    lastCheckFrame = Time.frameCount;

                    object found = Weapon7Helpers.FindGunControl();

                    if (found != null)
                    {
                        IntPtr pointer = Weapon7Helpers.GetPointer(found);

                        if (pointer != lastGunPointer)
                        {
                            lastGunPointer = pointer;
                            gunControl = found;
                            testWeapon = null;

                            Log.Info(
                                "[Weapon7] Found GunControl: " +
                                Weapon7Helpers.TypeName(found));
                        }
                        else
                        {
                            gunControl = found;
                        }

                        EnsureSlot7();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("[Weapon7] Runtime error: " + ex);
            }
        }

        private void RegisterButton()
        {
            if (buttonRegistered)
                return;

            buttonRegistered = true;

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

                Log.Info("[Weapon7] W7 HUD button registered.");
            }
            catch (Exception ex)
            {
                Log.Error("[Weapon7] Button registration failed: " + ex);
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
                    throw new InvalidOperationException(
                        "GunControl.slots is null.");

                int slotCount = Weapon7Helpers.Count(slots);

                if (slotCount < 6)
                    throw new InvalidOperationException(
                        "The game's six original slots are not ready.");

                // Find an existing test weapon if the list was rebuilt.
                object slot7 = slotCount >= 7
                    ? Weapon7Helpers.Item(slots, 6)
                    : null;

                if (slot7 != null)
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

                // Create slot 7 only if the game's list has six entries.
                if (slotCount < 7)
                {
                    Type innerListType =
                        slots.GetType().GetGenericArguments()[0];

                    object newSlot =
                        Activator.CreateInstance(innerListType);

                    Weapon7Helpers.Add(slots, newSlot);
                    slot7 = newSlot;

                    Log.Info("[Weapon7] Added slot 7 to GunControl.slots.");
                }

                if (slot7 == null)
                    slot7 = Weapon7Helpers.Item(slots, 6);

                if (testWeapon == null)
                {
                    // Use an existing weapon as a temporary test placeholder.
                    GameObject template =
                        FindTemplateWeapon(slots);

                    if (template == null)
                    {
                        Log.Warn(
                            "[Weapon7] Waiting for an existing weapon " +
                            "to become available.");
                        return false;
                    }

                    testWeapon = UnityEngine.Object.Instantiate(
                        template, template.transform.parent);

                    if (testWeapon == null)
                        throw new InvalidOperationException(
                            "Could not clone a weapon.");

                    testWeapon.name = "ULTRADROID_TEST_WEAPON7";
                    testWeapon.SetActive(false);

                    Log.Info(
                        "[Weapon7] Created temporary test weapon from: " +
                        template.name);
                }

                if (!Weapon7Helpers.Contains(slot7, testWeapon))
                    Weapon7Helpers.Add(slot7, testWeapon);

                // Rebuild the game's weapon lookup and wheel data.
                Weapon7Helpers.Invoke(
                    gunControl, "UpdateWeaponList", false);

                Log.Info(
                    "[Weapon7] Slot 7 prepared. Total slots=" +
                    Weapon7Helpers.Count(slots));

                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[Weapon7] Slot setup failed: " +
                          Weapon7Helpers.Unwrap(ex));
                return false;
            }
        }

        private GameObject FindTemplateWeapon(object slots)
        {
            int limit = Math.Min(6, Weapon7Helpers.Count(slots));

            for (int i = 0; i < limit; i++)
            {
                object slot = Weapon7Helpers.Item(slots, i);
                int count = Weapon7Helpers.Count(slot);

                for (int j = 0; j < count; j++)
                {
                    GameObject weapon =
                        Weapon7Helpers.Item(slot, j) as GameObject;

                    if (weapon != null &&
                        weapon.name != "ULTRADROID_TEST_WEAPON7")
                    {
                        return weapon;
                    }
                }
            }

            return null;
        }

        private void SwitchToSlot7()
        {
            try
            {
                object found = Weapon7Helpers.FindGunControl();

                if (found == null)
                {
                    Log.Warn("[Weapon7] GunControl was not found.");
                    return;
                }

                gunControl = found;
                lastGunPointer = Weapon7Helpers.GetPointer(found);

                if (!EnsureSlot7())
                {
                    Log.Warn("[Weapon7] Slot 7 is not ready yet.");
                    return;
                }

                Weapon7Helpers.Invoke(
                    gunControl, "SwitchWeapon", 7, false);

                Log.Info("[Weapon7] Requested SwitchWeapon(7, false).");
            }
            catch (Exception ex)
            {
                Log.Error("[Weapon7] Switch failed: " +
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
                catch { }
            }

            return null;
        }

        internal static object FindGunControl()
        {
            Type type = FindType("GunControl");

            if (type == null)
                return null;

            object found = UnityEngine.Object.FindObjectsOfType(
                Il2CppType.From(type));

            if (Count(found) == 0)
                return null;

            object raw = Item(found, 0);

            if (raw == null || type.IsInstanceOfType(raw))
                return raw;

            Il2CppObjectBase native = raw as Il2CppObjectBase;

            if (native == null || native.Pointer == IntPtr.Zero)
                return null;

            ConstructorInfo constructor = type.GetConstructor(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic,
                null, new[] { typeof(IntPtr) }, null);

            return constructor?.Invoke(
                new object[] { native.Pointer });
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

            object value = GetMember(collection, "Count");

            return value == null ? 0 : Convert.ToInt32(value);
        }

        internal static object Item(object collection, int index)
        {
            if (collection == null || index < 0)
                return null;

            if (collection is Array array)
                return index < array.Length ? array.GetValue(index) : null;

            try
            {
                PropertyInfo property = collection.GetType().GetProperty(
                    "Item",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

                if (property != null)
                    return property.GetValue(
                        collection, new object[] { index });
            }
            catch { }

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
                ParameterInfo[] parameters = method.GetParameters();

                if (method.Name != "Add" || parameters.Length != 1)
                    continue;

                if (value != null &&
                    !parameters[0].ParameterType.IsInstanceOfType(value))
                    continue;

                method.Invoke(collection, new[] { value });
                return;
            }

            throw new MissingMethodException(
                collection.GetType().FullName, "Add");
        }

        internal static bool Contains(object collection, GameObject target)
        {
            if (collection == null || target == null)
                return false;

            for (int i = 0; i < Count(collection); i++)
            {
                GameObject item = Item(collection, i) as GameObject;

                if (item == target)
                    return true;
            }

            return false;
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

                    ParameterInfo[] parameters = method.GetParameters();

                    if (parameters.Length != arguments.Length)
                        continue;

                    bool matches = true;

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (arguments[i] == null)
                            continue;

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
                instance.GetType().FullName, name);
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
