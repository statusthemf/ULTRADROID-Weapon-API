using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.Injection;
using ULTRADROID.Models;
using UnityEngine;

namespace ULTRADROID.Weapon.API
{
    public sealed class WeaponGameRuntime
    {
        private static WeaponGameRuntime current;

        // Type lookup is expensive because it may enumerate every loaded assembly.
        // Cache successful lookups; do not cache misses because game assemblies may
        // not be ready the first time the runtime starts.
        private static readonly Dictionary<string, Type> typeCache =
            new Dictionary<string, Type>(StringComparer.Ordinal);

        private readonly Action<string> log;

        private readonly Dictionary<string, GameObject> nativeWeapons =
            new Dictionary<string, GameObject>(
                StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, int> weaponSlots =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<int, string> slotOwners =
            new Dictionary<int, string>();

        // Maps each weapon ID to its variant ID -> native family index.
        private readonly Dictionary<string, Dictionary<string, int>>
            variantIndices =
                new Dictionary<string, Dictionary<string, int>>(
                    StringComparer.OrdinalIgnoreCase);

        private object gunControl;
        private IntPtr gunControlPointer = IntPtr.Zero;
        private bool definitionsRegisteredForGunControl;
        private int lastScanFrame = -120;
        private int lastWheelTintFrame = -3;
        private string lastDiscoveryMessage;

        private readonly Dictionary<int, WheelImageTintRecord> wheelImageTints =
            new Dictionary<int, WheelImageTintRecord>();

        private sealed class WheelImageTintRecord
        {
            public object Image;
            public Color OriginalColor;
            public Color AppliedColor;
        }

        public bool IsInitialized { get; private set; }

        public object GunControlInstance { get; private set; }

        private WeaponGameRuntime(Action<string> logger)
        {
            log = logger ?? (_ => { });
        }

        public static void Start(
            Action<string> logger,
            Action attachBehaviour)
        {
            if (current != null)
            {
                current.Log("Runtime already started.");
                return;
            }

            current = new WeaponGameRuntime(logger);

            try
            {
                if (attachBehaviour == null)
                    throw new ArgumentNullException(nameof(attachBehaviour));

                ClassInjector.RegisterTypeInIl2Cpp<WeaponGameRuntimeBehaviour>();

                attachBehaviour();

                current.Log("Runtime behaviour attached through BepInEx.");
                current.Log("Runtime started; waiting for GunControl.");
            }
            catch (Exception ex)
            {
                current.Log("Runtime startup failed: " + ex);
                current = null;
            }
        }

        internal static void Tick()
        {
            current?.UpdateRuntime();
        }

        internal static void DebugLog(string message)
        {
            current?.Log(message);
        }

        public bool TryGetWeapon(
            string weaponId,
            out WeaponDefinition definition)
        {
            return WeaponRegistry.TryGet(weaponId, out definition);
        }

        public static bool SelectWeapon(string weaponId)
        {
            if (current == null ||
                !current.IsInitialized ||
                string.IsNullOrWhiteSpace(weaponId))
                return false;

            if (!current.weaponSlots.TryGetValue(
                weaponId, out int slotNumber))
            {
                current.Log(
                    "Weapon has no registered slot: " + weaponId);
                return false;
            }

            try
            {
                Invoke(
                    current.gunControl,
                    "SwitchWeapon",
                    slotNumber,
                    false);

                current.Log(
                    "Selected '" + weaponId +
                    "' using slot " + slotNumber + ".");

                return true;
            }
            catch (Exception ex)
            {
                current.Log(
                    "Weapon selection failed: " + Unwrap(ex));
                return false;
            }
        }

        /// <summary>
        /// Selects a registered custom variant by ID.
        /// The index is resolved from the actual native family list.
        /// </summary>
        public static bool SelectVariant(
            string weaponId,
            string variantId)
        {
            if (current == null ||
                !current.IsInitialized ||
                string.IsNullOrWhiteSpace(weaponId) ||
                string.IsNullOrWhiteSpace(variantId))
                return false;

            if (!current.weaponSlots.TryGetValue(
                weaponId, out int slotNumber))
            {
                current.Log(
                    "Variant selection failed: unknown weapon '" +
                    weaponId + "'.");
                return false;
            }

            if (!current.variantIndices.TryGetValue(
                weaponId, out Dictionary<string, int> variants) ||
                !variants.TryGetValue(
                    variantId, out int variantIndex))
            {
                current.Log(
                    "Variant is not registered: " +
                    weaponId + "/" + variantId);
                return false;
            }

            try
            {
                Invoke(
                    current.gunControl,
                    "SwitchWeapon",
                    slotNumber,
                    false);

                Invoke(
                    current.gunControl,
                    "SwitchVariant",
                    variantIndex);

                current.Log(
                    "Selected variant '" + variantId +
                    "' of '" + weaponId +
                    "' at family index " + variantIndex + ".");

                return true;
            }
            catch (Exception ex)
            {
                current.Log(
                    "Variant selection failed: " + Unwrap(ex));
                return false;
            }
        }

        private void ClearRegistrations()
        {
            nativeWeapons.Clear();
            weaponSlots.Clear();
            slotOwners.Clear();
            variantIndices.Clear();
        }

        private void LogDiscoveryOnce(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            if (string.Equals(
                lastDiscoveryMessage,
                message,
                StringComparison.Ordinal))
                return;

            lastDiscoveryMessage = message;
            Log(message);
        }

        private void UpdateRuntime()
        {
            // Wheel visuals need to update more frequently than weapon discovery.
            if (Time.frameCount - lastWheelTintFrame >= 3)
            {
                lastWheelTintFrame = Time.frameCount;

                try
                {
                    UpdateWheelTints();
                }
                catch (Exception ex)
                {
                    Log("Wheel tint update failed: " + Unwrap(ex));
                }

                try
                {
                    UpdateHudIconTints();
                }
                catch (Exception ex)
                {
                    Log("HUD icon tint update failed: " + Unwrap(ex));
                }

                try
                {
                    UpdateEquippedHudTint();
                }
                catch (Exception ex)
                {
                    Log("Equipped HUD tint update failed: " + Unwrap(ex));
                }
            }

            // Keep the existing, slower GunControl discovery interval.
            if (Time.frameCount - lastScanFrame < 10)
                return;

            lastScanFrame = Time.frameCount;

            try
            {
                object found = FindReadyGunControl(LogDiscoveryOnce);

                if (found == null)
                {
                    if (IsInitialized)
                    {
                        Log(
                            "GunControl disappeared; waiting for it again.");
                    }

                    IsInitialized = false;
                    GunControlInstance = null;
                    gunControl = null;
                    gunControlPointer = IntPtr.Zero;
                    definitionsRegisteredForGunControl = false;

                    ClearRegistrations();
                    return;
                }

                // A successful discovery ends any previous repeated-warning state.
                lastDiscoveryMessage = null;

                IntPtr pointer = GetPointer(found);

                if (pointer == IntPtr.Zero)
                    return;
                
                if (pointer != gunControlPointer)
                {
                    gunControlPointer = pointer;
                    gunControl = found;
                    GunControlInstance = found;
                    IsInitialized = true;

                    ClearRegistrations();

                    Log("Connected to initialized GunControl.");

                    RegisterDefinitions();
                }
                else
                {
                    gunControl = found;
                    GunControlInstance = found;
                    IsInitialized = true;
                }
                definitionsRegisteredForGunControl = true;
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
                    string.IsNullOrWhiteSpace(definition.Id))
                    continue;

                RegisterDefinition(slots, definition);
            }
        }

