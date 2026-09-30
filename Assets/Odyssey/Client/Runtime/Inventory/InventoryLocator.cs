using System;
using System.Collections.Generic;
using Odyssey.Application.Content;
using Odyssey.Application.Inventory;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;

namespace Odyssey.Unity.Client
{
    /// <summary>One item of an inventory as the UI shows it: an instance or a stack, plus its equipped entry if any.</summary>
    public sealed class InventoryItemView
    {
        public InventoryItemView(ItemInstanceRecord instance, EquippedEntryRecord? equipped, string name, string summary)
        {
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
            Ref = InventoryItemRef.ForInstance(instance.ItemInstanceId);
            Location = instance.LocationRef;
            Revision = instance.Revision;
            DefinitionRef = instance.SourceItemDefinitionRef;
            Snapshot = instance.MechanicsSnapshot;
            Equipped = equipped;
            Name = name;
            Summary = summary;
        }

        public InventoryItemView(ItemStackRecord stack, EquippedEntryRecord? equipped, string name, string summary)
        {
            Stack = stack ?? throw new ArgumentNullException(nameof(stack));
            Ref = InventoryItemRef.ForStack(stack.ItemStackId);
            Location = stack.LocationRef;
            Revision = stack.Revision;
            DefinitionRef = stack.SourceItemDefinitionRef;
            Snapshot = stack.MechanicsSnapshot;
            Equipped = equipped;
            Name = name;
            Summary = summary;
        }

        public ItemInstanceRecord? Instance { get; }
        public ItemStackRecord? Stack { get; }
        public bool IsStack => Stack != null;
        public long Quantity => Stack?.Quantity.Value ?? 1;
        public InventoryItemRef Ref { get; }
        public InventoryLocationRef Location { get; }
        public long Revision { get; }
        public ContentDefinitionRef DefinitionRef { get; }
        public ItemMechanicsSnapshot Snapshot { get; }
        public EquippedEntryRecord? Equipped { get; }
        public string Name { get; }
        public string Summary { get; }
        public string Key => Ref.Kind == InventoryItemRefKind.ItemInstance ? Ref.ItemInstanceId.ToString() : Ref.ItemStackId.ToString();

        /// <summary>The UI zone: "Equipped: slot", "Container: key", "Dropped on scene", "Other".</summary>
        public string ZoneLabel
        {
            get
            {
                if (Equipped != null) return "Equipped: " + Equipped.Entry.EquipmentSlotRef;
                switch (Location.Kind)
                {
                    case InventoryLocationKind.Equipped:
                        return "Equipped: " + Location.DetailRef;
                    case InventoryLocationKind.Contained:
                        return "Container: " + Location.DetailRef;
                    case InventoryLocationKind.SceneDropped:
                        return "Dropped on scene";
                    default:
                        return "Other";
                }
            }
        }

        public bool IsEquipped => Equipped != null || Location.Kind == InventoryLocationKind.Equipped;
    }

    /// <summary>A loaded inventory: the record and its aggregated items (no single backend query returns all of it).</summary>
    public sealed class InventoryView
    {
        public InventoryView(InventoryRecord inventory, IReadOnlyList<InventoryItemView> items)
        {
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public InventoryRecord Inventory { get; }
        public IReadOnlyList<InventoryItemView> Items { get; }

        /// <summary>Items grouped by zone, equipped first, then containers in name order.</summary>
        public IReadOnlyList<KeyValuePair<string, List<InventoryItemView>>> Zones
        {
            get
            {
                var byZone = new SortedDictionary<string, List<InventoryItemView>>(StringComparer.Ordinal);
                foreach (InventoryItemView item in Items)
                {
                    string zone = item.ZoneLabel;
                    if (!byZone.TryGetValue(zone, out List<InventoryItemView> list)) byZone[zone] = list = new List<InventoryItemView>();
                    list.Add(item);
                }

                var ordered = new List<KeyValuePair<string, List<InventoryItemView>>>();
                foreach (KeyValuePair<string, List<InventoryItemView>> zone in byZone) if (zone.Key.StartsWith("Equipped", StringComparison.Ordinal)) ordered.Add(zone);
                foreach (KeyValuePair<string, List<InventoryItemView>> zone in byZone) if (!zone.Key.StartsWith("Equipped", StringComparison.Ordinal)) ordered.Add(zone);
                return ordered;
            }
        }

        public InventoryItemView? Find(string key)
        {
            foreach (InventoryItemView item in Items) if (item.Key == key) return item;
            return null;
        }
    }

