
using System;
using System.Reflection;

namespace ULTRADROID.Weapon.API
{
    /// <summary>
    /// Connects the weapon API to ULTRAKILL's native weapon system.
    /// Logging is supplied by the host plugin to avoid a direct
    /// BepInEx dependency in the public API.
    /// </summary>
    public sealed class WeaponGameRuntime
    {
        private readonly Action<string> log;

        public bool IsInitialized { get; private set; }

        public object GunControlInstance { get; private set; }

        public WeaponGameRuntime(Action<string> logger)
        {
            log = logger ?? (_ => { });
        }

        /// <summary>
        /// Attempts to locate the initialized GunControl singleton.
        /// Call again if the game has not initialized it yet.
        /// </summary>
        public bool TryInitialize()
        {
            try
            {
                Type gunControlType = FindType("GunControl");

                if (gunControlType == null)
                {
                    Log("GunControl type not found.");
                    return false;
                }

                object instance = FindSingletonInstance(gunControlType);

                if (instance == null)
                {
                    Log("GunControl is not initialized yet.");
                    return false;
                }

                GunControlInstance = instance;
                IsInitialized = true;

                Log("Connected to GunControl.");
                return true;
            }
            catch (Exception ex)
            {
                IsInitialized = false;
                GunControlInstance = null;

                Log("Initialization failed: " + ex);
                return false;
            }
        }

        public bool TryGetWeapon(
            string weaponId,
            out WeaponDefinition definition)
        {
            return WeaponRegistry.TryGet(weaponId, out definition);
        }

        private void Log(string message)
        {
            try
            {
                log("[ULTRADROID Weapon API] " + message);
            }
            catch
            {
                // A logging failure must not crash the game.
            }
        }

        private static Type FindType(string typeName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type type = assembly.GetType(typeName, false, false);

                    if (type != null)
                        return type;

                    foreach (Type candidate in assembly.GetTypes())
                    {
                        if (candidate.Name == typeName)
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
                            candidate.Name == typeName)
                        {
                            return candidate;
                        }
                    }
                }
                catch
                {
                    // Continue searching other assemblies.
                }
            }

            return null;
        }

        private static object FindSingletonInstance(Type targetType)
        {
            for (Type current = targetType;
                 current != null;
                 current = current.BaseType)
            {
                if (!current.IsGenericType ||
                    current.GetGenericTypeDefinition().Name != "MonoSingleton`1")
                {
                    continue;
                }

                Type[] arguments = current.GetGenericArguments();

                if (arguments.Length != 1 ||
                    arguments[0] != targetType)
                {
                    continue;
                }

                for (Type owner = current;
                     owner != null;
                     owner = owner.BaseType)
                {
                    PropertyInfo property = owner.GetProperty(
                        "Instance",
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.Static |
                        BindingFlags.DeclaredOnly);

                    if (property == null)
                        continue;

                    MethodInfo getter = property.GetGetMethod(true);

                    if (getter == null || !getter.IsStatic)
                        continue;

                    return getter.Invoke(null, null);
                }
            }

            return null;
        }
    }
}