        private void RegisterDefinition(
            object slots,
            WeaponDefinition definition)
        {
            if (definition.ExtendExistingSlot >= 1 &&
                definition.ExtendExistingSlot <= 6)
            {
                RegisterExistingFamilyExtension(
                    slots, definition, definition.ExtendExistingSlot);
                return;
            }

            GameObject baseWeapon = FindExistingWeapon(
                slots, definition.Id, out int existingSlot);

            int targetSlot;

            if (baseWeapon != null)
            {
                targetSlot = existingSlot;
            }
            else
            {
                targetSlot = ResolveTargetSlot(definition, slots);

                if (targetSlot < 7)
                {
                    Log(
                        "Invalid custom slot for '" +
                        definition.Id + "'.");
                    return;
                }

                if (slotOwners.TryGetValue(
                    targetSlot, out string owner) &&
                    !string.Equals(
                        owner,
                        definition.Id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    Log(
                        "Slot " + targetSlot +
                        " is already assigned to '" + owner +
                        "'. Cannot register '" + definition.Id + "'.");
                    return;
                }

                if (!TryCreateBaseWeapon(
                    slots,
                    definition,
                    targetSlot,
                    out baseWeapon))
                    return;

                Log(
                    "Registered '" + definition.DisplayName +
                    "' in native slot " + targetSlot + ".");
            }

            if (baseWeapon == null)
                return;

            weaponSlots[definition.Id] = targetSlot;
            nativeWeapons[definition.Id] = baseWeapon;
            slotOwners[targetSlot] = definition.Id;

            RegisterVariants(
                slots,
                definition,
                targetSlot,
                baseWeapon);
        }

        private void RegisterExistingFamilyExtension(
            object slots, WeaponDefinition definition, int slotNumber)
        {
            try
            {
                object family = Item(slots, slotNumber - 1);
                if (family == null || Count(family) == 0)
                {
                    Log("Cannot extend native slot " + slotNumber + ": family is empty.");
                    return;
                }

                var indices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                GameObject template = Item(family, 0) as GameObject;
                if (template == null)
                {
                    Log("Cannot extend native slot " + slotNumber + ": no GameObject template.");
                    return;
                }

                foreach (WeaponVariantDefinition variant in definition.Variants)
                {
                    if (variant == null || string.IsNullOrWhiteSpace(variant.Id))
                        continue;

                    string entryName = VariantWeaponName(definition.Id, variant.Id);
                    GameObject entry = FindInFamily(family, entryName, out int familyIndex);
                    if (entry == null)
                    {
                        GameObject clone = UnityEngine.Object.Instantiate(template, template.transform.parent);
                        if (clone == null) continue;
                        clone.name = entryName;
                        clone.SetActive(false);
                        if (!AssignDescriptor(clone, variant.DisplayName, variant.Color))
                        {
                            UnityEngine.Object.Destroy(clone);
                            Log("Could not assign descriptor for native-family variant '" + variant.Id + "'.");
                            continue;
                        }
                        Add(family, clone);
                        entry = clone;
                        familyIndex = FindFamilyIndex(family, entry);
                        Log("Appended variant '" + definition.Id + "/" + variant.Id + "' to existing native slot " + slotNumber + ".");
                    }
                    else
                    {
                        AssignDescriptor(entry, variant.DisplayName, variant.Color);
                        familyIndex = FindFamilyIndex(family, entry);
                    }

                    ApplyModelVisual(
                        entry,
                        variant.Model ?? definition.Model,
                        definition.Id + "/" + variant.Id);

                    Color? modelTint = variant.ModelTint ?? variant.CustomTint;
                    if (modelTint.HasValue) ApplyCustomTint(entry, modelTint.Value);
                    if (variant.IconTint.HasValue) ApplyIconTint(entry, variant.IconTint.Value);
                    if (familyIndex >= 0) indices[variant.Id] = familyIndex;
                    nativeWeapons[definition.Id] = entry;
                }

                weaponSlots[definition.Id] = slotNumber;
                variantIndices[definition.Id] = indices;
                try { Invoke(gunControl, "CalculateSlotCount"); }
                catch (Exception ex) { Log("CalculateSlotCount warning after native extension: " + Unwrap(ex)); }
                // Registration may be retried; avoid logging on every retry.
            }
            catch (Exception ex)
            {
                Log("Native family extension failed for '" + definition.Id + "': " + Unwrap(ex));
            }
        }

        private int ResolveTargetSlot(
            WeaponDefinition definition,
            object slots)
        {
            int requested = definition.PreferredSlot;

            if (requested >= 7)
                return requested;

            if (requested != -1)
            {
                Log(
                    "PreferredSlot must be -1 or at least 7. Weapon: " +
                    definition.Id);
                return -1;
            }

            // Native slot numbers are one-based. Preserve vanilla slots 1–6.
            int candidate = Math.Max(7, Count(slots) + 1);

            while (slotOwners.ContainsKey(candidate))
                candidate++;

            return candidate;
        }

        private void ApplyModelVisual(
            GameObject weapon,
            WeaponModelDefinition model,
            string ownerId)
        {
            if (weapon == null || model == null ||
                string.IsNullOrWhiteSpace(model.Path))
                return;

            GameObject loadedModel = null;

            try
            {
                string modelPath = model.Path;

                if (!System.IO.Path.IsPathRooted(modelPath))
                    modelPath = System.IO.Path.Combine(
                        Paths.PluginPath, modelPath);

                if (!File.Exists(modelPath))
                {
                    Log("Custom model not found: " + modelPath);
                    return;
                }

                ModelLoadOptions options = new ModelLoadOptions
                {
                    Name = "ULTRADROID_Model_" + SafeName(ownerId),
                    Position = model.Position,
                    RotationEuler = model.RotationEuler,
                    Scale = model.Scale,
                    LoadTextures = model.LoadTextures,
                    CreateColliders = model.CreateColliders
                };

                loadedModel = ModelAPI.LoadGLB(
                    modelPath, weapon.transform, options);

                if (loadedModel == null)
                {
                    Log("ModelAPI.LoadGLB returned null: " + ownerId);
                    return;
                }

                loadedModel.SetActive(true);

                if (model.HideTemplateMeshes)
                {
                    Renderer[] renderers =
                        weapon.GetComponentsInChildren<Renderer>(true);

                    foreach (Renderer renderer in renderers)
                    {
                        if (renderer == null)
                            continue;

                        Transform rt = renderer.transform;
                        Transform mt = loadedModel.transform;

                        if (rt == mt || rt.IsChildOf(mt))
                            continue;

                        if (renderer is MeshRenderer ||
                            renderer is SkinnedMeshRenderer)
                            renderer.enabled = false;
                    }
                }

                Log("Applied custom model: " + ownerId);
            }
            catch (Exception ex)
            {
                Log("Custom model failed: " + Unwrap(ex));

                if (loadedModel != null)
                    UnityEngine.Object.Destroy(loadedModel);
            }
        }

        private bool TryCreateBaseWeapon(
            object slots,
            WeaponDefinition definition,
            int targetSlot,
            out GameObject createdWeapon)
        {
            createdWeapon = null;
            GameObject prepared = null;

            try
            {
                if (slots == null ||
                    definition == null ||
                    targetSlot < 7)
                    return false;

                GameObject template = FindTemplateWeapon(slots);

                if (template == null)
                {
                    Log(
                        "No vanilla template found for '" +
                        definition.Id + "'.");
                    return false;
                }

                prepared = UnityEngine.Object.Instantiate(
                    template,
                    template.transform.parent);

                if (prepared == null)
                    return false;

                prepared.name = BaseWeaponName(definition.Id);
                prepared.SetActive(false);

                string displayName = definition.DisplayName;

                if (definition.Variants.Count > 0)
                {
                    WeaponVariantDefinition first =
                        definition.Variants[0];

                    if (first != null &&
                        !string.IsNullOrWhiteSpace(first.DisplayName))
                    {
                        displayName = first.DisplayName;
                    }
                }

                WeaponVariantColor baseColor =
                    definition.Variants.Count > 0 &&
                    definition.Variants[0] != null
                        ? definition.Variants[0].Color
                        : WeaponVariantColor.Default;

                ApplyModelVisual(
                    prepared,
                    definition.Model,
                    definition.Id);

                if (!AssignDescriptor(prepared, displayName, baseColor))
                {
                    Log(
                        "Could not assign WeaponDescriptor to '" +
                        definition.Id + "'.");
                    return false;
                }

                int targetIndex = targetSlot - 1;

                while (Count(slots) <= targetIndex)
                {
                    object newSlot = CreateEmptySlot(slots);

                    if (newSlot == null)
                    {
                        Log(
                            "Could not create native slot " +
                            (Count(slots) + 1) + ".");
                        return false;
                    }

                    Add(slots, newSlot);
                }

                object target = Item(slots, targetIndex);

                if (target == null)
                    return false;

                // Never overwrite another weapon family.
                if (Count(target) > 0)
                {
                    Log(
                        "Native slot " + targetSlot +
                        " is not empty. Refusing to overwrite it.");
                    return false;
                }

                Add(target, prepared);

                createdWeapon = prepared;
                prepared = null;

                try
                {
                    Invoke(gunControl, "CalculateSlotCount");
                }
                catch (Exception ex)
                {
                    Log(
                        "CalculateSlotCount warning: " +
                        Unwrap(ex));
                }

                return true;
            }
            catch (Exception ex)
            {
                Log(
                    "Native registration failed for '" +
                    definition.Id + "': " + Unwrap(ex));
                return false;
            }
            finally
            {
                if (prepared != null)
                    UnityEngine.Object.Destroy(prepared);
            }
        }

        private void RegisterVariants(
            object slots,
            WeaponDefinition definition,
            int slotNumber,
            GameObject baseWeapon)
        {
            object family = Item(slots, slotNumber - 1);

            if (family == null)
            {
                variantIndices.Remove(definition.Id);
                return;
            }

            if (!variantIndices.TryGetValue(
                definition.Id,
                out Dictionary<string, int> indices))
            {
                indices = new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);

                variantIndices[definition.Id] = indices;
            }

            // Keep the previous map only to identify real index changes. Rebuild
            // the live map each scan so removed/failed variants cannot leave stale
            // IDs pointing at the wrong native entry.
            Dictionary<string, int> previousIndices =
                new Dictionary<string, int>(
                    indices,
                    StringComparer.OrdinalIgnoreCase);

            indices.Clear();

            if (definition.Variants == null ||
                definition.Variants.Count == 0)
            {
                variantIndices.Remove(definition.Id);
                return;
            }

            // The first declared variant uses the base entry.
            // Additional variants receive independent native entries.
            for (int i = 0; i < definition.Variants.Count; i++)
            {
                WeaponVariantDefinition variant =
                    definition.Variants[i];

                if (variant == null ||
                    string.IsNullOrWhiteSpace(variant.Id))
                    continue;

                string entryName = i == 0
                    ? BaseWeaponName(definition.Id)
                    : VariantWeaponName(
                        definition.Id, variant.Id);

                GameObject entry = FindInFamily(
                    family, entryName, out int familyIndex);

                if (entry == null && i == 0)
                {
                    // The base entry may have been created before variant support.
                    entry = baseWeapon;
                    familyIndex = FindFamilyIndex(family, entry);
                }

                if (entry == null && i > 0)
                {
                    if (!TryCreateVariantEntry(
                        family,
                        baseWeapon,
                        definition,
                        variant,
                        out entry))
                    {
                        continue;
                    }

                    familyIndex = FindFamilyIndex(family, entry);
                }
                else if (entry != null &&
                         !AssignDescriptor(
                             entry, variant.DisplayName, variant.Color))
                {
                    Log(
                        "Warning: descriptor update failed for variant '" +
                        variant.Id + "'.");
                }

                if (entry != null)
                {
                    ApplyModelVisual(
                        entry,
                        variant.Model ?? definition.Model,
                        definition.Id + "/" + variant.Id);

                    Color? modelTint = variant.ModelTint ?? variant.CustomTint;
                    if (modelTint.HasValue)
                        ApplyCustomTint(entry, modelTint.Value);
                    if (variant.IconTint.HasValue)
                        ApplyIconTint(entry, variant.IconTint.Value);
                }

                if (entry == null || familyIndex < 0)
                {
                    Log(
                        "Could not resolve native index for variant '" +
                        definition.Id + "/" + variant.Id + "'.");
                    continue;
                }

                indices[variant.Id] = familyIndex;

                if (!previousIndices.TryGetValue(
                    variant.Id, out int previousIndex) ||
                    previousIndex != familyIndex)
                {
                    Log(
                        "Variant '" + definition.Id + "/" + variant.Id +
                        "' maps to native family index " + familyIndex + ".");
                }
            }

            foreach (KeyValuePair<string, int> previous in previousIndices)
            {
                if (!indices.ContainsKey(previous.Key))
                {
                    Log(
                        "Variant mapping is no longer available: " +
                        definition.Id + "/" + previous.Key + ".");
                }
            }
        }

