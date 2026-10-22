

using System;

namespace ULTRADROID.Weapon.API
{
    public enum WeaponAttackSlot
    {
        Primary,
        Alternate,
        Special
    }

    public enum WeaponAttackKind
    {
        Custom,
        Hitscan,
        Projectile,
        Melee,
        Area
    }

    public sealed class WeaponAttackDefinition
    {
        public string Id { get; }

        public WeaponAttackKind Kind { get; set; }

        public float Damage { get; set; }
        public float Range { get; set; }
        public float CooldownSeconds { get; set; }
        public float ProjectileSpeed { get; set; }

        public Action<WeaponAttackContext> Execute { get; set; }

        public WeaponAttackDefinition(
            string id,
            WeaponAttackKind kind = WeaponAttackKind.Custom)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException(
                    "Attack ID cannot be empty.", nameof(id));

            if (!Enum.IsDefined(typeof(WeaponAttackKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));

            Id = id.Trim();
            Kind = kind;
        }
    }

    public sealed class WeaponAttackContext
    {
        public string WeaponId { get; }
        public string VariantId { get; }
        public WeaponAttackSlot Slot { get; }

        // The runtime adapter will supply actual game-specific objects.
        public object Owner { get; }
        public object NativeContext { get; }

        public WeaponAttackContext(
            string weaponId,
            string variantId,
            WeaponAttackSlot slot,
            object owner = null,
            object nativeContext = null)
        {
            WeaponId = weaponId ??
                throw new ArgumentNullException(nameof(weaponId));

            VariantId = variantId;
            Slot = slot;
            Owner = owner;
            NativeContext = nativeContext;
        }
    }
}