    /// <summary>
    /// ODY-S11-204: finds, creates and loads inventories. The backend has no "inventory of this owner" query, so the UI
    /// uses one stable, client-side convention for the one inventory each owner has: the <see cref="InventoryId"/> hex
    /// equals the owner's own id hex (<c>char_X</c> -> <c>inv_X</c>, <c>scn_X</c> -> <c>inv_X</c>). It is only an id
    /// choice passed to the backend's own <see cref="IInventoryRepository.CreateInventory"/>; the backend owns the rows.
    /// Recorded in the task contract as a limitation pending a backend owner lookup.
    /// </summary>
    public static class InventoryLocator
    {
        public const string DefaultContainerKey = "backpack";
        public const string SceneLocationKey = "ground";

        public static InventoryId CharacterInventoryId(CharacterId characterId) => FromOwnerHex(characterId.ToString());

        public static InventoryId SceneInventoryId(SceneId sceneId) => FromOwnerHex(sceneId.ToString());

        public static InventoryOwnerRef CharacterOwner(CharacterId characterId) => InventoryOwnerRef.ForCharacter(characterId);

        public static InventoryOwnerRef SceneOwner(SceneId sceneId) => InventoryOwnerRef.ForScene(sceneId, SceneLocationKey);

        public static Result<InventoryRecord> Get(IInventoryRepository inventory, CampaignHandle campaign, InventoryId inventoryId) =>
            inventory.GetInventory(campaign, inventoryId, UiCommandIds.NewCorrelationId());

        public static Result<InventoryRecord> Create(IInventoryRepository inventory, CampaignHandle campaign, InventoryId inventoryId, InventoryOwnerRef owner, Odyssey.Application.Time.IWallClock clock)
        {
            var now = clock.GetUtcNow();
            return UiGuard.Run(() => inventory.CreateInventory(campaign, new InventoryRecord(inventoryId, campaign.CampaignId, owner, 1, now, now), UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()));
        }

        /// <summary>Aggregates instances + stacks + equipped entries of one inventory, naming each item from the catalog.</summary>
        public static Result<InventoryView> Load(IInventoryRepository inventory, IContentCatalogRepository catalog, CampaignHandle campaign, InventoryId inventoryId)
        {
            CorrelationId correlationId = UiCommandIds.NewCorrelationId();
            Result<InventoryRecord> record = inventory.GetInventory(campaign, inventoryId, correlationId);
            if (record.IsFailure) return Result<InventoryView>.Failure(record.Error);

            Result<IReadOnlyList<ItemInstanceRecord>> instances = inventory.ListItemInstances(campaign, campaign.CampaignId, inventoryId, correlationId);
            if (instances.IsFailure) return Result<InventoryView>.Failure(instances.Error);
            Result<IReadOnlyList<ItemStackRecord>> stacks = inventory.ListItemStacks(campaign, campaign.CampaignId, inventoryId, correlationId);
            if (stacks.IsFailure) return Result<InventoryView>.Failure(stacks.Error);
            Result<IReadOnlyList<EquippedEntryRecord>> equipped = inventory.ListEquippedEntries(campaign, campaign.CampaignId, inventoryId, correlationId);
            if (equipped.IsFailure) return Result<InventoryView>.Failure(equipped.Error);

            var entries = new Dictionary<string, EquippedEntryRecord>(StringComparer.Ordinal);
            foreach (EquippedEntryRecord entry in equipped.Value) entries[RefKey(entry.Entry.ItemRef)] = entry;

            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            var items = new List<InventoryItemView>();
            foreach (ItemInstanceRecord instance in instances.Value)
            {
                entries.TryGetValue(RefKey(InventoryItemRef.ForInstance(instance.ItemInstanceId)), out EquippedEntryRecord? entry);
                items.Add(new InventoryItemView(instance, entry, NameOf(catalog, campaign, instance.SourceItemDefinitionRef, names, correlationId), Describe(instance.MechanicsSnapshot, correlationId)));
            }

            foreach (ItemStackRecord stack in stacks.Value)
            {
                entries.TryGetValue(RefKey(InventoryItemRef.ForStack(stack.ItemStackId)), out EquippedEntryRecord? entry);
                items.Add(new InventoryItemView(stack, entry, NameOf(catalog, campaign, stack.SourceItemDefinitionRef, names, correlationId), Describe(stack.MechanicsSnapshot, correlationId)));
            }

            return Result<InventoryView>.Success(new InventoryView(record.Value, items));
        }

