
using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace ULTRADROID.Weapon.API
{
    public sealed class WeaponGameRuntime
    {
        private static WeaponGameRuntime current;
        private static bool componentRegistered;

        private readonly Action<string> log;

        private readonly Dictionary<string, GameObject> nativeWeapons =
            new Dictionary<string, GameObject>(
                StringComparer.OrdinalIgnoreCase);

        private object gunControl;
        private IntPtr gunControlPointer = IntPtr.Zero;
        private int lastScanFrame = -120;
        private bool firstWeaponEquipped;

        public bool IsInitialized { get; private set; }

        public object GunControlInstance { get; private set; }

        private WeaponGameRuntime(Action<string> logger)
        {
            log = logger ?? (_ => { });
        }

        public static void Start(Action<string> logger)
        {
            if (current != null)
            {
                current.Log("Runtime already started.");
                return;
            }

            current = new WeaponGameRuntime(logger);

            ClassInjector.RegisterTypeInIl2Cpp<WeaponGameRuntimeBehaviour>();
            componentRegistered = true;

            GameObject host =
                new GameObject("ULTRADROID_Weapon_API_Runtime");

            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<WeaponGameRuntimeBehaviour>();

            current.Log("Runtime started; waiting for GunControl.");
        }

        internal static void Tick()
        {
            current?.UpdateRuntime();
        }

        public bool TryGetWeapon(
            string weaponId,
            out WeaponDefinition definition)
        {
            return WeaponRegistry.TryGet(weaponId, out definition);
        }

        private void UpdateRuntime()
        {
            if (Time.frameCount - lastScanFrame < 60)
                return;

            lastScanFrame = Time.frameCount;

            try
            {
                object found = FindReadyGunControl();

                if (found == null)
                {
                    IsInitialized = false;
                    GunControlInstance = null;
                    return;
                }

                IntPtr pointer = GetPointer(found);

                if (pointer == IntPtr.Zero)
                    return;

                if (pointer != gunControlPointer)
                {
                    gunControlPointer = pointer;
                    gunControl = found;
                    GunControlInstance = found;
                    IsInitialized = true;

                    nativeWeapons.Clear();
                    firstWeaponEquipped = false;

                    Log("Connected to initialized GunControl.");
                }
                else
                {
                    gunControl = found;
                    GunControlInstance = found;
                    IsInitialized = true;
                }

                RegisterDefinitions();
            }
            catch (Exception ex)
            {
                Log("Runtime update failed: " + Unwrap(ex));
            }
        }

        private void RegisterDefinitions()
        {
            object slots = GetMember(gunControl, "slots");

            if (slots == null || Count(slots) < 6)
                return;

            foreach (WeaponDefinition definition in WeaponRegistry.GetAll())
            {
                if (definition == null ||
                    nativeWeapons.ContainsKey(definition.Id))
                    continue;

                GameObject existing =
                    FindExistingWeapon(slots, definition.Id);

                if (existing != null)
                {
                    nativeWeapons[definition.Id] = existing;
                    continue;
                }

                if (!TryRegisterDefinition(
                    slots, definition, out GameObject created))
                    continue;

                nativeWeapons[definition.Id] = created;

                int slotNumber = Count(slots);

                Log("Registered '" + definition.DisplayName +
                    "' in native slot " + slotNumber + ".");

                if (!firstWeaponEquipped)
                {
                    firstWeaponEquipped = true;

                    try
                    {
                        Invoke(
                            gunControl,
                            "SwitchWeapon",
                            slotNumber,
                            false);

                        Log("Requested equip for custom weapon in slot " +
                            slotNumber + ".");
                    }
                    catch (Exception ex)
                    {
                        Log("Registered weapon but auto-equip failed: " +
                            Unwrap(ex));
                    }
                }
            }
        }

        private bool TryRegisterDefinition(
            object slots,
            WeaponDefinition definition,
            out GameObject createdWeapon)
        {
            createdWeapon = null;
            object newSlot = null;

            try
            {
                GameObject template = FindTemplateWeapon(slots);

                if (template == null)
                {
                    Log("No vanilla template found for '" +
                        definition.Id + "'.");
                    return false;
                }

                newSlot = CreateEmptySlot(slots);

                if (newSlot == null)
                {
                    Log("Could not create native slot for '" +
                        definition.Id + "'.");
                    return false;
                }

                createdWeapon = UnityEngine.Object.Instantiate(
                    template, template.transform.parent);

                if (createdWeapon == null)
                    return false;

                createdWeapon.name =
                    "ULTRADROID_WEAPON_" + SafeName(definition.Id);

                createdWeapon.SetActive(false);

                if (!AssignDescriptor(
                    createdWeapon, definition.DisplayName))
                {
                    UnityEngine.Object.Destroy(createdWeapon);
                    createdWeapon = null;

                    Log("Could not assign a unique WeaponDescriptor to '" +
                        definition.Id + "'.");
                    return false;
                }

                // Only add the new slot after the cloned weapon is ready.
                Add(slots, newSlot);
                object actualSlot = Item(slots, Count(slots) - 1);

                Add(actualSlot, createdWeapon);

                try
                {
                    Invoke(gunControl, "CalculateSlotCount");
                }
                catch (Exception ex)
                {
                    Log("CalculateSlotCount warning: " + Unwrap(ex));
                }

                return true;
            }
            catch (Exception ex)
            {
                if (createdWeapon != null)
                    UnityEngine.Object.Destroy(createdWeapon);

                createdWeapon = null;

                Log("Native registration failed for '" +
                    definition.Id + "': " + Unwrap(ex));
                return false;
            }
        }

        private static object CreateEmptySlot(object slots)
        {
            Type type = slots.GetType();

            if (!type.IsGenericType)
                return null;

            Type[] args = type.GetGenericArguments();

            if (args.Length != 1)
                return null;

            return Activator.CreateInstance(args[0]);
        }

        private static GameObject FindTemplateWeapon(object slots)
        {
            int limit = Math.Min(6, Count(slots));

            for (int i = 0; i < limit; i++)
            {
                object slot = Item(slots, i);

                for (int j = 0; j < Count(slot); j++)
                {
                    GameObject weapon = Item(slot, j) as GameObject;

                    if (weapon != null &&
                        !weapon.name.StartsWith(
                            "ULTRADROID_WEAPON_",
                            StringComparison.Ordinal))
                    {
                        return weapon;
                    }
                }
            }

            return null;
        }

        private static GameObject FindExistingWeapon(
            object slots, string id)
        {
            string expected = "ULTRADROID_WEAPON_" + SafeName(id);

            for (int i = 6; i < Count(slots); i++)
            {
                object slot = Item(slots, i);

                for (int j = 0; j < Count(slot); j++)
                {
                    GameObject weapon = Item(slot, j) as GameObject;

                    if (weapon != null && weapon.name == expected)
                        return weapon;
                }
            }

            return null;
        }

        private static bool AssignDescriptor(
            GameObject weapon, string displayName)
        {
            Type iconType = FindType("WeaponIcon");
            Type descriptorType = FindType("WeaponDescriptor");

            if (iconType == null || descriptorType == null)
                return false;

            object components = Invoke(
                weapon,
                "GetComponentsInChildren",
                Il2CppType.From(iconType),
                true);

            if (Count(components) == 0)
                return false;

            object firstIcon =
                WrapInstance(iconType, Item(components, 0));

            if (firstIcon == null)
                return false;

            object original =
                GetMember(firstIcon, "weaponDescriptor");

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

            if (!SetMember(
                descriptor, "weaponName", displayName))
                return false;

            int assigned = 0;

            for (int i = 0; i < Count(components); i++)
            {
                object icon =
                    WrapInstance(iconType, Item(components, i));

                if (icon != null &&
                    SetMember(icon, "weaponDescriptor", descriptor))
                {
                    assigned++;
                }
            }

            return assigned > 0;
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
                catch { }
            }

            object il2CppType = null;

            try
            {
                il2CppType = Il2CppType.From(descriptorType);
            }
            catch { }

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
                catch { }
            }

            return null;
        }

        private static object FindReadyGunControl()
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

        private static Type FindType(string name)
        {
            foreach (Assembly assembly in
                AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type direct = assembly.GetType(name, false);

                    if (direct != null)
                        return direct;

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

        private static IntPtr GetPointer(object value)
        {
            Il2CppObjectBase native = value as Il2CppObjectBase;

            return native == null
                ? IntPtr.Zero
                : native.Pointer;
        }

        private static object GetMember(object instance, string name)
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

        private static bool SetMember(
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
                    catch { }
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
                        catch { }
                    }
                }
            }

            return false;
        }

        private static int Count(object collection)
        {
            if (collection == null)
                return 0;

            if (collection is Array array)
                return array.Length;

            object count = GetMember(collection, "Count");

            return count == null ? 0 : Convert.ToInt32(count);
        }

        private static object Item(object collection, int index)
        {
            if (collection == null || index < 0)
                return null;

            if (collection is Array array)
                return index < array.Length
                    ? array.GetValue(index)
                    : null;

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
            catch { }

            try
            {
                foreach (MethodInfo method in collection.GetType().GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic))
                {
                    if (method.Name == "get_Item" &&
                        method.GetParameters().Length == 1 &&
                        method.GetParameters()[0].ParameterType == typeof(int))
                    {
                        return method.Invoke(
                            collection, new object[] { index });
                    }
                }
            }
            catch { }

            return null;
        }

        private static void Add(object collection, object value)
        {
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

                if (value != null &&
                    !parameters[0].ParameterType.IsInstanceOfType(value) &&
                    parameters[0].ParameterType != value.GetType())
                    continue;

                method.Invoke(collection, new[] { value });
                return;
            }

            throw new MissingMethodException(
                collection.GetType().FullName, "Add");
        }

        private static object Invoke(
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
                        {
                            if (parameters[i].ParameterType.IsValueType &&
                                Nullable.GetUnderlyingType(
                                    parameters[i].ParameterType) == null)
                            {
                                matches = false;
                            }
                        }
                        else if (!parameters[i].ParameterType.IsAssignableFrom(
                                     arguments[i].GetType()) &&
                                 parameters[i].ParameterType !=
                                     arguments[i].GetType())
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

        private static string SafeName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "CUSTOM";

            char[] chars = value.ToUpperInvariant().ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_')
                    chars[i] = '_';
            }

            return new string(chars);
        }

        private void Log(string message)
        {
            try
            {
                log("[ULTRADROID Weapon API] " + message);
            }
            catch { }
        }

        private static Exception Unwrap(Exception ex)
        {
            while (ex is TargetInvocationException &&
                   ex.InnerException != null)
            {
                ex = ex.InnerException;
            }

            return ex;
        }
    }

    public sealed class WeaponGameRuntimeBehaviour : MonoBehaviour
    {
        public WeaponGameRuntimeBehaviour(IntPtr ptr) : base(ptr) { }

        public void Update()
        {
            WeaponGameRuntime.Tick();
        }
    }
}
