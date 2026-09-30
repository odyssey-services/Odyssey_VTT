using System;
using System.Collections.Generic;
using Odyssey.Application.Inventory;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    /// <summary>Which of the two inventories a panel action targets.</summary>
    public enum InventoryOwnerChoice
    {
        Character = 1,
        SceneGround = 2
    }

    /// <summary>
    /// ODY-S11-204: inventory and equipment of the character open in the Character panel, plus the scene's ground
    /// inventory (drop/pick up). Items are grouped by zone (equipped slot / container / dropped / other), named from the
    /// live catalog and described from their own mechanics snapshot.
    ///
    /// Every inventory mutation is MainGM-only in the backend (create, move, split, merge, equip, unequip); a non-MainGM
    /// gets an explanation banner and a read-only list, never silently hidden controls. Each command passes the current
    /// revisions (item, source/destination inventory, equipped entry) of freshly loaded state, and both inventories are
    /// reloaded after every successful call.
    ///
    /// Equip: slot and body parts are free input; for Armor the snapshot's slot key / covered parts pre-fill them as a
    /// client hint only -- the backend does not check slot compatibility and the UI does not add rules it does not have.
    /// </summary>
    public sealed class InventoryPanelPresenter : IDisposable
    {
        private readonly GameSessionContext _context;
        private readonly IInventoryRepository _inventory;
        private readonly IContentCatalogRepository _catalog;
        private readonly ICharacterRepository _characters;
        private readonly List<ContentDefinitionRecord> _creatable = new List<ContentDefinitionRecord>();
        private VisualElement? _body;
        private OdyBanner? _roleNotice;
        private IDisposable? _roleSubscription;
        private bool _disposed;

        public InventoryPanelPresenter(GameSessionContext context, IInventoryRepository inventory, IContentCatalogRepository catalog, ICharacterRepository characters)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
            Banner = new OdyBanner("inventory-banner");
        }

        public OdyBanner Banner { get; }
        public CharacterRecord? Character { get; private set; }
        public InventoryView? CharacterInventory { get; private set; }
        public InventoryView? GroundInventory { get; private set; }
        public string? SelectedItemKey { get; private set; }
        public bool CanManage => _context.ActorIsMainGm;

        /// <summary>Published definitions that can become items (Item/Weapon/Armor as instances; stackable Item/Ammo as stacks).</summary>
        public IReadOnlyList<ContentDefinitionRecord> CreatableDefinitions => _creatable;

        public VisualElement BuildView()
        {
            var root = new VisualElement { name = "inventory-panel" };
            _roleNotice = new OdyBanner("inventory-role-notice");
            root.Add(_roleNotice.Element);
            root.Add(Banner.Element);
            _body = new VisualElement { name = "inventory-body" };
            root.Add(_body);
            _roleSubscription = _context.Selection.Subscribe(_ => Refresh());
            _context.PresentationRuntime.AddSubscription(_roleSubscription);
            Refresh();
            return root;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _roleSubscription?.Dispose();
            _disposed = true;
        }

        /// <summary>Follows the Character panel's open character (null clears the character side).</summary>
        public void SetCharacter(CharacterRecord? character)
        {
            Character = character;
            SelectedItemKey = null;
            Refresh();
        }

        public Result Refresh()
        {
            if (_roleNotice != null)
            {
                if (CanManage) _roleNotice.Hide();
                else _roleNotice.Show(OdyBannerKind.Info, "Only the MainGM can create, move, split, merge, equip or unequip items -- that is a backend rule. Ask your MainGM to change your inventory.");
            }

            if (Character != null)
            {
                Result<CharacterRecord> fresh = _characters.GetCharacter(_context.Campaign, Character.CharacterId, UiCommandIds.NewCorrelationId());
                if (fresh.IsSuccess) Character = fresh.Value;
            }

            CharacterInventory = Character == null ? null : LoadOrNull(InventoryLocator.CharacterInventoryId(Character.CharacterId));
            GroundInventory = LoadOrNull(InventoryLocator.SceneInventoryId(_context.SceneId));
            LoadCreatable();
            Render();
            return Result.Success();
        }

        public InventoryView? ViewOf(InventoryOwnerChoice owner) => owner == InventoryOwnerChoice.Character ? CharacterInventory : GroundInventory;

        public InventoryItemView? FindItem(string key) => CharacterInventory?.Find(key) ?? GroundInventory?.Find(key);

        public void SelectItem(string? key)
        {
            SelectedItemKey = key;
            Render();
        }

        /// <summary>MainGM: creates the (single) inventory of the chosen owner.</summary>
        public Result<InventoryRecord> CreateInventory(InventoryOwnerChoice owner)
        {
            if (!RequireManager("Creating an inventory")) return Result<InventoryRecord>.Failure(InventoryUiErrors.Denied());
            if (owner == InventoryOwnerChoice.Character && Character == null) return Result<InventoryRecord>.Failure(UiGuard.InvalidRequest());
            Result<InventoryRecord> created = owner == InventoryOwnerChoice.Character
                ? InventoryLocator.Create(_inventory, _context.Campaign, InventoryLocator.CharacterInventoryId(Character!.CharacterId), InventoryLocator.CharacterOwner(Character.CharacterId), _context.Clock)
                : InventoryLocator.Create(_inventory, _context.Campaign, InventoryLocator.SceneInventoryId(_context.SceneId), InventoryLocator.SceneOwner(_context.SceneId), _context.Clock);
            return Finish("Inventory created.", "Creating an inventory", created);
        }

        /// <summary>MainGM: a new item from a Published definition -- a stack (with quantity) for stackable Item/Ammo, otherwise an instance.</summary>
        public Result<string> CreateItem(ContentDefinitionId definitionId, long quantity, string containerKey, InventoryOwnerChoice owner = InventoryOwnerChoice.Character)
        {
            if (!RequireManager("Creating an item")) return Result<string>.Failure(InventoryUiErrors.Denied());
            InventoryView? target = ViewOf(owner);
            if (target == null)
            {
                Banner.Show(OdyBannerKind.Warning, "Create the inventory first.");
                return Result<string>.Failure(UiGuard.InvalidRequest());
            }

            ContentDefinitionRecord? definition = null;
            foreach (ContentDefinitionRecord candidate in _creatable) if (candidate.ContentDefinitionId.Equals(definitionId)) definition = candidate;
            if (definition == null)
            {
                Banner.Show(OdyBannerKind.Error, "Choose a published Item, Weapon, Armor or Ammo definition.");
                return Result<string>.Failure(UiGuard.InvalidRequest());
            }

            string container = string.IsNullOrWhiteSpace(containerKey) ? InventoryLocator.DefaultContainerKey : containerKey.Trim();
            InventoryId inventoryId = target.Inventory.InventoryId;
            InventoryOwnerRef ownerRef = target.Inventory.OwnerRef;
            // Same location shape as a move produces (a container of the target inventory) for both owners. Built inside
            // UiGuard: a non-canonical container key is a precondition failure, shown as an invalid request.
            if (!InventoryLocator.IsContainerKey(container))
            {
                Banner.Show(OdyBannerKind.Error, "Container keys are short lowercase tokens, e.g. backpack or belt_pouch.");
                return Result<string>.Failure(UiGuard.InvalidRequest());
            }

            InventoryLocationRef location = InventoryLocationRef.Contained(inventoryId, container);

            if (CreatesStack(definition))
            {
                if (quantity < 1)
                {
                    Banner.Show(OdyBannerKind.Error, "Quantity must be at least 1.");
                    return Result<string>.Failure(UiGuard.InvalidRequest());
                }

                ItemStackId stackId = ItemStackId.NewId(_context.Clock.GetUtcNow());
                Result<ItemStackRecord> stack = UiGuard.Run(() => InventoryCreationService.CreateItemStackFromDefinition(_catalog, _inventory, _context.CampaignRepository, _context.Clock,
                    new CreateItemStackFromDefinitionRequest(_context.Campaign, stackId, inventoryId, ownerRef, location, definitionId, ItemStackQuantity.Create(quantity), _context.ActorUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId())));
                if (stack.IsFailure) return Fail<string>("Creating an item", stack.Error);
                Reload("Created " + quantity + " × " + definition.Name + ".");
                return Result<string>.Success(stack.Value.ItemStackId.ToString());
            }

            ItemInstanceId instanceId = ItemInstanceId.NewId(_context.Clock.GetUtcNow());
            Result<ItemInstanceRecord> instance = UiGuard.Run(() => InventoryCreationService.CreateItemInstanceFromDefinition(_catalog, _inventory, _context.CampaignRepository, _context.Clock,
                new CreateItemInstanceFromDefinitionRequest(_context.Campaign, instanceId, inventoryId, ownerRef, location, definitionId, _context.ActorUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId())));
            if (instance.IsFailure) return Fail<string>("Creating an item", instance.Error);
            Reload("Created " + definition.Name + ".");
            return Result<string>.Success(instance.Value.ItemInstanceId.ToString());
        }

        /// <summary>MainGM: moves an item to a container of the chosen inventory (same inventory = container change).</summary>
        public Result Move(string itemKey, InventoryOwnerChoice destination, string containerKey)
        {
            if (!RequireManager("Moving")) return Result.Failure(InventoryUiErrors.Denied());
            if (!TryLocate(itemKey, out InventoryItemView? item, out InventoryView? source)) return Result.Failure(UiGuard.InvalidRequest());
            InventoryView? target = ViewOf(destination);
            if (target == null)
            {
                Banner.Show(OdyBannerKind.Warning, "The destination inventory does not exist yet.");
                return Result.Failure(UiGuard.InvalidRequest());
            }

            if (item!.IsEquipped)
            {
                Banner.Show(OdyBannerKind.Warning, "Unequip the item before moving it.");
                return Result.Failure(UiGuard.InvalidRequest());
            }

            string container = string.IsNullOrWhiteSpace(containerKey) ? InventoryLocator.DefaultContainerKey : containerKey.Trim();
            InventoryRecord from = source!.Inventory;
            InventoryRecord to = target.Inventory;
            Result moved;
            if (item.IsStack)
            {
                Result<ItemStackRecord> result = UiGuard.Run(() => InventoryMovementService.MoveItemStack(_inventory, _context.CampaignRepository,
                    new MoveItemStackRequest(_context.Campaign, item.Stack!.ItemStackId, item.Revision, from.InventoryId, from.Revision, to.InventoryId, to.Revision, container, _context.ActorUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId())));
                moved = result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
            }
            else
            {
                Result<ItemInstanceRecord> result = UiGuard.Run(() => InventoryMovementService.MoveItemInstance(_inventory, _context.CampaignRepository,
                    new MoveItemInstanceRequest(_context.Campaign, item.Instance!.ItemInstanceId, item.Revision, from.InventoryId, from.Revision, to.InventoryId, to.Revision, container, _context.ActorUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId())));
                moved = result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
            }

            if (moved.IsFailure) return FailPlain("Moving", moved.Error);
            Reload(item.Name + " moved to " + container + ".");
            return moved;
        }

        /// <summary>MainGM: splits <paramref name="quantity"/> off a stack into a new stack of the same inventory.</summary>
        public Result<string> Split(string stackKey, long quantity)
        {
            if (!RequireManager("Splitting")) return Result<string>.Failure(InventoryUiErrors.Denied());
            if (!TryLocate(stackKey, out InventoryItemView? item, out InventoryView? source) || !item!.IsStack) return Result<string>.Failure(UiGuard.InvalidRequest());
            if (quantity < 1 || quantity >= item.Quantity)
            {
                Banner.Show(OdyBannerKind.Error, "Split between 1 and " + (item.Quantity - 1) + ".");
                return Result<string>.Failure(UiGuard.InvalidRequest());
            }

            ItemStackId resultId = ItemStackId.NewId(_context.Clock.GetUtcNow());
            Result<ItemStackRecord> split = UiGuard.Run(() => InventoryStackOperationService.Split(_inventory, _context.CampaignRepository,
                new SplitItemStackRequest(_context.Campaign, item.Stack!.ItemStackId, resultId, source!.Inventory.InventoryId, quantity, item.Revision, source.Inventory.Revision, _context.ActorUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId())));
            if (split.IsFailure) return Fail<string>("Splitting", split.Error);
            Reload("Split " + quantity + " off.");
            return Result<string>.Success(resultId.ToString());
        }

        /// <summary>MainGM: merges <paramref name="sourceKey"/> into <paramref name="destinationKey"/> (same definition, same inventory).</summary>
        public Result Merge(string sourceKey, string destinationKey)
        {
            if (!RequireManager("Merging")) return Result.Failure(InventoryUiErrors.Denied());
            if (!TryLocate(sourceKey, out InventoryItemView? source, out InventoryView? inventory) || !source!.IsStack) return Result.Failure(UiGuard.InvalidRequest());
            InventoryItemView? destination = inventory!.Find(destinationKey);
            if (destination == null || !destination.IsStack || destination.Key == source.Key) return Result.Failure(UiGuard.InvalidRequest());

            Result<ItemStackRecord> merged = UiGuard.Run(() => InventoryStackOperationService.Merge(_inventory, _context.CampaignRepository,
                new MergeItemStacksRequest(_context.Campaign, source.Stack!.ItemStackId, destination.Stack!.ItemStackId, inventory.Inventory.InventoryId, source.Revision, destination.Revision, inventory.Inventory.Revision, _context.ActorUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId())));
            if (merged.IsFailure) return FailPlain("Merging", merged.Error);
            Reload("Stacks merged.");
            return Result.Success();
        }

        /// <summary>MainGM: equips an item of the character's inventory into a free-text slot, optionally on body parts.</summary>
        public Result Equip(string itemKey, string equipmentSlot, IReadOnlyList<string> bodyParts)
        {
            if (!RequireManager("Equipping")) return Result.Failure(InventoryUiErrors.Denied());
            if (!TryLocate(itemKey, out InventoryItemView? item, out InventoryView? inventory)) return Result.Failure(UiGuard.InvalidRequest());
            if (string.IsNullOrWhiteSpace(equipmentSlot))
            {
                Banner.Show(OdyBannerKind.Error, "Enter an equipment slot.");
                return Result.Failure(UiGuard.InvalidRequest());
            }

            var parts = new List<BodyPartId>();
            foreach (string part in bodyParts ?? Array.Empty<string>())
            {
                if (!BodyPartId.TryParse(part, out BodyPartId id))
                {
                    Banner.Show(OdyBannerKind.Error, "\"" + part + "\" is not a valid body part id.");
                    return Result.Failure(UiGuard.InvalidRequest());
                }

                parts.Add(id);
            }

            Result<EquippedEntryRecord> equipped = UiGuard.Run(() => EquipmentService.Equip(_inventory, _characters, _context.CampaignRepository,
                new EquipRequest(_context.Campaign, item!.Ref, inventory!.Inventory.InventoryId, item.Revision, equipmentSlot.Trim(), parts, _context.ActorUserId, _context.Clock.GetUtcNow(), _context.ActorUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId())));
            if (equipped.IsFailure) return FailPlain("Equipping", equipped.Error);
            Reload(item!.Name + " equipped (" + equipmentSlot.Trim() + ").");
            return Result.Success();
        }

        /// <summary>MainGM: unequips into a container of the same inventory.</summary>
        public Result Unequip(string itemKey, string containerKey)
        {
            if (!RequireManager("Unequipping")) return Result.Failure(InventoryUiErrors.Denied());
            if (!TryLocate(itemKey, out InventoryItemView? item, out InventoryView? inventory) || item!.Equipped == null) return Result.Failure(UiGuard.InvalidRequest());
            string container = string.IsNullOrWhiteSpace(containerKey) ? InventoryLocator.DefaultContainerKey : containerKey.Trim();
            Result<bool> unequipped = UiGuard.Run(() => EquipmentService.Unequip(_inventory, _context.CampaignRepository,
                new UnequipRequest(_context.Campaign, item.Ref, inventory!.Inventory.InventoryId, item.Revision, item.Equipped.Entry.Revision, container, _context.ActorUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId())));
            if (unequipped.IsFailure) return FailPlain("Unequipping", unequipped.Error);
            Reload(item.Name + " unequipped into " + container + ".");
            return Result.Success();
        }

        public static bool CreatesStack(ContentDefinitionRecord definition)
        {
            if (definition.DefinitionType == ContentDefinitionType.Ammo) return true;
            if (definition.DefinitionType != ContentDefinitionType.Item) return false;
            Result<ItemDefinition> item = Odyssey.Application.Content.TypedDefinitionCodec.DecodeItem(definition.DefinitionType, definition.PropertiesJson, UiCommandIds.NewCorrelationId());
            return item.IsSuccess && item.Value.IsStackable;
        }

        // ---- internals ------------------------------------------------------------------------------

        private InventoryView? LoadOrNull(InventoryId id)
        {
            Result<InventoryView> loaded = InventoryLocator.Load(_inventory, _catalog, _context.Campaign, id);
            return loaded.IsSuccess ? loaded.Value : null;
        }

        private void LoadCreatable()
        {
            _creatable.Clear();
            Result<IReadOnlyList<ContentDefinitionRecord>> published = _catalog.ListContentDefinitions(_context.Campaign, ContentDefinitionStatus.Published, UiCommandIds.NewCorrelationId());
            if (published.IsFailure) return;
            foreach (ContentDefinitionRecord record in published.Value)
            {
                ContentDefinitionType type = record.DefinitionType;
                if (type == ContentDefinitionType.Item || type == ContentDefinitionType.Weapon || type == ContentDefinitionType.Armor || type == ContentDefinitionType.Ammo) _creatable.Add(record);
            }
        }

        private bool TryLocate(string key, out InventoryItemView? item, out InventoryView? inventory)
        {
            item = CharacterInventory?.Find(key);
            inventory = item != null ? CharacterInventory : null;
            if (item == null)
            {
                item = GroundInventory?.Find(key);
                inventory = item != null ? GroundInventory : null;
            }

            if (item == null) Banner.Show(OdyBannerKind.Error, "Select an item first.");
            return item != null;
        }

        private bool RequireManager(string action)
        {
            if (CanManage) return true;
            Banner.Show(OdyBannerKind.Warning, action + " is MainGM-only in the backend. Ask your MainGM.");
            return false;
        }

        private Result<T> Finish<T>(string success, string action, Result<T> result)
        {
            if (result.IsFailure) return Fail<T>(action, result.Error);
            Reload(success);
            return result;
        }

        private Result<T> Fail<T>(string action, Error error)
        {
            FailPlain(action, error);
            return Result<T>.Failure(error);
        }

        private Result FailPlain(string action, Error error)
        {
            // Stale revisions: reload so the next attempt uses the server's state.
            if (error.SafeReasonCode.Equals(SafeReasonCode.StateChanged)) Refresh();
            Banner.ShowError(action, error);
            return Result.Failure(error);
        }

        private void Reload(string message)
        {
            Refresh();
            Banner.Show(OdyBannerKind.Success, message);
        }

        // ---- rendering ------------------------------------------------------------------------------

        private void Render()
        {
            if (_body == null) return;
            _body.Clear();

            VisualElement characterSection = OdyUi.Section(Character == null ? "Character" : Character.DisplayName, "inventory-character-section");
            if (Character == null) characterSection.Add(OdyUi.EmptyState("Open a character in the Character panel to see their inventory.", "inventory-no-character"));
            else RenderInventory(characterSection, CharacterInventory, InventoryOwnerChoice.Character);
            _body.Add(characterSection);

            VisualElement groundSection = OdyUi.Section("Scene ground", "inventory-ground-section");
            RenderInventory(groundSection, GroundInventory, InventoryOwnerChoice.SceneGround);
            _body.Add(groundSection);

            InventoryItemView? selected = SelectedItemKey == null ? null : FindItem(SelectedItemKey);
            if (selected != null && CanManage) _body.Add(BuildActions(selected));
            if (CanManage && (CharacterInventory != null || GroundInventory != null)) _body.Add(BuildCreate());
        }

        private void RenderInventory(VisualElement section, InventoryView? view, InventoryOwnerChoice owner)
        {
            if (view == null)
            {
                section.Add(OdyUi.EmptyState(CanManage ? "No inventory yet." : "No inventory yet -- the MainGM creates it.", "inventory-missing-" + owner.ToString().ToLowerInvariant()));
                if (CanManage) section.Add(OdyUi.ButtonRow(OdyUi.Button("Create inventory", () => CreateInventory(owner), OdyButtonVariant.Primary, "inventory-create-" + owner.ToString().ToLowerInvariant(), small: true)));
                return;
            }

            if (view.Items.Count == 0) section.Add(OdyUi.EmptyState("Empty.", "inventory-empty-" + owner.ToString().ToLowerInvariant()));
            foreach (KeyValuePair<string, List<InventoryItemView>> zone in view.Zones)
            {
                Label zoneTitle = OdyUi.Text(zone.Key, OdyClasses.TextLabel);
                zoneTitle.name = "inventory-zone";
                section.Add(zoneTitle);
                var list = new VisualElement();
                list.AddToClassList(OdyClasses.List);
                foreach (InventoryItemView item in zone.Value)
                {
                    string key = item.Key;
                    var row = new Button(() => SelectItem(key)) { name = "inventory-item-" + key, text = string.Empty };
                    row.AddToClassList(OdyClasses.ListItem);
                    if (SelectedItemKey == key) row.AddToClassList(OdyClasses.ListItemSelected);
                    var main = new VisualElement();
                    main.AddToClassList(OdyClasses.ListItemMain);
                    main.Add(OdyUi.Text(item.Name + (item.IsStack ? " × " + item.Quantity : string.Empty), OdyClasses.ListItemTitle));
                    main.Add(OdyUi.Text(item.Summary, OdyClasses.ListItemMeta));
                    row.Add(main);
                    if (item.IsEquipped) row.Add(OdyUi.Badge("Equipped", OdyStatusKind.Success));
                    list.Add(row);
                }

                section.Add(list);
            }
        }

        private VisualElement BuildActions(InventoryItemView item)
        {
            VisualElement card = OdyUi.Card(item.Name, out VisualElement body, "inventory-actions");
            body.Add(OdyUi.Text(item.ZoneLabel + " · " + item.Summary, OdyClasses.TextCaption));

            if (item.IsEquipped)
            {
                TextField container = OdyUi.TextField("Unequip into container", InventoryLocator.DefaultContainerKey, "inventory-unequip-container");
                body.Add(container);
                body.Add(OdyUi.ButtonRow(OdyUi.Button("Unequip", () => Unequip(item.Key, container.value), OdyButtonVariant.Primary, "inventory-unequip", small: true)));
                return card;
            }

            // Move
            var move = OdyUi.Section("Move");
            var moveRow = new VisualElement();
            moveRow.AddToClassList(OdyClasses.FormRow);
            var destinations = new List<string> { InventoryOwnerChoice.Character.ToString(), InventoryOwnerChoice.SceneGround.ToString() };
            DropdownField destination = OdyUi.Dropdown("To", destinations, 0, "inventory-move-destination");
            TextField moveContainer = OdyUi.TextField("Container", InventoryLocator.DefaultContainerKey, "inventory-move-container");
            moveRow.Add(destination);
            moveRow.Add(moveContainer);
            move.Add(moveRow);
            move.Add(OdyUi.ButtonRow(OdyUi.Button("Move", () => Move(item.Key, EnumChoices.TryParse(destination.value, out InventoryOwnerChoice choice) ? choice : InventoryOwnerChoice.Character, moveContainer.value), OdyButtonVariant.Secondary, "inventory-move", small: true)));
            body.Add(move);

            if (item.IsStack)
            {
                var stack = OdyUi.Section("Stack");
                IntegerField quantity = OdyUi.IntegerField("Split off", 1, "inventory-split-quantity");
                stack.Add(quantity);
                var siblings = new List<string>();
                InventoryView? home = CharacterInventory?.Find(item.Key) != null ? CharacterInventory : GroundInventory;
                if (home != null)
                {
                    foreach (InventoryItemView other in home.Items)
                    {
                        if (other.IsStack && other.Key != item.Key && other.DefinitionRef.DefinitionId.Equals(item.DefinitionRef.DefinitionId)) siblings.Add(other.Key);
                    }
                }

                stack.Add(OdyUi.ButtonRow(OdyUi.Button("Split", () => Split(item.Key, quantity.value), OdyButtonVariant.Secondary, "inventory-split", small: true)));
                if (siblings.Count > 0)
                {
                    DropdownField into = OdyUi.Dropdown("Merge into", siblings, 0, "inventory-merge-target");
                    stack.Add(into);
                    stack.Add(OdyUi.ButtonRow(OdyUi.Button("Merge", () => Merge(item.Key, into.value), OdyButtonVariant.Secondary, "inventory-merge", small: true)));
                }

                body.Add(stack);
            }

            if (CharacterInventory?.Find(item.Key) != null)
            {
                var equip = OdyUi.Section("Equip");
                string slotHint = string.Empty;
                IReadOnlyList<string> partHints = Array.Empty<string>();
                bool hinted = InventoryLocator.TryGetArmorHint(item.Snapshot, out slotHint, out partHints);
                TextField slot = OdyUi.TextField("Slot", hinted ? slotHint : "main_hand", "inventory-equip-slot");
                equip.Add(slot);
                var chosen = new List<string>(partHints);
                if (Character?.Anatomy != null)
                {
                    foreach (BodyPart part in Character.Anatomy.BodyParts)
                    {
                        string id = part.BodyPartId.ToString();
                        Toggle toggle = OdyUi.Toggle(part.Name + " (" + id + ")", chosen.Contains(id), "inventory-equip-part-" + id);
                        toggle.RegisterValueChangedCallback(evt => { if (evt.newValue) { if (!chosen.Contains(id)) chosen.Add(id); } else chosen.Remove(id); });
                        equip.Add(toggle);
                    }
                }
                else
                {
                    equip.Add(OdyUi.Text("The character has no anatomy yet, so no body parts can be chosen.", OdyClasses.FieldHint));
                }

                if (hinted) equip.Add(OdyUi.Text("Pre-filled from the armor's slot and covered parts -- a hint only; the backend does not check slot compatibility.", OdyClasses.FieldHint));
                equip.Add(OdyUi.ButtonRow(OdyUi.Button("Equip", () => Equip(item.Key, slot.value, chosen), OdyButtonVariant.Primary, "inventory-equip", small: true)));
                body.Add(equip);
            }

            return card;
        }

        private VisualElement BuildCreate()
        {
            VisualElement section = OdyUi.Section("Create an item", "inventory-create-item");
            if (_creatable.Count == 0)
            {
                section.Add(OdyUi.Text("Publish an Item, Weapon, Armor or Ammo definition in the Catalog first.", OdyClasses.FieldHint));
                return section;
            }

            var choices = new List<string>();
            foreach (ContentDefinitionRecord record in _creatable) choices.Add(record.Name + " (" + record.DefinitionType + " v" + record.Version + ")");
            DropdownField definition = OdyUi.Dropdown("Definition", choices, 0, "inventory-create-definition");
            var row = new VisualElement();
            row.AddToClassList(OdyClasses.FormRow);
            IntegerField quantity = OdyUi.IntegerField("Quantity (stacks)", 1, "inventory-create-quantity");
            TextField container = OdyUi.TextField("Container", InventoryLocator.DefaultContainerKey, "inventory-create-container");
            var owners = new List<string>();
            if (CharacterInventory != null) owners.Add(InventoryOwnerChoice.Character.ToString());
            if (GroundInventory != null) owners.Add(InventoryOwnerChoice.SceneGround.ToString());
            DropdownField owner = OdyUi.Dropdown("Into", owners, 0, "inventory-create-owner");
            row.Add(quantity);
            row.Add(container);
            row.Add(owner);
            section.Add(definition);
            section.Add(row);
            section.Add(OdyUi.Text("Stackable Items and Ammo become a stack with the quantity; Items, Weapons and Armor otherwise become one instance.", OdyClasses.FieldHint));
            section.Add(OdyUi.ButtonRow(OdyUi.Button("Create", () =>
            {
                int index = definition.index;
                if (index < 0 || index >= _creatable.Count) return;
                CreateItem(_creatable[index].ContentDefinitionId, quantity.value, container.value, EnumChoices.TryParse(owner.value, out InventoryOwnerChoice choice) ? choice : InventoryOwnerChoice.Character);
            }, OdyButtonVariant.Primary, "inventory-create-button", small: true)));
            return section;
        }
    }

    internal static class InventoryUiErrors
    {
        private static readonly CorrelationId Placeholder = CorrelationId.Parse("corr_00000000000000000000000000000000");

        internal static Error Denied() => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Authorization, SafeReasonCode.PermissionDenied, UserMessageKey.Parse("errors.inventory.move_denied"), RetryDirective.DoNotRetry, Placeholder);
    }
}