        /// <summary>A one-line summary of what the item instance really has (its mechanics snapshot, not the live catalog).</summary>
        public static string Describe(ItemMechanicsSnapshot snapshot, CorrelationId correlationId)
        {
            switch (snapshot.ContentType)
            {
                case ContentDefinitionType.Weapon:
                    {
                        Result<WeaponDefinition> weapon = TypedDefinitionCodec.DecodeWeapon(snapshot.ContentType, snapshot.Payload, correlationId);
                        if (weapon.IsFailure) break;
                        return "Weapon · " + weapon.Value.DamageExpression + " · " + weapon.Value.AttackMode + " · range " + weapon.Value.Range + (weapon.Value.AmmoRequirement != AmmoRequirement.None ? " · ammo " + weapon.Value.AmmoRequirement : string.Empty);
                    }
                case ContentDefinitionType.Armor:
                    {
                        Result<ArmorDefinition> armor = TypedDefinitionCodec.DecodeArmor(snapshot.ContentType, snapshot.Payload, correlationId);
                        if (armor.IsFailure) break;
                        var parts = new List<string>();
                        foreach (var part in armor.Value.CoveredBodyPartIds) parts.Add(part.ToString());
                        return "Armor · slot " + armor.Value.EquipmentSlotKey + " · protection " + armor.Value.Protection + " · covers " + string.Join(", ", parts);
                    }
                case ContentDefinitionType.Ammo:
                    {
                        Result<AmmoDefinition> ammo = TypedDefinitionCodec.DecodeAmmo(snapshot.ContentType, snapshot.Payload, correlationId);
                        if (ammo.IsFailure) break;
                        return "Ammo · keys " + string.Join(", ", ammo.Value.CompatibilityKeys) + (ammo.Value.DamageContribution != null ? " · +" + ammo.Value.DamageContribution : string.Empty);
                    }
                case ContentDefinitionType.Item:
                    {
                        Result<ItemDefinition> item = TypedDefinitionCodec.DecodeItem(snapshot.ContentType, snapshot.Payload, correlationId);
                        if (item.IsFailure) break;
                        return EnumChoices.Humanize(item.Value.Category.ToString()) + " · weight " + item.Value.Weight + (item.Value.HasCharges ? " · charges " + item.Value.MaxCharges : string.Empty);
                    }
            }

            return snapshot.ContentType.ToString();
        }

        /// <summary>Client-side, non-authoritative equip hint from an Armor snapshot: its slot key and covered parts.</summary>
        public static bool TryGetArmorHint(ItemMechanicsSnapshot snapshot, out string slotKey, out IReadOnlyList<string> bodyParts)
        {
            slotKey = string.Empty;
            bodyParts = Array.Empty<string>();
            if (snapshot.ContentType != ContentDefinitionType.Armor) return false;
            Result<ArmorDefinition> armor = TypedDefinitionCodec.DecodeArmor(snapshot.ContentType, snapshot.Payload, UiCommandIds.NewCorrelationId());
            if (armor.IsFailure) return false;
            slotKey = armor.Value.EquipmentSlotKey;
            var parts = new List<string>();
            foreach (var part in armor.Value.CoveredBodyPartIds) parts.Add(part.ToString());
            bodyParts = parts;
            return true;
        }

        /// <summary>
        /// The Domain's canonical token shape for container/slot keys (lowercase letters, digits, '_' or '-', at most 96),
        /// mirrored here only to show a readable message before calling the backend, which checks it again.
        /// </summary>
        public static bool IsContainerKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > 96 || value.Trim() != value) return false;
            foreach (char c in value)
            {
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-')) return false;
            }

            return true;
        }

        public static string RefKey(InventoryItemRef itemRef) => itemRef.Kind == InventoryItemRefKind.ItemInstance ? itemRef.ItemInstanceId.ToString() : itemRef.ItemStackId.ToString();

        private static InventoryId FromOwnerHex(string ownerId)
        {
            int separator = ownerId.IndexOf('_');
            return InventoryId.Parse("inv_" + ownerId.Substring(separator + 1));
        }

        // Live Name from the catalog (the recommended source for display); falls back to the type when the row is gone.
        private static string NameOf(IContentCatalogRepository catalog, CampaignHandle campaign, ContentDefinitionRef reference, Dictionary<string, string> cache, CorrelationId correlationId)
        {
            string key = reference.DefinitionId.ToString();
            if (cache.TryGetValue(key, out string cached)) return cached;
            Result<ContentDefinitionRecord> definition = catalog.GetContentDefinition(campaign, reference.DefinitionId, correlationId);
            string name = definition.IsSuccess ? definition.Value.Name : "Unknown item";
            cache[key] = name;
            return name;
        }
    }
}
