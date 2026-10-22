
using System;
using System.Collections.Generic;

namespace ULTRADROID.Weapon.API
{
    public static class WeaponRegistry
    {
        private static readonly object Sync = new object();

        private static readonly Dictionary<string, WeaponDefinition> Weapons =
            new Dictionary<string, WeaponDefinition>(
                StringComparer.OrdinalIgnoreCase);

        public static event Action<WeaponDefinition> WeaponRegistered;

        public static void Register(WeaponDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            lock (Sync)
            {
                if (Weapons.ContainsKey(definition.Id))
                {
                    throw new InvalidOperationException(
                        "A weapon with ID '" + definition.Id +
                        "' is already registered.");
                }

                Weapons.Add(definition.Id, definition);
            }

            WeaponRegistered?.Invoke(definition);
        }

        public static bool TryGet(
            string id,
            out WeaponDefinition definition)
        {
            lock (Sync)
            {
                return Weapons.TryGetValue(
                    id ?? string.Empty, out definition);
            }
        }

        public static WeaponDefinition[] GetAll()
        {
            lock (Sync)
            {
                var result = new WeaponDefinition[Weapons.Count];
                Weapons.Values.CopyTo(result, 0);
                return result;
            }
        }

        public static bool Unregister(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            lock (Sync)
            {
                return Weapons.Remove(id);
            }
        }

        public static void Clear()
        {
            lock (Sync)
            {
                Weapons.Clear();
            }
        }
    }
}
