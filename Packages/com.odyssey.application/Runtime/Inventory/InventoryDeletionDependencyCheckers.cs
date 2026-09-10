using System;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Inventory
{
    /// <summary>
    /// ODY-S05-206: real <see cref="ICharacterDeletionDependencyChecker"/> --
    /// blocks <c>DeleteCharacterPermanently</c> while the character still owns
    /// any <c>ItemInstance</c>/<c>ItemStack</c> (contained, raw-equipped
    /// location, or scene-dropped but still owned). Callers opt in explicitly
    /// by passing this to <c>SqliteCharacterRepository</c>'s constructor; there
    /// is no implicit default wiring and no composition root in this codebase
    /// yet.
    /// </summary>
    public sealed class InventoryCharacterDeletionDependencyChecker : ICharacterDeletionDependencyChecker
    {
        private readonly IInventoryRepository _inventoryRepository;

        public InventoryCharacterDeletionDependencyChecker(IInventoryRepository inventoryRepository)
        {
            _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
        }

        public string? CheckBlockingDependency(CampaignHandle campaign, CharacterId characterId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));

            Result<bool> result = _inventoryRepository.HasAnyItemOwnedByCharacter(
                campaign, campaign.CampaignId, characterId, DependencyCheckCorrelation.Placeholder);

            if (result.IsFailure)
            {
                // ADR-025 section 5.2: fail closed. An unreadable Inventory
                // store must block an irreversible delete, never silently
                // allow it. Only the safe Error.Code is surfaced -- never raw
                // provider text or a local path.
                return "Inventory dependency check could not be completed (" + result.Error.Code + ").";
            }

            return result.Value
                ? "Character still owns one or more Inventory items (contained, equipped-location, or scene-dropped)."
                : null;
        }
    }

    /// <summary>
    /// ODY-S05-206: real <see cref="IContentDefinitionDeletionDependencyChecker"/> --
    /// blocks <c>DeleteDraftDefinition</c> when any runtime
    /// <c>ItemInstance</c>/<c>ItemStack</c> pins the target definition id as
    /// its <c>SourceItemDefinitionRef</c> (any pinned version). By construction
    /// no Draft can be runtime-referenced through today's public API; this is a
    /// forward-compatible gate for a future Archived-definition delete path.
    /// </summary>
    public sealed class InventoryContentDefinitionDependencyChecker : IContentDefinitionDeletionDependencyChecker
    {
        private readonly IInventoryRepository _inventoryRepository;

        public InventoryContentDefinitionDependencyChecker(IInventoryRepository inventoryRepository)
        {
            _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
        }

        public string? CheckBlockingDependency(CampaignHandle campaign, ContentDefinitionId definitionId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));

            Result<bool> result = _inventoryRepository.HasAnyRuntimeReferenceToDefinition(
                campaign, campaign.CampaignId, definitionId, DependencyCheckCorrelation.Placeholder);

            if (result.IsFailure)
            {
                // Fail closed: an unreadable Inventory store must block a
                // physical catalog delete, never silently allow it.
                return "Runtime reference dependency check could not be completed (" + result.Error.Code + ").";
            }

            return result.Value
                ? "One or more runtime items pin this content definition."
                : null;
        }
    }

    internal static class DependencyCheckCorrelation
    {
        // ODY-S05-206: the ICharacterDeletionDependencyChecker /
        // IContentDefinitionDeletionDependencyChecker method signatures carry
        // no caller CorrelationId, so an internal read needs a placeholder --
        // the same convention SerializationFailures / PersistenceFailures use
        // for codec-level calls with no caller correlation.
        internal static readonly CorrelationId Placeholder = CorrelationId.Parse("corr_00000000000000000000000000000000");
    }
}
