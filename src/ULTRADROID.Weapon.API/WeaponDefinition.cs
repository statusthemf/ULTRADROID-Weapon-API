
using System;
using System.Collections.Generic;

namespace ULTRADROID.Weapon.API
{
    public sealed class WeaponDefinition
    {
        private readonly List<WeaponVariantDefinition> variants =
            new List<WeaponVariantDefinition>();

        public string Id { get; }
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public int PreferredSlot { get; set; }

        public IReadOnlyList<WeaponVariantDefinition> Variants =>
            variants.AsReadOnly();

        public WeaponAttackDefinition PrimaryAttack { get; set; }
        public WeaponAttackDefinition AlternateAttack { get; set; }

        public WeaponDefinition(string id, string displayName)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Weapon ID cannot be empty.", nameof(id));

            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException(
                    "Display name cannot be empty.", nameof(displayName));

            Id = id.Trim();
            DisplayName = displayName.Trim();
            Category = "Custom";
            PreferredSlot = -1;
        }

        public WeaponDefinition AddVariant(WeaponVariantDefinition variant)
        {
            if (variant == null)
                throw new ArgumentNullException(nameof(variant));

            foreach (WeaponVariantDefinition existing in variants)
            {
                if (string.Equals(
                    existing.Id, variant.Id,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Duplicate variant ID '" + variant.Id +
                        "' on weapon '" + Id + "'.");
                }
            }

            variants.Add(variant);
            return this;
        }

        public WeaponVariantDefinition GetVariant(string variantId)
        {
            foreach (WeaponVariantDefinition variant in variants)
            {
                if (string.Equals(
                    variant.Id, variantId,
                    StringComparison.OrdinalIgnoreCase))
                    return variant;
            }

            return null;
        }
    }

    public sealed class WeaponVariantDefinition
    {
        public string Id { get; }
        public string DisplayName { get; set; }

        // These are placeholders for model/visual asset references.
        // The game integration will define how they are loaded and attached.
        public object ModelAsset { get; set; }
        public object VisualData { get; set; }

        public WeaponAttackDefinition PrimaryAttack { get; set; }
        public WeaponAttackDefinition AlternateAttack { get; set; }

        public WeaponVariantDefinition(string id, string displayName)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException(
                    "Variant ID cannot be empty.", nameof(id));

            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException(
                    "Display name cannot be empty.", nameof(displayName));

            Id = id.Trim();
            DisplayName = displayName.Trim();
        }
    }
}
