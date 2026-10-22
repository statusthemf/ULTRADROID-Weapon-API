
using System;
using System.Collections.Generic;

namespace ULTRADROID.Weapon.API
{
    public sealed class WeaponDefinition
    {
        private readonly List<WeaponVariantDefinition> variants =
            new List<WeaponVariantDefinition>();

        private readonly List<WeaponAttackDefinition> attacks =
            new List<WeaponAttackDefinition>();

        private WeaponAttackDefinition primaryAttack;
        private WeaponAttackDefinition alternateAttack;

        public string Id { get; }
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public int PreferredSlot { get; set; }

        public IReadOnlyList<WeaponVariantDefinition> Variants =>
            variants.AsReadOnly();

        // Legacy attack properties remain supported.
        public WeaponAttackDefinition PrimaryAttack
        {
            get => primaryAttack;
            set
            {
                ValidateLegacyAttack(value, alternateAttack);
                primaryAttack = value;
            }
        }

        public WeaponAttackDefinition AlternateAttack
        {
            get => alternateAttack;
            set
            {
                ValidateLegacyAttack(value, primaryAttack);
                alternateAttack = value;
            }
        }

        // Includes legacy primary/alternate attacks and added attacks.
        public IReadOnlyList<WeaponAttackDefinition> Attacks
        {
            get
            {
                var result = new List<WeaponAttackDefinition>();

                if (primaryAttack != null)
                    result.Add(primaryAttack);

                if (alternateAttack != null)
                    result.Add(alternateAttack);

                result.AddRange(attacks);
                return result.AsReadOnly();
            }
        }

        public WeaponDefinition(string id, string displayName)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException(
                    "Weapon ID cannot be empty.", nameof(id));

            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException(
                    "Display name cannot be empty.", nameof(displayName));

            Id = id.Trim();
            DisplayName = displayName.Trim();
            Category = "Custom";
            PreferredSlot = -1;
        }

        public WeaponDefinition AddAttack(WeaponAttackDefinition attack)
        {
            if (attack == null)
                throw new ArgumentNullException(nameof(attack));

            EnsureUniqueAttackId(attack.Id, null);
            attacks.Add(attack);
            return this;
        }

