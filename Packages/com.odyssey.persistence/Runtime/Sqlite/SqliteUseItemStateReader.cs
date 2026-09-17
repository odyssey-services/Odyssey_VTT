using System;
using System.Collections.Generic;
using Odyssey.Application.Content;
using Odyssey.Application.Inventory;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>
    /// ODY-S06-107: read-only composition of existing SQLite-backed authoritative stores for item use --
    /// the exact same shape `SqliteActivateAbilityStateReader` (`ODY-S06-106`) already established.
    ///
    /// Resolves the item's own `ItemDefinition` straight from its already-pinned `ItemMechanicsSnapshot`
    /// (via a `DecodeItem` switch mirroring -- not calling, since it is `private` in a forbidden-to-touch
    /// file -- `ItemEffectLifecycleService.DecodeItem`'s own established pattern), never from today's
    /// catalog entry -- unlike `AbilityDefinitionId`, an item's own snapshot already carries everything
    /// needed, so no `AbilityDefinitionId`-style catalog-bridge gap exists here at all. Each `EffectDefinitionRef`
    /// in `BuiltInEffectRefs` DOES need a real catalog round-trip (the item's own snapshot only pins the
    /// item's own shape, not its referenced effects'), mirroring `ItemEffectLifecycleService`'s own "resolve
    /// id through the catalog, do not trust a cache" pattern for exactly that reason. Filters to
    /// `EffectDurationType.Instant` only (the same criterion `ItemEffectLifecycleService`'s own line 113
    /// already applies for the opposite purpose -- skipping `Instant` during equip -- confirmed by direct
    /// code read before designing this filter, so both call sites agree on what "Instant" means without
    /// duplicating a diverging definition).
    /// </summary>
    public sealed class SqliteUseItemStateReader : IUseItemStateReader
    {
        private readonly ICharacterRepository _characters;
        private readonly IInventoryRepository _inventory;
        private readonly IContentCatalogRepository _catalog;
        private readonly IWallClock _clock;

        public SqliteUseItemStateReader(ICharacterRepository characters, IInventoryRepository inventory, IContentCatalogRepository catalog, IWallClock clock)
        {
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public Result<UseItemState> Read(CampaignHandle campaign, UseItemIntent intent, CorrelationId correlationId)
        {
            Result<CharacterRecord> actor = _characters.GetCharacter(campaign, intent.ActorId, correlationId);
            if (actor.IsFailure || actor.Value.CampaignId != campaign.CampaignId) return Result<UseItemState>.Failure(actor.IsFailure ? actor.Error : Rejected(correlationId));

            Result<(InventoryId InventoryId, InventoryOwnerRef OwnerRef, ItemMechanicsSnapshot Snapshot)> resolved = ResolveItem(campaign, intent.Item, correlationId);
            if (resolved.IsFailure) return Result<UseItemState>.Failure(resolved.Error);

            // ODY-S06-107: a real correctness check no ТЗ line spelled out explicitly but that this task's
            // own read-state seam is the natural place for -- an item can only be used by the character who
            // owns it (ADR-019's own general ownership principle, applied here since no other authority
            // exists over an item's own use).
            if (!resolved.Value.OwnerRef.Equals(InventoryOwnerRef.ForCharacter(intent.ActorId)))
            {
                return Result<UseItemState>.Failure(Rejected(correlationId));
            }

            Result<ItemDefinition> decodedItem = DecodeItem(resolved.Value.Snapshot, correlationId);
            if (decodedItem.IsFailure) return Result<UseItemState>.Failure(decodedItem.Error);

            // ODY-S06-107 section 2.3's own explicit MVP boundary: HasCharges describes only a definition-level
            // capability (confirmed by direct doc-comment read -- no runtime current-charge state exists
            // anywhere in this codebase); building real per-instance charge tracking is a separate, larger
            // task. An honest rejection here, not a crash, not a silent single-use treatment that would be
            // wrong for a multi-charge item.
            if (decodedItem.Value.HasCharges)
            {
                return Result<UseItemState>.Failure(HasChargesUnsupported(correlationId));
            }

            var envelopes = new List<MechanicsPrimitiveEnvelope>();
            foreach (ContentDefinitionRef effectRef in decodedItem.Value.BuiltInEffectRefs)
            {
                Result<ContentDefinitionRecord> fetched = _catalog.GetContentDefinition(campaign, effectRef.DefinitionId, correlationId);
                if (fetched.IsFailure) return Result<UseItemState>.Failure(fetched.Error);
                ContentDefinitionRecord content = fetched.Value;
                if (content.Version != effectRef.Version || content.Status != ContentDefinitionStatus.Published || content.DefinitionType != ContentDefinitionType.Effect)
                {
                    return Result<UseItemState>.Failure(Rejected(correlationId));
                }

                Result<EffectDefinition> decodedEffect = TypedDefinitionCodec.DecodeEffect(content.DefinitionType, content.PropertiesJson, correlationId);
                if (decodedEffect.IsFailure) return Result<UseItemState>.Failure(decodedEffect.Error);

                // The same Instant-only filter ItemEffectLifecycleService.cs:113 already applies (read, not
                // modified) -- a WhileItemEquipped/ForRounds/etc. built-in effect is simply not this
                // command's own concern (TC-USEITEM-004's own explicit requirement).
                if (decodedEffect.Value.DurationType != EffectDurationType.Instant) continue;

                MechanicsPrimitiveEnvelope primitives;
                if (decodedEffect.Value.MechanicsPayloadRef == null)
                {
                    primitives = new MechanicsPrimitiveEnvelope(1, Array.Empty<MechanicsPrimitive>());
                }
                else
                {
                    Result<MechanicsPrimitiveEnvelope> decodedPrimitives = MechanicsPayloadCodec.DecodePrimitives(decodedEffect.Value.MechanicsPayloadRef, correlationId);
                    if (decodedPrimitives.IsFailure) return Result<UseItemState>.Failure(decodedPrimitives.Error);
                    primitives = decodedPrimitives.Value;
                }

                envelopes.Add(primitives);
            }

            return Result<UseItemState>.Success(new UseItemState(actor.Value, resolved.Value.InventoryId, decodedItem.Value, envelopes));
        }

        public Result<bool> CanControlActor(CampaignHandle campaign, CharacterId actorId, UserId userId, CorrelationId correlationId)
        {
            Result<CharacterRecord> character = _characters.GetCharacter(campaign, actorId, correlationId);
            if (character.IsFailure) return Result<bool>.Failure(character.Error);
            return Result<bool>.Success(character.Value.CampaignId == campaign.CampaignId && CharacterOwnershipAssignment.IsAssignedCharacter(character.Value.Ownership, userId, _clock.GetUtcNow()));
        }

        private Result<(InventoryId InventoryId, InventoryOwnerRef OwnerRef, ItemMechanicsSnapshot Snapshot)> ResolveItem(CampaignHandle campaign, InventoryItemRef item, CorrelationId correlationId)
        {
            if (item.Kind == InventoryItemRefKind.ItemStack)
            {
                Result<ItemStackRecord> stack = _inventory.GetItemStack(campaign, item.ItemStackId, correlationId);
                return stack.IsFailure
                    ? Result<(InventoryId, InventoryOwnerRef, ItemMechanicsSnapshot)>.Failure(stack.Error)
                    : Result<(InventoryId, InventoryOwnerRef, ItemMechanicsSnapshot)>.Success((stack.Value.InventoryId, stack.Value.OwnerRef, stack.Value.MechanicsSnapshot));
            }

            Result<ItemInstanceRecord> instance = _inventory.GetItemInstance(campaign, item.ItemInstanceId, correlationId);
            return instance.IsFailure
                ? Result<(InventoryId, InventoryOwnerRef, ItemMechanicsSnapshot)>.Failure(instance.Error)
                : Result<(InventoryId, InventoryOwnerRef, ItemMechanicsSnapshot)>.Success((instance.Value.InventoryId, instance.Value.OwnerRef, instance.Value.MechanicsSnapshot));
        }

        /// <summary>Mirrors `ItemEffectLifecycleService.DecodeItem`'s own established switch verbatim (that method is `private` in a file this task must not modify, so the same logic is repeated here, not called) -- decodes straight from the item's own pinned `MechanicsSnapshot.Payload`, never the live catalog.</summary>
        private static Result<ItemDefinition> DecodeItem(ItemMechanicsSnapshot snapshot, CorrelationId correlationId)
        {
            switch (snapshot.ContentType)
            {
                case ContentDefinitionType.Item:
                    return TypedDefinitionCodec.DecodeItem(snapshot.ContentType, snapshot.Payload, correlationId);
                case ContentDefinitionType.Weapon:
                    Result<WeaponDefinition> weapon = TypedDefinitionCodec.DecodeWeapon(snapshot.ContentType, snapshot.Payload, correlationId);
                    return weapon.IsFailure ? Result<ItemDefinition>.Failure(weapon.Error) : Result<ItemDefinition>.Success(weapon.Value.Item);
                case ContentDefinitionType.Armor:
                    Result<ArmorDefinition> armor = TypedDefinitionCodec.DecodeArmor(snapshot.ContentType, snapshot.Payload, correlationId);
                    return armor.IsFailure ? Result<ItemDefinition>.Failure(armor.Error) : Result<ItemDefinition>.Success(armor.Value.Item);
                case ContentDefinitionType.Ammo:
                    Result<AmmoDefinition> ammo = TypedDefinitionCodec.DecodeAmmo(snapshot.ContentType, snapshot.Payload, correlationId);
                    return ammo.IsFailure ? Result<ItemDefinition>.Failure(ammo.Error) : Result<ItemDefinition>.Success(ammo.Value.Item);
                default:
                    return Result<ItemDefinition>.Failure(Rejected(correlationId));
            }
        }

        private static Error Rejected(CorrelationId correlationId) => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Precondition, SafeReasonCode.ActionNotAllowed, UserMessageKey.Parse("errors.item.invalid_state"), RetryDirective.DoNotRetry, correlationId);
        private static Error HasChargesUnsupported(CorrelationId correlationId) => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Validation, SafeReasonCode.InvalidRequest, UserMessageKey.Parse("errors.item.has_charges_unsupported"), RetryDirective.DoNotRetry, correlationId);
    }
}
