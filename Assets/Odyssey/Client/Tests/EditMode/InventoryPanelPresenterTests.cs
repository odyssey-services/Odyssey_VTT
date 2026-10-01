using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Application.CharacterAdvancement;
using Odyssey.Application.Content;
using Odyssey.Application.Inventory;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Persistence.Sqlite;
using Odyssey.Unity.Client;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>ODY-S11-204: inventory and equipment UI.</summary>
    public sealed class InventoryPanelPresenterTests
    {
        [Test]
        public void NonMainGm_SeesWhyActionsAreUnavailable_AndCannotCreate()
        {
            using var fixture = InventoryFixture.Create(BaselineRole.Player);
            Assert.That(fixture.View.Q<Label>("inventory-role-notice-text").text, Does.Contain("Only the MainGM"), "explained, not silently hidden");
            Assert.That(fixture.Panel.CreateInventory(InventoryOwnerChoice.SceneGround).IsFailure, Is.True);
            Assert.That(fixture.Panel.Banner.Text, Does.Contain("MainGM-only"));
            Assert.That(fixture.View.Q<Button>("inventory-create-sceneground"), Is.Null);
        }

        [Test]
        public void MainGm_CreatesInventories_AndItemsFromPublishedDefinitions_GroupedByZone()
        {
            using var fixture = InventoryFixture.Create(BaselineRole.MainGM);
            fixture.OpenCharacterWithInventory();
            ContentDefinitionRecord sword = fixture.PublishWeapon("Sword");
            ContentDefinitionRecord arrows = fixture.PublishAmmo("Arrows");

            Result<string> swordItem = fixture.Panel.CreateItem(sword.ContentDefinitionId, 1, "backpack");
            Result<string> arrowStack = fixture.Panel.CreateItem(arrows.ContentDefinitionId, 20, "quiver");
            Assert.That(swordItem.IsSuccess && arrowStack.IsSuccess, Is.True);

            InventoryView inventory = fixture.Panel.CharacterInventory!;
            InventoryItemView swordView = inventory.Find(swordItem.Value)!;
            Assert.That(swordView.Name, Is.EqualTo("Sword"), "name from the live catalog");
            Assert.That(swordView.Summary, Does.StartWith("Weapon · 1d8"), "mechanics from the item's own snapshot");
            Assert.That(swordView.ZoneLabel, Is.EqualTo("Container: backpack"));
            InventoryItemView arrowView = inventory.Find(arrowStack.Value)!;
            Assert.That(arrowView.IsStack && arrowView.Quantity == 20, Is.True);
            Assert.That(inventory.Zones.Select(z => z.Key), Is.EquivalentTo(new[] { "Container: backpack", "Container: quiver" }));
            Assert.That(fixture.Panel.CreateItem(sword.ContentDefinitionId, 1, "Not A Key").IsFailure, Is.True, "container keys must be canonical");
        }

        [Test]
        public void Stacks_SplitAndMerge()
        {
            using var fixture = InventoryFixture.Create(BaselineRole.MainGM);
            fixture.OpenCharacterWithInventory();
            string stack = fixture.Panel.CreateItem(fixture.PublishAmmo("Bolts").ContentDefinitionId, 20, "quiver").Value;

            Assert.That(fixture.Panel.Split(stack, 20).IsFailure, Is.True, "cannot split off the whole stack");
            Result<string> split = fixture.Panel.Split(stack, 5);
            Assert.That(split.IsSuccess, Is.True);
            Assert.That(fixture.Panel.CharacterInventory!.Find(stack)!.Quantity, Is.EqualTo(15));
            Assert.That(fixture.Panel.CharacterInventory.Find(split.Value)!.Quantity, Is.EqualTo(5));

            Assert.That(fixture.Panel.Merge(split.Value, stack).IsSuccess, Is.True);
            Assert.That(fixture.Panel.CharacterInventory!.Items.Where(i => i.IsStack).Sum(i => i.Quantity), Is.EqualTo(20));
        }

        [Test]
        public void Move_BetweenCharacterAndSceneGround()
        {
            using var fixture = InventoryFixture.Create(BaselineRole.MainGM);
            fixture.OpenCharacterWithInventory();
            Assert.That(fixture.Panel.CreateInventory(InventoryOwnerChoice.SceneGround).IsSuccess, Is.True);
            string sword = fixture.Panel.CreateItem(fixture.PublishWeapon("Axe").ContentDefinitionId, 1, "backpack").Value;

            Assert.That(fixture.Panel.Move(sword, InventoryOwnerChoice.SceneGround, "ground").IsSuccess, Is.True);
            Assert.That(fixture.Panel.GroundInventory!.Find(sword), Is.Not.Null);
            Assert.That(fixture.Panel.CharacterInventory!.Find(sword), Is.Null);

            Assert.That(fixture.Panel.Move(sword, InventoryOwnerChoice.Character, "belt").IsSuccess, Is.True);
            Assert.That(fixture.Panel.CharacterInventory!.Find(sword)!.ZoneLabel, Is.EqualTo("Container: belt"));
        }

        [Test]
        public void Equip_ArmorHintPrefillsSlot_EquippedBlocksBodyPartRemoval_UnequipReturnsIt()
        {
            using var fixture = InventoryFixture.Create(BaselineRole.MainGM);
            CharacterRecord character = fixture.OpenCharacterWithInventory(withAnatomy: true);
            string helmet = fixture.Panel.CreateItem(fixture.PublishArmor("Helmet").ContentDefinitionId, 1, "backpack").Value;

            InventoryItemView helmetView = fixture.Panel.CharacterInventory!.Find(helmet)!;
            Assert.That(InventoryLocator.TryGetArmorHint(helmetView.Snapshot, out string slot, out IReadOnlyList<string> parts), Is.True);
            Assert.That(slot, Is.EqualTo("head"));
            fixture.Panel.SelectItem(helmet);
            Assert.That(fixture.View.Q<TextField>("inventory-equip-slot").value, Is.EqualTo("head"), "client hint pre-fills the slot");

            Assert.That(fixture.Panel.Equip(helmet, slot, parts).IsSuccess, Is.True);
            InventoryItemView equipped = fixture.Panel.CharacterInventory!.Find(helmet)!;
            Assert.That(equipped.IsEquipped, Is.True);
            Assert.That(equipped.ZoneLabel, Is.EqualTo("Equipped: head"));

            // Anatomy side: a body part wearing equipment cannot be removed -- explained by the character panel.
            using var characters = new CharacterPanelPresenter(fixture.Host.Context, fixture.Characters, fixture.Host.NewSceneRepository(), fixture.Host.ExportDirectory);
            characters.BuildView();
            characters.Open(character.CharacterId);
            Assert.That(characters.RemoveBodyPart(BodyPartId.Parse("Head")).IsFailure, Is.True);
            Assert.That(characters.Banner.Text, Does.Contain("equipment is worn"));

            fixture.Panel.SelectItem(helmet);
            Assert.That(fixture.Panel.Unequip(helmet, "backpack").IsSuccess, Is.True);
            Assert.That(fixture.Panel.CharacterInventory!.Find(helmet)!.IsEquipped, Is.False);
        }

        [Test]
        public void Equip_FreeSlotIsAcceptedAsTheBackendAllows()
        {
            using var fixture = InventoryFixture.Create(BaselineRole.MainGM);
            fixture.OpenCharacterWithInventory();
            string sword = fixture.Panel.CreateItem(fixture.PublishWeapon("Dagger").ContentDefinitionId, 1, "backpack").Value;
            Assert.That(fixture.Panel.Equip(sword, "off_hand", new List<string>()).IsSuccess, Is.True, "no client-side slot rules beyond the backend's");
        }

        [Test]
        public void StaleItemRevision_IsRejected_AndThePanelReloads()
        {
            using var fixture = InventoryFixture.Create(BaselineRole.MainGM);
            fixture.OpenCharacterWithInventory();
            fixture.Panel.CreateInventory(InventoryOwnerChoice.SceneGround);
            string sword = fixture.Panel.CreateItem(fixture.PublishWeapon("Mace").ContentDefinitionId, 1, "backpack").Value;
            InventoryView loaded = fixture.Panel.CharacterInventory!;
            InventoryItemView item = loaded.Find(sword)!;

            // Someone else moves the item first (same inventory, other container).
            Result<ItemInstanceRecord> external = InventoryMovementService.MoveItemInstance(fixture.Inventory, fixture.Host.CampaignRepository,
                new MoveItemInstanceRequest(fixture.Host.Campaign, item.Instance!.ItemInstanceId, item.Revision, loaded.Inventory.InventoryId, loaded.Inventory.Revision, loaded.Inventory.InventoryId, loaded.Inventory.Revision, "belt", fixture.Host.Selection.MainGmUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()));
            Assert.That(external.IsSuccess, Is.True);

            Result stale = fixture.Panel.Move(sword, InventoryOwnerChoice.SceneGround, "ground");
            Assert.That(stale.IsFailure, Is.True);
            Assert.That(stale.Error.SafeReasonCode, Is.EqualTo(SafeReasonCode.StateChanged));
            Assert.That(fixture.Panel.CharacterInventory!.Find(sword)!.ZoneLabel, Is.EqualTo("Container: belt"), "reloaded from the server");
            Assert.That(fixture.Panel.Move(sword, InventoryOwnerChoice.SceneGround, "ground").IsSuccess, Is.True, "the retry uses fresh revisions");
        }

        private sealed class InventoryFixture : System.IDisposable
        {
            private InventoryFixture(GameTestHost host)
            {
                Host = host;
                Inventory = host.NewInventoryRepository();
                Characters = host.NewCharacterRepository(Inventory);
                Catalog = host.NewCatalogRepository(Inventory);
                Panel = new InventoryPanelPresenter(host.Context, Inventory, Catalog, Characters);
                View = Panel.BuildView();
            }

            public GameTestHost Host { get; }
            public SqliteInventoryRepository Inventory { get; }
            public SqliteCharacterRepository Characters { get; }
            public SqliteContentCatalogRepository Catalog { get; }
            public InventoryPanelPresenter Panel { get; }
            public VisualElement View { get; }

            public static InventoryFixture Create(BaselineRole role) => new InventoryFixture(GameTestHost.Create(role));

            public CharacterRecord OpenCharacterWithInventory(bool withAnatomy = false)
            {
                Result<CharacterRecord> created = Characters.BindDraftToCampaign(
                    new BindDraftToCampaignRequest(Host.Campaign, CharacterKind.PlayerCharacter, "Nia", "humanoid", Host.Selection.PlayerUserId, CharacterCreationSeed.None(), null, null),
                    UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId());
                Assert.That(created.IsSuccess, Is.True);
                CharacterRecord character = created.Value;
                if (withAnatomy)
                {
                    Result<CharacterRecord> anatomy = CharacterAdvancementService.InitializeAnatomyWithDefaults(Characters, Host.Campaign, character.CharacterId, AnatomyProfileDefinitionId.Parse("humanoid"), Host.Selection.MainGmUserId, character.Revisions.CharacterAnatomyRevision, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId());
                    Assert.That(anatomy.IsSuccess, Is.True);
                    character = anatomy.Value;
                }

                Panel.SetCharacter(character);
                Assert.That(Panel.CreateInventory(InventoryOwnerChoice.Character).IsSuccess, Is.True);
                Assert.That(Panel.CharacterInventory, Is.Not.Null);
                return character;
            }

            public ContentDefinitionRecord PublishWeapon(string name) => Publish(ContentDefinitionType.Weapon, name, TypedDefinitionCodec.EncodeWeapon(new WeaponDefinition(PlainItem(), "1d8", 1, WeaponAttackMode.Melee, 1, AmmoRequirement.None, new List<string>())));

            public ContentDefinitionRecord PublishArmor(string name) => Publish(ContentDefinitionType.Armor, name, TypedDefinitionCodec.EncodeArmor(new ArmorDefinition(PlainItem(), "head", new List<BodyPartId> { BodyPartId.Parse("Head") }, 2)));

            public ContentDefinitionRecord PublishAmmo(string name) => Publish(ContentDefinitionType.Ammo, name, TypedDefinitionCodec.EncodeAmmo(new AmmoDefinition(
                new ItemDefinition(ItemCategory.Consumable, true, 50, 0, false, null, false, null, new List<ContentDefinitionRef>(), new List<ContentDefinitionRef>()),
                new List<string> { "arrow" }, null, new List<ContentDefinitionRef>())));

            public void Dispose()
            {
                Panel.Dispose();
                Host.Dispose();
            }

            private static ItemDefinition PlainItem() => new ItemDefinition(ItemCategory.Generic, false, null, 1, false, null, false, null, new List<ContentDefinitionRef>(), new List<ContentDefinitionRef>());

            private ContentDefinitionRecord Publish(ContentDefinitionType type, string name, string propertiesJson)
            {
                UserId gm = Host.Selection.MainGmUserId;
                Result<ContentDefinitionRecord> draft = ContentCatalogAuthoringService.CreateDraftDefinition(Catalog, Host.CampaignRepository,
                    new CreateDraftDefinitionRequest(Host.Campaign, type, name, null, gm, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId(), new[] { Host.Context.ActiveRulesetKey }, null, propertiesJson));
                Assert.That(draft.IsSuccess, Is.True, "draft " + name);
                Result<ContentDefinitionRecord> published = ContentCatalogLifecycleService.PublishDefinition(Catalog, Host.CampaignRepository,
                    new PublishDefinitionRequest(Host.Campaign, draft.Value.ContentDefinitionId, draft.Value.Revision, gm, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()));
                Assert.That(published.IsSuccess, Is.True, "publish " + name);
                Panel.Refresh();
                return published.Value;
            }
        }
    }
}