        private bool TryCreateVariantEntry(
            object family,
            GameObject baseWeapon,
            WeaponDefinition definition,
            WeaponVariantDefinition variant,
            out GameObject created)
        {
            created = null;
            GameObject prepared = null;

            try
            {
                prepared = UnityEngine.Object.Instantiate(
                    baseWeapon,
                    baseWeapon.transform.parent);

                if (prepared == null)
                    return false;

                prepared.name = VariantWeaponName(
                    definition.Id, variant.Id);

                prepared.SetActive(false);

                if (!AssignDescriptor(
                    prepared, variant.DisplayName, variant.Color))
                {
                    Log(
                        "Could not assign descriptor to variant '" +
                        variant.Id + "'.");
                    return false;
                }

                Add(family, prepared);

                created = prepared;
                prepared = null;

                try
                {
                    Invoke(gunControl, "CalculateSlotCount");
                }
                catch (Exception ex)
                {
                    Log(
                        "CalculateSlotCount warning: " +
                        Unwrap(ex));
                }

                Log(
                    "Created native entry for variant '" +
                    definition.Id + "/" + variant.Id + "'.");

                return true;
            }
            catch (Exception ex)
            {
                Log(
                    "Variant creation failed for '" +
                    definition.Id + "/" + variant.Id +
                    "': " + Unwrap(ex));
                return false;
            }
            finally
            {
                if (prepared != null)
                    UnityEngine.Object.Destroy(prepared);
            }
        }

