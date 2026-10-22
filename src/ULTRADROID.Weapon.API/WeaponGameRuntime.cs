
using System;
using System.Reflection;
using BepInEx.Logging;

namespace ULTRADROID.Weapon.API
{
    /// <summary>
    /// Entry point for integrating registered weapon definitions
    /// with ULTRAKILL's native weapon system.
    ///
    /// Game-specific operations are isolated here so that the public
    /// weapon definitions and registry remain independent of the game.
    /// </summary>
    public sealed class WeaponGameRuntime
    {
        private readonly ManualLogSource log;

        public bool IsInitialized { get; private set; }

        public object GunControlInstance { get; private set; }

        public WeaponGameRuntime(ManualLogSource logger)
        {
            log = logger ??
                throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Attempts to locate the initialized GunControl singleton.
        /// Call this after the game has initialized its weapon system.
        /// </summary>
        public bool TryInitialize()
        {
            try
            {
                Type gunControlType = FindType("GunControl");

                if (gunControlType == null)
                {
                    log.LogWarning(
                        "[Weapon API Runtime] GunControl type not found.");

                    return false;
                }

                object instance = FindSingletonInstance(gunControlType);

                if (instance == null)
                {
                    log.LogInfo(
                        "[Weapon API Runtime] GunControl is not initialized yet.");

                    return false;
                }

                GunControlInstance = instance;
                IsInitialized = true;

                log.LogInfo(
                    "[Weapon API Runtime] Connected to GunControl.");

                return true;
            }
            catch (Exception ex)
            {
                log.LogError(
                    "[Weapon API Runtime] Initialization failed: " + ex);

                IsInitialized = false;
                GunControlInstance = null;

                return false;
            }
        }

        /// <summary>
        /// Returns a registered weapon definition by ID.
        /// This does not yet create a native in-game weapon.
        /// </summary>
        public bool TryGetWeapon(
            string weaponId,
            out WeaponDefinition definition)
        {
            return WeaponRegistry.TryGet(weaponId, out definition);
        }

        private static Type FindType(string typeName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type type = assembly.GetType(
                        typeName,
                        throwOnError: false,
                        ignoreCase: false);

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
                    current.GetGenericTypeDefinition().Name !=
                        "MonoSingleton`1")
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