        public WeaponAttackDefinition GetAttack(string attackId)
        {
            if (string.IsNullOrWhiteSpace(attackId))
                return null;

            if (primaryAttack != null &&
                string.Equals(
                    primaryAttack.Id, attackId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return primaryAttack;
            }

            if (alternateAttack != null &&
                string.Equals(
                    alternateAttack.Id, attackId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return alternateAttack;
            }

            foreach (WeaponAttackDefinition attack in attacks)
            {
                if (string.Equals(
                    attack.Id, attackId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return attack;
                }
            }

            return null;
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
                {
                    return variant;
                }
            }

            return null;
        }

        private void ValidateLegacyAttack(
            WeaponAttackDefinition candidate,
            WeaponAttackDefinition otherLegacyAttack)
        {
            if (candidate == null)
                return;

            if (otherLegacyAttack != null &&
                !ReferenceEquals(candidate, otherLegacyAttack) &&
                string.Equals(
                    candidate.Id,
                    otherLegacyAttack.Id,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Duplicate attack ID '" + candidate.Id +
                    "' on weapon '" + Id + "'.");
            }

            EnsureUniqueAttackId(candidate.Id, candidate);
        }

        private void EnsureUniqueAttackId(
            string attackId,
            WeaponAttackDefinition allowedReference)
        {
            if (primaryAttack != null &&
                !ReferenceEquals(primaryAttack, allowedReference) &&
                string.Equals(
                    primaryAttack.Id, attackId,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Duplicate attack ID '" + attackId +
                    "' on weapon '" + Id + "'.");
            }

            if (alternateAttack != null &&
                !ReferenceEquals(alternateAttack, allowedReference) &&
                string.Equals(
                    alternateAttack.Id, attackId,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Duplicate attack ID '" + attackId +
                    "' on weapon '" + Id + "'.");
            }

            foreach (WeaponAttackDefinition existing in attacks)
            {
                if (!ReferenceEquals(existing, allowedReference) &&
                    string.Equals(
                        existing.Id, attackId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Duplicate attack ID '" + attackId +
                        "' on weapon '" + Id + "'.");
                }
            }
        }
    }

    public sealed class WeaponVariantDefinition
    {
        private readonly List<WeaponAttackDefinition> attacks =
            new List<WeaponAttackDefinition>();

        private WeaponAttackDefinition primaryAttack;
        private WeaponAttackDefinition alternateAttack;

        public string Id { get; }
        public string DisplayName { get; set; }

        // Placeholders for model/visual asset references.
        // The game integration will define how they are loaded and attached.
        public object ModelAsset { get; set; }
        public object VisualData { get; set; }

        public WeaponAttackDefinition PrimaryAttack
        {
            get => primaryAttack;
            set
            {
                ValidateLegacyAttack(value, alternateAttack);
                primaryAttack = value;
            }
        }

        public WeaponAttackDefinition AlternateAttack
        {
            get => alternateAttack;
            set
            {
                ValidateLegacyAttack(value, primaryAttack);
                alternateAttack = value;
            }
        }

        // Includes legacy primary/alternate attacks and added attacks.
        public IReadOnlyList<WeaponAttackDefinition> Attacks
        {
            get
            {
                var result = new List<WeaponAttackDefinition>();

                if (primaryAttack != null)
                    result.Add(primaryAttack);

                if (alternateAttack != null)
                    result.Add(alternateAttack);

                result.AddRange(attacks);
                return result.AsReadOnly();
            }
        }

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

        public WeaponVariantDefinition AddAttack(
            WeaponAttackDefinition attack)
        {
            if (attack == null)
                throw new ArgumentNullException(nameof(attack));

            EnsureUniqueAttackId(attack.Id, null);
            attacks.Add(attack);
            return this;
        }

        public WeaponAttackDefinition GetAttack(string attackId)
        {
            if (string.IsNullOrWhiteSpace(attackId))
                return null;

            if (primaryAttack != null &&
                string.Equals(
                    primaryAttack.Id, attackId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return primaryAttack;
            }

            if (alternateAttack != null &&
                string.Equals(
                    alternateAttack.Id, attackId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return alternateAttack;
            }

            foreach (WeaponAttackDefinition attack in attacks)
            {
                if (string.Equals(
                    attack.Id, attackId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return attack;
                }
            }

            return null;
        }

        private void ValidateLegacyAttack(
            WeaponAttackDefinition candidate,
            WeaponAttackDefinition otherLegacyAttack)
        {
            if (candidate == null)
                return;

            if (otherLegacyAttack != null &&
                !ReferenceEquals(candidate, otherLegacyAttack) &&
                string.Equals(
                    candidate.Id,
                    otherLegacyAttack.Id,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Duplicate attack ID '" + candidate.Id +
                    "' on variant '" + Id + "'.");
            }

            EnsureUniqueAttackId(candidate.Id, candidate);
        }

        private void EnsureUniqueAttackId(
            string attackId,
            WeaponAttackDefinition allowedReference)
        {
            if (primaryAttack != null &&
                !ReferenceEquals(primaryAttack, allowedReference) &&
                string.Equals(
                    primaryAttack.Id, attackId,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Duplicate attack ID '" + attackId +
                    "' on variant '" + Id + "'.");
            }

            if (alternateAttack != null &&
                !ReferenceEquals(alternateAttack, allowedReference) &&
                string.Equals(
                    alternateAttack.Id, attackId,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Duplicate attack ID '" + attackId +
                    "' on variant '" + Id + "'.");
            }

            foreach (WeaponAttackDefinition existing in attacks)
            {
                if (!ReferenceEquals(existing, allowedReference) &&
                    string.Equals(
                        existing.Id, attackId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Duplicate attack ID '" + attackId +
                        "' on variant '" + Id + "'.");
                }
            }
        }
    }
}