        private static string BaseWeaponName(string id)
        {
            return "ULTRADROID_WEAPON_" + SafeName(id);
        }

        private static string VariantWeaponName(
            string weaponId,
            string variantId)
        {
            return "ULTRADROID_VARIANT_" +
                SafeName(weaponId) + "_" + SafeName(variantId);
        }

        private static GameObject FindInFamily(
            object family,
            string expectedName,
            out int index)
        {
            index = -1;

            for (int i = 0; i < Count(family); i++)
            {
                GameObject item = Item(family, i) as GameObject;

                if (item != null &&
                    string.Equals(
                        item.name,
                        expectedName,
                        StringComparison.Ordinal))
                {
                    index = i;
                    return item;
                }
            }

            return null;
        }

        private static int FindFamilyIndex(
            object family,
            GameObject target)
        {
            if (target == null)
                return -1;

            for (int i = 0; i < Count(family); i++)
            {
                GameObject item = Item(family, i) as GameObject;

                if (item == target)
                    return i;

                if (item != null &&
                    item.GetInstanceID() == target.GetInstanceID())
                    return i;
            }

            return -1;
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
                            StringComparison.Ordinal) &&
                        !weapon.name.StartsWith(
                            "ULTRADROID_VARIANT_",
                            StringComparison.Ordinal))
                    {
                        return weapon;
                    }
                }
            }

            return null;
        }

        private static GameObject FindExistingWeapon(
            object slots,
            string id,
            out int slotNumber)
        {
            slotNumber = -1;
            string expected = BaseWeaponName(id);

            for (int i = 6; i < Count(slots); i++)
            {
                object family = Item(slots, i);

                for (int j = 0; j < Count(family); j++)
                {
                    GameObject weapon =
                        Item(family, j) as GameObject;

                    if (weapon != null &&
                        string.Equals(
                            weapon.name,
                            expected,
                            StringComparison.Ordinal))
                    {
                        slotNumber = i + 1;
                        return weapon;
                    }
                }
            }

            return null;
        }


        // Custom tints are deliberately independent of the native
        // WeaponVariant enum. This avoids sending unsupported enum values
        // (such as Pink or Black) into the game's indexed color arrays.
        private static void ApplyCustomTint(GameObject weapon, Color tint)
        {
            if (weapon == null)
                return;

            try
            {
                Renderer[] renderers =
                    weapon.GetComponentsInChildren<Renderer>(true);

                MaterialPropertyBlock block = new MaterialPropertyBlock();
                int tinted = 0;

                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null)
                        continue;

                    Material[] materials = renderer.sharedMaterials;

                    for (int i = 0; i < materials.Length; i++)
                    {
                        Material material = materials[i];

                        if (material == null)
                            continue;

                        int colorProperty = 0;

                        if (material.HasProperty("_Color"))
                            colorProperty = Shader.PropertyToID("_Color");
                        else if (material.HasProperty("_BaseColor"))
                            colorProperty = Shader.PropertyToID("_BaseColor");

                        if (colorProperty == 0)
                            continue;

                        renderer.GetPropertyBlock(block, i);
                        block.SetColor(colorProperty, tint);
                        renderer.SetPropertyBlock(block, i);

                        tinted++;
                    }
                }

                if (tinted == 0)
                {
                    DebugLog(
                        "Custom tint found no supported material color properties on '" +
                        weapon.name + "'.");
                }
            }
            catch (Exception ex)
            {
                DebugLog(
                    "Custom tint failed for '" + weapon.name +
                    "': " + Unwrap(ex));
            }
        }

        private void UpdateHudIconTints()
        {
            if (gunControl == null)
                return;

            object icons = GetMember(gunControl, "currentWeaponIcons");
            Type iconType = FindType("WeaponIcon");

            if (icons == null || iconType == null)
                return;

            for (int i = 0; i < Count(icons); i++)
            {
                object icon = WrapInstance(iconType, Item(icons, i));

                if (icon == null)
                    continue;

                object descriptor = GetMember(icon, "weaponDescriptor");
                string displayName = Convert.ToString(
                    GetMember(descriptor, "weaponName"));

                if (string.IsNullOrWhiteSpace(displayName))
                    continue;

                Color? tint = null;

                foreach (WeaponDefinition definition in WeaponRegistry.GetAll())
                {
                    if (definition == null)
                        continue;

                    foreach (WeaponVariantDefinition variant in definition.Variants)
                    {
                        if (variant == null ||
                            !string.Equals(
                                displayName,
                                variant.DisplayName,
                                StringComparison.OrdinalIgnoreCase))
                            continue;

                        tint = variant.IconTint
                            ?? variant.ModelTint
                            ?? variant.CustomTint;

                        if (tint.HasValue)
                            break;
                    }

                    if (tint.HasValue)
                        break;
                }

                if (!tint.HasValue)
                    continue;

                object images = GetMember(icon, "variationColoredImages");

                for (int j = 0; j < Count(images); j++)
                {
                    object image = Item(images, j);

                    if (image == null)
                        continue;

                    object current = GetMember(image, "color");

                    if (current is Color currentColor &&
                        ColorsApproximatelyEqual(currentColor, tint.Value))
                        continue;

                    SetMember(image, "color", tint.Value);
                }
            }
        }

        private void UpdateEquippedHudTint()
        {
            if (gunControl == null)
                return;

            GameObject currentWeapon =
                GetMember(gunControl, "currentWeapon") as GameObject;

            if (currentWeapon == null)
                return;

            Type iconType = FindType("WeaponIcon");
            if (iconType == null)
                return;

            object icons = currentWeapon.GetComponentsInChildren(
                Il2CppType.From(iconType), true);

            string displayName = null;

            for (int i = 0; i < Count(icons); i++)
            {
                object icon = WrapInstance(iconType, Item(icons, i));
                if (icon == null)
                    continue;

                object descriptor = GetMember(icon, "weaponDescriptor");
                string name = Convert.ToString(
                    GetMember(descriptor, "weaponName"));

                if (!string.IsNullOrWhiteSpace(name))
                {
                    displayName = name;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(displayName))
                return;

            Color? tint = null;

            foreach (WeaponDefinition definition in WeaponRegistry.GetAll())
            {
                if (definition == null)
                    continue;

                foreach (WeaponVariantDefinition variant in definition.Variants)
                {
                    if (variant == null ||
                        !string.Equals(
                            displayName,
                            variant.DisplayName,
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    tint = variant.IconTint
                        ?? variant.ModelTint
                        ?? variant.CustomTint;

                    if (tint.HasValue)
                        break;
                }

                if (tint.HasValue)
                    break;
            }

            if (!tint.HasValue)
                return;

            Type hudType = FindType("WeaponHUD");
            if (hudType == null)
                return;

            object huds = UnityEngine.Object.FindObjectsOfType(
                Il2CppType.From(hudType));

            for (int i = 0; i < Count(huds); i++)
            {
                object hud = WrapInstance(hudType, Item(huds, i));
                if (hud == null)
                    continue;

                object image = GetMember(hud, "img");
                object glowImage = GetMember(hud, "glowImg");

                if (image != null)
                    SetMember(image, "color", tint.Value);

                if (glowImage != null)
                    SetMember(glowImage, "color", tint.Value);
            }
        }

        private void UpdateWheelTints()
        {
            Type wheelType = FindType("WeaponWheel");

            if (wheelType == null)
            {
                RestoreWheelImageTints(new HashSet<int>());
                return;
            }

            object wheels = UnityEngine.Object.FindObjectsOfType(
                Il2CppType.From(wheelType));

            var desired = new Dictionary<int, Tuple<object, Color>>();

            for (int wi = 0; wi < Count(wheels); wi++)
            {
                object wheel = WrapInstance(wheelType, Item(wheels, wi));

                if (wheel == null)
                    continue;

                object segments = GetMember(wheel, "segments");

                for (int si = 0; si < Count(segments); si++)
                {
                    object segment = Item(segments, si);

                    if (segment == null)
                        continue;

                    object descriptor = GetMember(segment, "descriptor");

                    string displayName = Convert.ToString(
                        GetMember(descriptor, "weaponName"));

                    if (string.IsNullOrWhiteSpace(displayName))
                        continue;

                    Color? tint = FindWheelTint(displayName);

                    if (!tint.HasValue)
                        continue;

                    AddDesiredWheelImage(
                        desired,
                        GetMember(segment, "icon"),
                        tint.Value);

                    AddDesiredWheelImage(
                        desired,
                        GetMember(segment, "iconGlow"),
                        tint.Value);
                }
            }

            foreach (KeyValuePair<int, Tuple<object, Color>> item in desired)
            {
                if (!wheelImageTints.TryGetValue(
                    item.Key,
                    out WheelImageTintRecord record))
                {
                    object image = item.Value.Item1;

                    if (image == null)
                        continue;

                    object rawColor = GetMember(image, "color");

                    if (!(rawColor is Color original))
                        continue;

                    record = new WheelImageTintRecord
                    {
                        Image = image,
                        OriginalColor = original,
                        AppliedColor = item.Value.Item2
                    };

                    wheelImageTints[item.Key] = record;
                }

                record.AppliedColor = item.Value.Item2;

                SetMember(record.Image, "color", item.Value.Item2);
            }

            RestoreWheelImageTints(new HashSet<int>(desired.Keys));
        }

        private Color? FindWheelTint(string displayName)
        {
            foreach (WeaponDefinition definition in WeaponRegistry.GetAll())
            {
                if (definition == null)
                    continue;

                foreach (WeaponVariantDefinition variant in definition.Variants)
                {
                    if (variant == null ||
                        !variant.WheelTintEnabled ||
                        !variant.WheelTint.HasValue ||
                        string.IsNullOrWhiteSpace(variant.DisplayName))
                    {
                        continue;
                    }

                    if (string.Equals(
                        displayName,
                        variant.DisplayName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return variant.WheelTint.Value;
                    }
                }
            }

            return null;
        }

        private static void AddDesiredWheelImage(
            Dictionary<int, Tuple<object, Color>> desired,
            object image,
            Color tint)
        {
            if (image == null)
                return;

            try
            {
                int id = Convert.ToInt32(
                    Invoke(image, "GetInstanceID"));

                if (id != 0)
                    desired[id] = Tuple.Create(image, tint);
            }
            catch
            {
                // An unavailable or destroyed UI image should not break the runtime.
            }
        }

        private void RestoreWheelImageTints(HashSet<int> desiredIds)
        {
            var remove = new List<int>();

            foreach (KeyValuePair<int, WheelImageTintRecord> pair in wheelImageTints)
            {
                if (desiredIds.Contains(pair.Key))
                    continue;

                WheelImageTintRecord record = pair.Value;

                if (record != null && record.Image != null)
                {
                    object rawColor = GetMember(record.Image, "color");

                    // Restore only if the current color is still the one we applied.
                    // This avoids overwriting a color changed by another mod.
                    if (rawColor is Color currentColor &&
                        ColorsApproximatelyEqual(
                            currentColor,
                            record.AppliedColor))
                    {
                        SetMember(record.Image, "color", record.OriginalColor);
                    }
                }

                remove.Add(pair.Key);
            }

            foreach (int id in remove)
                wheelImageTints.Remove(id);
        }

        private static bool ColorsApproximatelyEqual(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.01f &&
                   Mathf.Abs(a.g - b.g) < 0.01f &&
                   Mathf.Abs(a.b - b.b) < 0.01f &&
                   Mathf.Abs(a.a - b.a) < 0.01f;
        }

        private static void ApplyIconTint(GameObject weapon, Color tint)
        {
            if (weapon == null) return;
            try
            {
                Type iconType = FindType("WeaponIcon");
                if (iconType == null) return;
                object components = Invoke(weapon, "GetComponentsInChildren", Il2CppType.From(iconType), true);
                int changed = 0;
                for (int i = 0; i < Count(components); i++)
                {
                    object icon = WrapInstance(iconType, Item(components, i));
                    object images = GetMember(icon, "variationColoredImages");
                    for (int j = 0; j < Count(images); j++)
                    {
                        object image = Item(images, j);
                        if (image != null && SetMember(image, "color", tint)) changed++;
                    }
                }
                // Do not spam logs when the cloned weapon has no variation-colored images.
            }
            catch (Exception ex)
            {
                DebugLog("Icon tint failed for '" + weapon.name + "': " + Unwrap(ex));
            }
        }

        private static bool AssignDescriptor(
            GameObject weapon,
            string displayName,
            WeaponVariantColor color)
        {
            if (weapon == null ||
                string.IsNullOrWhiteSpace(displayName))
                return false;

            Type iconType = FindType("WeaponIcon");
            Type descriptorType = FindType("WeaponDescriptor");

            if (iconType == null || descriptorType == null)
                return false;

            object components;

            try
            {
                components = Invoke(
                    weapon,
                    "GetComponentsInChildren",
                    Il2CppType.From(iconType),
                    true);
            }
            catch
            {
                return false;
            }

            int componentCount = Count(components);

            if (componentCount == 0)
                return false;

            // Do not allocate a new ScriptableObject on every scan. If every
            // WeaponIcon already has the desired descriptor name, no write is
            // required. If a child is missing/mismatched, repair all icons below.
            bool descriptorsAlreadyMatch = true;

            for (int i = 0; i < componentCount; i++)
            {
                object icon = WrapInstance(
                    iconType, Item(components, i));

                if (icon == null)
                {
                    descriptorsAlreadyMatch = false;
                    break;
                }

                object existingDescriptor = GetMember(
                    icon, "weaponDescriptor");

                object existingName = GetMember(
                    existingDescriptor, "weaponName");

                if (!string.Equals(
                    Convert.ToString(existingName),
                    displayName,
                    StringComparison.Ordinal))
                {
                    descriptorsAlreadyMatch = false;
                    break;
                }

                // Default means preserve the template's native color. For an
                // explicit color, ensure the existing descriptor already uses it.
                if (color != WeaponVariantColor.Default &&
                    !HasNativeVariantColor(
                        existingDescriptor, color))
                {
                    descriptorsAlreadyMatch = false;
                    break;
                }
            }

            if (descriptorsAlreadyMatch)
                return true;

            object firstIcon = WrapInstance(
                iconType, Item(components, 0));

            if (firstIcon == null)
                return false;

            object original = GetMember(
                firstIcon, "weaponDescriptor");

            object descriptor = CreateDescriptor(descriptorType);

            if (descriptor == null)
                return false;

            if (original != null)
            {
                SetMember(
                    descriptor, "icon",
                    GetMember(original, "icon"));

                SetMember(
                    descriptor, "glowIcon",
                    GetMember(original, "glowIcon"));
            }

            if (color != WeaponVariantColor.Default &&
                !TrySetNativeVariantColor(descriptor, color))
            {
                DebugLog(
                    "Warning: could not set native variationColor to '" +
                    color + "' for descriptor '" + displayName + "'.");
                return false;
            }

            if (!SetMember(
                descriptor, "weaponName", displayName))
                return false;

            int assigned = 0;

            for (int i = 0; i < componentCount; i++)
            {
                object icon = WrapInstance(
                    iconType, Item(components, i));

                if (icon != null &&
                    SetMember(
                        icon, "weaponDescriptor", descriptor))
                {
                    assigned++;
                }
            }

            return assigned > 0;
        }

        private static bool HasNativeVariantColor(
            object descriptor,
            WeaponVariantColor requestedColor)
        {
            object currentColor = GetMember(
                descriptor, "variationColor");

            if (currentColor == null)
                return false;

            try
            {
                return Convert.ToInt32(currentColor) ==
                    (int)requestedColor;
            }
            catch
            {
                return string.Equals(
                    Convert.ToString(currentColor),
                    requestedColor.ToString(),
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool TrySetNativeVariantColor(
            object descriptor,
            WeaponVariantColor requestedColor)
        {
            if (descriptor == null)
                return false;

            Type descriptorType = descriptor.GetType();

            for (Type type = descriptorType;
                type != null;
                type = type.BaseType)
            {
                FieldInfo field = type.GetField(
                    "variationColor",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);

                if (field != null)
                {
                    try
                    {
                        object value = ConvertColorValue(
                            field.FieldType, requestedColor);

                        field.SetValue(descriptor, value);
                        return true;
                    }
                    catch { }
                }

                PropertyInfo property = type.GetProperty(
                    "variationColor",
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
                            object value = ConvertColorValue(
                                property.PropertyType, requestedColor);

                            property.SetValue(descriptor, value, null);
                            return true;
                        }
                        catch { }
                    }
                }
            }

            return false;
        }

        private static object ConvertColorValue(
            Type nativeType,
            WeaponVariantColor requestedColor)
        {
            if (nativeType.IsEnum)
            {
                return Enum.ToObject(
                    nativeType, (int)requestedColor);
            }

            Type underlying = Nullable.GetUnderlyingType(nativeType)
                ?? nativeType;

            return Convert.ChangeType(
                (int)requestedColor, underlying);
        }

        private static object CreateDescriptor(Type descriptorType)
        {
            foreach (MethodInfo method in
                typeof(ScriptableObject).GetMethods(
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

            foreach (MethodInfo method in
                typeof(ScriptableObject).GetMethods(
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

        private static object FindReadyGunControl(
            Action<string> report)
        {
            Type type = FindType("GunControl");

            if (type == null)
            {
                report?.Invoke(
                    "Waiting: GunControl type is unavailable.");
                return null;
            }

            object found;

            try
            {
                found = UnityEngine.Object.FindObjectsOfType(
                    Il2CppType.From(type));
            }
            catch (Exception ex)
            {
                report?.Invoke(
                    "GunControl search failed: " + Unwrap(ex));
                return null;
            }

            for (int i = 0; i < Count(found); i++)
            {
                object candidate;

                try
                {
                    candidate = WrapInstance(
                        type, Item(found, i));
                }
                catch (Exception ex)
                {
                    report?.Invoke(
                        "Could not wrap GunControl candidate: " +
                        Unwrap(ex));
                    continue;
                }

                if (candidate == null)
                    continue;

                object slots = GetMember(candidate, "slots");

                if (slots == null ||
                    Count(slots) < 6 ||
                    GetPointer(candidate) == IntPtr.Zero)
                    continue;

                // Don't log readiness here: this function is polled repeatedly.
                // UpdateRuntime logs only when the instance pointer changes.
                return candidate;
            }

            return null;
        }

        private static bool HasPopulatedVanillaSlots(object slots)
        {
            return slots != null && Count(slots) >= 6;
        }

        private static Type FindType(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            if (typeCache.TryGetValue(name, out Type cached) &&
                cached != null)
                return cached;

            foreach (Assembly assembly in
                AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type direct = assembly.GetType(name, false);

                    if (direct != null)
                    {
                        typeCache[name] = direct;
                        return direct;
                    }

                    foreach (Type candidate in assembly.GetTypes())
                    {
                        if (candidate != null &&
                            candidate.Name == name)
                        {
                            typeCache[name] = candidate;
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
                            typeCache[name] = candidate;
                            return candidate;
                        }
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

        private static object GetMember(
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

        private static bool SetMember(
            object instance,
            string name,
            object value)
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
                            property.SetValue(
                                instance, value, null);
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

            return count == null
                ? 0
                : Convert.ToInt32(count);
        }

        private static object Item(
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
                foreach (MethodInfo method in
                    collection.GetType().GetMethods(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic))
                {
                    if (method.Name == "get_Item" &&
                        method.GetParameters().Length == 1 &&
                        method.GetParameters()[0].ParameterType ==
                            typeof(int))
                    {
                        return method.Invoke(
                            collection, new object[] { index });
                    }
                }
            }
            catch { }

            return null;
        }

        private static void Add(
            object collection,
            object value)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            foreach (MethodInfo method in
                collection.GetType().GetMethods(
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
                foreach (MethodInfo method in
                    type.GetMethods(
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
                if (!char.IsLetterOrDigit(chars[i]) &&
                    chars[i] != '_')
                {
                    chars[i] = '_';
                }
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
        private int diagnosticFrames;

        public WeaponGameRuntimeBehaviour(IntPtr ptr)
            : base(ptr)
        {
        }

        public void Awake()
        {
            WeaponGameRuntime.DebugLog("LIFECYCLE: Awake fired.");
        }

        public void OnEnable()
        {
            WeaponGameRuntime.DebugLog("LIFECYCLE: OnEnable fired.");
        }

        public void Start()
        {
            WeaponGameRuntime.DebugLog("LIFECYCLE: Start fired.");
        }

        public void Update()
        {
            if (diagnosticFrames < 3)
            {
                diagnosticFrames++;

                WeaponGameRuntime.DebugLog(
                    "LIFECYCLE: Update fired; frame=" +
                    Time.frameCount);
            }

            // No periodic heartbeat: it adds log noise without proving more than
            // the successful runtime discovery/startup messages already show.
            WeaponGameRuntime.Tick();
        }
    }
}
