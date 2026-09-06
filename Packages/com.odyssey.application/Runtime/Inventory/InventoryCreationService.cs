using System;
using Odyssey.Application.Commands;
using Odyssey.Application.Content;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Inventory
{
    /// <summary>
    /// ODY-S05-203: MainGM-only creation of runtime Inventory item records
    /// from already Published catalog definitions. This service copies a
    /// mechanics snapshot and delegates persistence/idempotency to
    /// <see cref="IInventoryRepository"/>; it does not move, split, merge,
    /// equip, use, execute, or migrate items.
    /// </summary>
    public static class InventoryCreationService
    {
        private const string EmptyRuntimeState = "{}";

        public static Result<ItemInstanceRecord> CreateItemInstanceFromDefinition(
            IContentCatalogRepository catalogRepository,
            IInventoryRepository inventoryRepository,
            IWallClock clock,
            CreateItemInstanceFromDefinitionRequest request)
        {
            if (catalogRepository == null) throw new ArgumentNullException(nameof(catalogRepository));
            if (inventoryRepository == null) throw new ArgumentNullException(nameof(inventoryRepository));
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            if (request == null) throw new ArgumentNullException(nameof(request));

            Result<ContentDefinitionRecord> prepared = PrepareDefinition(catalogRepository, request.Campaign, request.ContentDefinitionId, request.ActorIsMainGm, IsInstanceDefinitionType, request.CorrelationId);
            if (prepared.IsFailure)
            {
                return Result<ItemInstanceRecord>.Failure(prepared.Error);
            }

            ContentDefinitionRecord definition = prepared.Value;
            UtcInstant now = clock.GetUtcNow();
            ContentDefinitionRef sourceRef = new ContentDefinitionRef(definition.ContentDefinitionId, definition.Version);
            var record = new ItemInstanceRecord(
                request.ItemInstanceId,
                request.Campaign.CampaignId,
                request.InventoryId,
                request.OwnerRef,
                request.LocationRef,
                sourceRef,
                CopySnapshot(definition, sourceRef),
                EmptyRuntimeState,
                1,
                now,
                now);

            return inventoryRepository.CreateItemInstance(request.Campaign, record, request.CommandId, request.CorrelationId);
        }

        public static Result<ItemStackRecord> CreateItemStackFromDefinition(
            IContentCatalogRepository catalogRepository,
            IInventoryRepository inventoryRepository,
            IWallClock clock,
            CreateItemStackFromDefinitionRequest request)
        {
            if (catalogRepository == null) throw new ArgumentNullException(nameof(catalogRepository));
            if (inventoryRepository == null) throw new ArgumentNullException(nameof(inventoryRepository));
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            if (request == null) throw new ArgumentNullException(nameof(request));

            Result<ContentDefinitionRecord> prepared = PrepareDefinition(catalogRepository, request.Campaign, request.ContentDefinitionId, request.ActorIsMainGm, IsStackDefinitionType, request.CorrelationId);
            if (prepared.IsFailure)
            {
                return Result<ItemStackRecord>.Failure(prepared.Error);
            }

            ContentDefinitionRecord definition = prepared.Value;
            if (!StackQuantityAllowed(definition, request.Quantity, request.CorrelationId))
            {
                return Result<ItemStackRecord>.Failure(InventoryCreationFailures.DefinitionTypeUnsupported(request.CorrelationId));
            }

            UtcInstant now = clock.GetUtcNow();
            ContentDefinitionRef sourceRef = new ContentDefinitionRef(definition.ContentDefinitionId, definition.Version);
            var record = new ItemStackRecord(
                request.ItemStackId,
                request.Campaign.CampaignId,
                request.InventoryId,
                request.OwnerRef,
                request.LocationRef,
                sourceRef,
                CopySnapshot(definition, sourceRef),
                request.Quantity,
                EmptyRuntimeState,
                1,
                now,
                now);

            return inventoryRepository.CreateItemStack(request.Campaign, record, request.CommandId, request.CorrelationId);
        }

        private static Result<ContentDefinitionRecord> PrepareDefinition(
            IContentCatalogRepository repository,
            CampaignHandle campaign,
            ContentDefinitionId definitionId,
            bool actorIsMainGm,
            Func<ContentDefinitionType, bool> typeAllowed,
            CorrelationId correlationId)
        {
            if (!actorIsMainGm)
            {
                return Result<ContentDefinitionRecord>.Failure(InventoryCreationFailures.NotMainGm(correlationId));
            }

            Result<ContentDefinitionRecord> fetched = repository.GetContentDefinition(campaign, definitionId, correlationId);
            if (fetched.IsFailure)
            {
                return fetched;
            }

            ContentDefinitionRecord definition = fetched.Value;
            if (definition.Status != ContentDefinitionStatus.Published || definition.Version < 1)
            {
                return Result<ContentDefinitionRecord>.Failure(InventoryCreationFailures.DefinitionNotPublished(correlationId));
            }

            if (!typeAllowed(definition.DefinitionType))
            {
                return Result<ContentDefinitionRecord>.Failure(InventoryCreationFailures.DefinitionTypeUnsupported(correlationId));
            }

            Result<CatalogValidationResult> validation = CatalogValidationService.ValidateContentDefinition(repository, new ValidateContentDefinitionRequest(campaign, definitionId, correlationId));
            if (validation.IsFailure)
            {
                return Result<ContentDefinitionRecord>.Failure(validation.Error);
            }

            return validation.Value.IsValid
                ? Result<ContentDefinitionRecord>.Success(definition)
                : Result<ContentDefinitionRecord>.Failure(InventoryCreationFailures.DefinitionValidationFailed(correlationId));
        }

        private static bool IsInstanceDefinitionType(ContentDefinitionType type)
        {
            return type == ContentDefinitionType.Item ||
                   type == ContentDefinitionType.Weapon ||
                   type == ContentDefinitionType.Armor;
        }

        private static bool IsStackDefinitionType(ContentDefinitionType type)
        {
            return type == ContentDefinitionType.Item ||
                   type == ContentDefinitionType.Ammo;
        }

        private static bool StackQuantityAllowed(ContentDefinitionRecord definition, ItemStackQuantity quantity, CorrelationId correlationId)
        {
            Result<ItemDefinition> item = definition.DefinitionType == ContentDefinitionType.Item
                ? TypedDefinitionCodec.DecodeItem(definition.DefinitionType, definition.PropertiesJson, correlationId)
                : DecodeAmmoItem(definition, correlationId);
            if (item.IsFailure || !item.Value.IsStackable)
            {
                return false;
            }

            return !item.Value.MaxStackSize.HasValue || quantity.Value <= item.Value.MaxStackSize.Value;
        }

        private static Result<ItemDefinition> DecodeAmmoItem(ContentDefinitionRecord definition, CorrelationId correlationId)
        {
            Result<AmmoDefinition> ammo = TypedDefinitionCodec.DecodeAmmo(definition.DefinitionType, definition.PropertiesJson, correlationId);
            return ammo.IsSuccess
                ? Result<ItemDefinition>.Success(ammo.Value.Item)
                : Result<ItemDefinition>.Failure(ammo.Error);
        }

        private static ItemMechanicsSnapshot CopySnapshot(ContentDefinitionRecord definition, ContentDefinitionRef sourceRef)
        {
            return new ItemMechanicsSnapshot(sourceRef, definition.Version, definition.DefinitionType, definition.PropertiesJson);
        }
    }

    public sealed class CreateItemInstanceFromDefinitionRequest
    {
        public CreateItemInstanceFromDefinitionRequest(
            CampaignHandle campaign,
            ItemInstanceId itemInstanceId,
            InventoryId inventoryId,
            InventoryOwnerRef ownerRef,
            InventoryLocationRef locationRef,
            ContentDefinitionId contentDefinitionId,
            UserId actorUserId,
            bool actorIsMainGm,
            CommandId commandId,
            CorrelationId correlationId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!itemInstanceId.IsValid) throw new ArgumentException("ItemInstanceId is required.", nameof(itemInstanceId));
            if (!inventoryId.IsValid) throw new ArgumentException("InventoryId is required.", nameof(inventoryId));
            if (!ownerRef.IsValid) throw new ArgumentException("OwnerRef is required.", nameof(ownerRef));
            if (!locationRef.IsValid) throw new ArgumentException("LocationRef is required.", nameof(locationRef));
            if (!contentDefinitionId.IsValid) throw new ArgumentException("ContentDefinitionId is required.", nameof(contentDefinitionId));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            ItemInstanceId = itemInstanceId;
            InventoryId = inventoryId;
            OwnerRef = ownerRef;
            LocationRef = locationRef;
            ContentDefinitionId = contentDefinitionId;
            ActorUserId = actorUserId;
            ActorIsMainGm = actorIsMainGm;
            CommandId = commandId;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }
        public ItemInstanceId ItemInstanceId { get; }
        public InventoryId InventoryId { get; }
        public InventoryOwnerRef OwnerRef { get; }
        public InventoryLocationRef LocationRef { get; }
        public ContentDefinitionId ContentDefinitionId { get; }
        public UserId ActorUserId { get; }
        public bool ActorIsMainGm { get; }
        public CommandId CommandId { get; }
        public CorrelationId CorrelationId { get; }
    }

    public sealed class CreateItemStackFromDefinitionRequest
    {
        public CreateItemStackFromDefinitionRequest(
            CampaignHandle campaign,
            ItemStackId itemStackId,
            InventoryId inventoryId,
            InventoryOwnerRef ownerRef,
            InventoryLocationRef locationRef,
            ContentDefinitionId contentDefinitionId,
            ItemStackQuantity quantity,
            UserId actorUserId,
            bool actorIsMainGm,
            CommandId commandId,
            CorrelationId correlationId)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            if (!itemStackId.IsValid) throw new ArgumentException("ItemStackId is required.", nameof(itemStackId));
            if (!inventoryId.IsValid) throw new ArgumentException("InventoryId is required.", nameof(inventoryId));
            if (!ownerRef.IsValid) throw new ArgumentException("OwnerRef is required.", nameof(ownerRef));
            if (!locationRef.IsValid) throw new ArgumentException("LocationRef is required.", nameof(locationRef));
            if (!contentDefinitionId.IsValid) throw new ArgumentException("ContentDefinitionId is required.", nameof(contentDefinitionId));
            if (!quantity.IsValid) throw new ArgumentException("Quantity is required.", nameof(quantity));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));

            ItemStackId = itemStackId;
            InventoryId = inventoryId;
            OwnerRef = ownerRef;
            LocationRef = locationRef;
            ContentDefinitionId = contentDefinitionId;
            Quantity = quantity;
            ActorUserId = actorUserId;
            ActorIsMainGm = actorIsMainGm;
            CommandId = commandId;
            CorrelationId = correlationId;
        }

        public CampaignHandle Campaign { get; }
        public ItemStackId ItemStackId { get; }
        public InventoryId InventoryId { get; }
        public InventoryOwnerRef OwnerRef { get; }
        public InventoryLocationRef LocationRef { get; }
        public ContentDefinitionId ContentDefinitionId { get; }
        public ItemStackQuantity Quantity { get; }
        public UserId ActorUserId { get; }
        public bool ActorIsMainGm { get; }
        public CommandId CommandId { get; }
        public CorrelationId CorrelationId { get; }
    }

    public static class InventoryCreationFailures
    {
        public static Error NotMainGm(CorrelationId correlationId) => Error.Create(
            ErrorCodes.InventoryCreateDenied,
            ErrorCategory.Authorization,
            SafeReasonCode.PermissionDenied,
            UserMessageKey.Parse("errors.inventory.create_denied"),
            RetryDirective.DoNotRetry,
            correlationId);

        public static Error DefinitionNotPublished(CorrelationId correlationId) => Error.Create(
            ErrorCodes.InventoryCreateDefinitionNotPublished,
            ErrorCategory.Conflict,
            SafeReasonCode.ActionNotAllowed,
            UserMessageKey.Parse("errors.inventory.create_definition_not_published"),
            RetryDirective.DoNotRetry,
            correlationId);

        public static Error DefinitionTypeUnsupported(CorrelationId correlationId) => Error.Create(
            ErrorCodes.InventoryCreateDefinitionTypeUnsupported,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.inventory.create_definition_type_unsupported"),
            RetryDirective.DoNotRetry,
            correlationId);

        public static Error DefinitionValidationFailed(CorrelationId correlationId) => Error.Create(
            ErrorCodes.InventoryCreateDefinitionValidationFailed,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.inventory.create_definition_validation_failed"),
            RetryDirective.DoNotRetry,
            correlationId);
    }
}
