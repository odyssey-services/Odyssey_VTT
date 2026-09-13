using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Odyssey.Application.Commands;
using Odyssey.Application.Content;
using Odyssey.Application.Inventory;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Content;
using Odyssey.Domain.Effects;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Effects
{
    /// <summary>
    /// Host-only reactions invoked after successful EquipmentService results.
    /// These are sequential repository operations, not event subscriptions or
    /// an atomic equipment command. The host must finish/retry a reaction with
    /// identical inputs before processing the next equipment operation.
    /// The reaction CommandId is distinct from the equipment command's id.
    /// Authorization and target selection belong to the invoking host; this
    /// service adds no MainGM restriction. CharacterAbility suspension has no
    /// existing repository API and remains ODY-S05-505-F01.
    /// </summary>
    public static class ItemEffectLifecycleService
    {
        /// <summary>
        /// Pass only a successful Equip result and its already validated target.
        /// ForDuration requires the host's Ruleset-derived expiry resolver; no
        /// duration unit or targeting language is inferred here. Instant creates
        /// no persistent row; its immediate mechanics are outside this service.
        /// </summary>
        public static Result<IReadOnlyList<ActiveEffectRecord>> OnItemEquipped(
            IInventoryRepository inventory, IContentCatalogRepository catalog, IActiveEffectRepository effects,
            CampaignHandle campaign, EquippedEntryRecord equipped, ActiveEffectTargetRef target,
            CommandId commandId, CorrelationId correlationId,
            Func<EffectMechanicsSnapshot, UtcInstant, Result<UtcInstant>>? resolveExpiry = null)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (effects == null) throw new ArgumentNullException(nameof(effects));
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (equipped == null) throw new ArgumentNullException(nameof(equipped));
            if (!target.IsValid) throw new ArgumentException("Target is required.", nameof(target));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (!equipped.CampaignId.Equals(campaign.CampaignId)) return Failed(correlationId);

            InventoryItemRef item = equipped.Entry.ItemRef;
            var currentEquipment = inventory.GetEquippedEntry(campaign, item, correlationId);
            if (currentEquipment.IsFailure) return Result<IReadOnlyList<ActiveEffectRecord>>.Failure(currentEquipment.Error);
            if (!currentEquipment.Value.Entry.InventoryId.Equals(equipped.Entry.InventoryId) ||
                !currentEquipment.Value.Entry.EquippedAt.Equals(equipped.Entry.EquippedAt) ||
                !currentEquipment.Value.Entry.EquippedByUserId.Equals(equipped.Entry.EquippedByUserId)) return Failed(correlationId);

            var snapshot = ReadItemSnapshot(inventory, campaign, item, correlationId);
            if (snapshot.IsFailure) return Result<IReadOnlyList<ActiveEffectRecord>>.Failure(snapshot.Error);
            var definition = DecodeItem(snapshot.Value, correlationId);
            if (definition.IsFailure) return Result<IReadOnlyList<ActiveEffectRecord>>.Failure(definition.Error);
            var sourced = ListSource(effects, campaign, item, correlationId);
            if (sourced.IsFailure) return sourced;

            // Prepare every new snapshot before any mutation. Catalog errors cannot
            // leave half of a newly equipped item's built-in definitions applied.
            var create = new List<ActiveEffectRecord>();
            var retained = new List<ActiveEffectRecord>();
            for (int index = 0; index < definition.Value.BuiltInEffectRefs.Count; index++)
            {
                ContentDefinitionRef effectRef = definition.Value.BuiltInEffectRefs[index];
                ActiveEffectId id = ActiveEffectId.Parse("aeff_" + StableId(commandId, "create/" + index.ToString(CultureInfo.InvariantCulture)));
                // Creation replay in the existing repository is keyed by command.
                // Reject reuse for another item before that replay can return its row.
                var prior = effects.GetActiveEffect(campaign, id, correlationId);
                if (prior.IsFailure && prior.Error.Code != ErrorCodes.PersistenceActiveEffectNotFound)
                    return Result<IReadOnlyList<ActiveEffectRecord>>.Failure(prior.Error);
                if (prior.IsSuccess && !prior.Value.Effect.SourceRef.Equals(ActiveEffectSourceRef.ForEquippedItem(item)))
                    return Failed(correlationId);
                ActiveEffectRecord? existing = null;
                foreach (ActiveEffectRecord record in sourced.Value)
                {
                    if (record.Effect.ActiveEffectId.Equals(id))
                    {
                        if (!record.Effect.EffectDefinitionRef.Equals(effectRef) || !record.Effect.TargetRef.Equals(target) ||
                            !record.Effect.AppliedByUserId.Equals(equipped.Entry.EquippedByUserId) ||
                            !record.Effect.AppliedAt.Equals(equipped.Entry.EquippedAt)) return Failed(correlationId);
                        existing = record;
                        break;
                    }
                    if (record.Effect.EffectDefinitionRef.Equals(effectRef) && record.Effect.TargetRef.Equals(target) &&
                        (record.Effect.Status == ActiveEffectStatus.Active || record.Effect.Status == ActiveEffectStatus.Suspended))
                    {
                        var pinned = TypedDefinitionCodec.DecodeEffect(record.Effect.EffectMechanicsSnapshot.ContentType, record.Effect.EffectMechanicsSnapshot.Payload, correlationId);
                        if (pinned.IsFailure) return Result<IReadOnlyList<ActiveEffectRecord>>.Failure(pinned.Error);
                        if (pinned.Value.DurationType == EffectDurationType.WhileItemEquipped) existing = record;
                    }
                }
                if (existing != null)
                {
                    retained.Add(existing);
                    continue;
                }

                var fetched = catalog.GetContentDefinition(campaign, effectRef.DefinitionId, correlationId);
                if (fetched.IsFailure) return Result<IReadOnlyList<ActiveEffectRecord>>.Failure(fetched.Error);
                var content = fetched.Value;
                if (content.Version != effectRef.Version || content.Status != ContentDefinitionStatus.Published ||
                    content.DefinitionType != ContentDefinitionType.Effect) return Failed(correlationId);
                var typed = TypedDefinitionCodec.DecodeEffect(content.DefinitionType, content.PropertiesJson, correlationId);
                if (typed.IsFailure) return Result<IReadOnlyList<ActiveEffectRecord>>.Failure(typed.Error);
                if (typed.Value.DurationType == EffectDurationType.Instant) continue;
                if (IsCombatDuration(typed.Value.DurationType)) return Failed(correlationId);
                var effectSnapshot = new EffectMechanicsSnapshot(effectRef, content.Version, content.DefinitionType, content.PropertiesJson);
                UtcInstant? expiresAt = null;
                if (typed.Value.DurationType == EffectDurationType.ForDuration)
                {
                    if (resolveExpiry == null) return Failed(correlationId);
                    var expiry = resolveExpiry(effectSnapshot, equipped.Entry.EquippedAt);
                    if (expiry.IsFailure) return Result<IReadOnlyList<ActiveEffectRecord>>.Failure(expiry.Error);
                    if (expiry.Value.CompareTo(equipped.Entry.EquippedAt) <= 0) return Failed(correlationId);
                    expiresAt = expiry.Value;
                }
                // 503 currently provides decisions, not atomic stacking writes.
                // Do not silently bypass a policy that requires those writes.
                if (typed.Value.StackPolicy != EffectStackPolicy.IndependentInstances)
                {
                    var targets = effects.ListActiveEffectsByTarget(campaign, campaign.CampaignId, target, correlationId);
                    if (targets.IsFailure) return Result<IReadOnlyList<ActiveEffectRecord>>.Failure(targets.Error);
                    foreach (var other in targets.Value)
                        if (other.Effect.EffectDefinitionRef.Equals(effectRef) &&
                            (other.Effect.Status == ActiveEffectStatus.Active || other.Effect.Status == ActiveEffectStatus.Suspended)) return Failed(correlationId);
                    foreach (var other in create)
                        if (other.Effect.EffectDefinitionRef.Equals(effectRef)) return Failed(correlationId);
                }
                create.Add(new ActiveEffectRecord(campaign.CampaignId, new ActiveEffect(id, effectRef, effectSnapshot,
                    ActiveEffectSourceRef.ForEquippedItem(item), target, ActiveEffectStatus.Active, 1,
                    equipped.Entry.EquippedByUserId, equipped.Entry.EquippedAt, expiresAt, 1)));
            }

            var resumed = TransitionSource(effects, campaign, sourced.Value, true, equipped.Entry.EquippedByUserId, commandId, correlationId);
            if (resumed.IsFailure) return resumed;
            var result = new List<ActiveEffectRecord>(resumed.Value);
            foreach (var record in retained)
            {
                bool returned = false;
                foreach (var changed in result) if (changed.Effect.ActiveEffectId.Equals(record.Effect.ActiveEffectId)) returned = true;
                if (!returned) result.Add(record);
            }
            foreach (var record in create)
            {
                var saved = effects.CreateActiveEffect(campaign, record,
                    CommandId.Parse("cmd_" + StableId(commandId, record.Effect.ActiveEffectId.ToString())), correlationId);
                if (saved.IsFailure) return Result<IReadOnlyList<ActiveEffectRecord>>.Failure(saved.Error);
                result.Add(saved.Value);
            }
            return Result<IReadOnlyList<ActiveEffectRecord>>.Success(result.AsReadOnly());
        }

        /// <summary>Invoke only after a successful Unequip result, with the source from the original request.</summary>
        public static Result<IReadOnlyList<ActiveEffectRecord>> OnItemUnequipped(
            IActiveEffectRepository effects, CampaignHandle campaign, InventoryItemRef item,
            UserId actorUserId, CommandId commandId, CorrelationId correlationId)
        {
            if (effects == null) throw new ArgumentNullException(nameof(effects));
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!item.IsValid) throw new ArgumentException("Item is required.", nameof(item));
            if (!actorUserId.IsValid) throw new ArgumentException("Actor is required.", nameof(actorUserId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            var sourced = ListSource(effects, campaign, item, correlationId);
            return sourced.IsFailure ? sourced : TransitionSource(effects, campaign, sourced.Value, false, actorUserId, commandId, correlationId);
        }

        private static Result<IReadOnlyList<ActiveEffectRecord>> TransitionSource(IActiveEffectRepository effects, CampaignHandle campaign,
            IReadOnlyList<ActiveEffectRecord> records, bool equipped, UserId actor, CommandId command, CorrelationId correlation)
        {
            var transitions = new List<ActiveEffectRecord>();
            foreach (var record in records)
            {
                if (record.Effect.Status != (equipped ? ActiveEffectStatus.Suspended : ActiveEffectStatus.Active)) continue;
                var snapshot = record.Effect.EffectMechanicsSnapshot;
                var definition = TypedDefinitionCodec.DecodeEffect(snapshot.ContentType, snapshot.Payload, correlation);
                if (definition.IsFailure) return Result<IReadOnlyList<ActiveEffectRecord>>.Failure(definition.Error);
                if (definition.Value.DurationType == EffectDurationType.WhileItemEquipped) transitions.Add(record);
            }
            var results = new List<ActiveEffectRecord>();
            foreach (var record in transitions)
            {
                var effect = record.Effect;
                var changed = effects.SetItemEffectEquipped(campaign, campaign.CampaignId, effect.ActiveEffectId, equipped,
                    effect.Revision, actor, CommandId.Parse("cmd_" + StableId(command, "transition/" + effect.ActiveEffectId)), correlation);
                if (changed.IsFailure) return Result<IReadOnlyList<ActiveEffectRecord>>.Failure(changed.Error);
                results.Add(new ActiveEffectRecord(campaign.CampaignId, new ActiveEffect(effect.ActiveEffectId, effect.EffectDefinitionRef,
                    effect.EffectMechanicsSnapshot, effect.SourceRef, effect.TargetRef, equipped ? ActiveEffectStatus.Active : ActiveEffectStatus.Suspended,
                    effect.StackCount, effect.AppliedByUserId, effect.AppliedAt, effect.ExpiresAt, changed.Value)));
            }
            return Result<IReadOnlyList<ActiveEffectRecord>>.Success(results.AsReadOnly());
        }

        private static Result<IReadOnlyList<ActiveEffectRecord>> ListSource(IActiveEffectRepository effects, CampaignHandle campaign, InventoryItemRef item, CorrelationId correlation)
        {
            var first = effects.ListActiveEffectsBySource(campaign, campaign.CampaignId, ActiveEffectSourceRef.ForItem(item), correlation);
            if (first.IsFailure) return first;
            var second = effects.ListActiveEffectsBySource(campaign, campaign.CampaignId, ActiveEffectSourceRef.ForEquippedItem(item), correlation);
            if (second.IsFailure) return second;
            var records = new List<ActiveEffectRecord>(first.Value);
            records.AddRange(second.Value);
            return Result<IReadOnlyList<ActiveEffectRecord>>.Success(records.AsReadOnly());
        }

        private static Result<ItemMechanicsSnapshot> ReadItemSnapshot(IInventoryRepository repository, CampaignHandle campaign, InventoryItemRef item, CorrelationId correlation)
        {
            if (item.Kind == InventoryItemRefKind.ItemInstance)
            {
                var result = repository.GetItemInstance(campaign, item.ItemInstanceId, correlation);
                return result.IsFailure ? Result<ItemMechanicsSnapshot>.Failure(result.Error) : Result<ItemMechanicsSnapshot>.Success(result.Value.MechanicsSnapshot);
            }
            var stack = repository.GetItemStack(campaign, item.ItemStackId, correlation);
            return stack.IsFailure ? Result<ItemMechanicsSnapshot>.Failure(stack.Error) : Result<ItemMechanicsSnapshot>.Success(stack.Value.MechanicsSnapshot);
        }

        private static Result<ItemDefinition> DecodeItem(ItemMechanicsSnapshot snapshot, CorrelationId correlation)
        {
            switch (snapshot.ContentType)
            {
                case ContentDefinitionType.Item: return TypedDefinitionCodec.DecodeItem(snapshot.ContentType, snapshot.Payload, correlation);
                case ContentDefinitionType.Weapon:
                    var weapon = TypedDefinitionCodec.DecodeWeapon(snapshot.ContentType, snapshot.Payload, correlation);
                    return weapon.IsFailure ? Result<ItemDefinition>.Failure(weapon.Error) : Result<ItemDefinition>.Success(weapon.Value.Item);
                case ContentDefinitionType.Armor:
                    var armor = TypedDefinitionCodec.DecodeArmor(snapshot.ContentType, snapshot.Payload, correlation);
                    return armor.IsFailure ? Result<ItemDefinition>.Failure(armor.Error) : Result<ItemDefinition>.Success(armor.Value.Item);
                case ContentDefinitionType.Ammo:
                    var ammo = TypedDefinitionCodec.DecodeAmmo(snapshot.ContentType, snapshot.Payload, correlation);
                    return ammo.IsFailure ? Result<ItemDefinition>.Failure(ammo.Error) : Result<ItemDefinition>.Success(ammo.Value.Item);
                default: return Result<ItemDefinition>.Failure(ItemEffectLifecycleFailures.Invalid(correlation));
            }
        }

        private static bool IsCombatDuration(EffectDurationType duration) => duration == EffectDurationType.ForRounds || duration == EffectDurationType.ForTurns ||
            duration == EffectDurationType.UntilSourceTurnStart || duration == EffectDurationType.UntilSourceTurnEnd ||
            duration == EffectDurationType.UntilTargetTurnStart || duration == EffectDurationType.UntilTargetTurnEnd;

        private static Result<IReadOnlyList<ActiveEffectRecord>> Failed(CorrelationId correlation) => Result<IReadOnlyList<ActiveEffectRecord>>.Failure(ItemEffectLifecycleFailures.Invalid(correlation));

        private static string StableId(CommandId command, string purpose)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes("ody-s05-505/v1/" + command + "/" + purpose));
            var text = new StringBuilder(32);
            for (int i = 0; i < 16; i++) text.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return text.ToString();
        }
    }

    public static class ItemEffectLifecycleFailures
    {
        public static Error Invalid(CorrelationId correlationId) => Error.Create(
            ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest, UserMessageKey.Parse("errors.application.validation_invalid"), RetryDirective.DoNotRetry, correlationId);
    }
}
