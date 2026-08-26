using System;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Dice;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// ODY-S03-007: durable counterpart to ODY-S03-005's in-memory-only
    /// <c>DiceRollStore</c> -- that task's own task contract section 3
    /// explicitly deferred persistence to this task. Mirrors
    /// <see cref="ISceneRepository"/>'s exact port/implementation split
    /// (Application-layer interface, <c>Odyssey.Persistence.Sqlite</c>
    /// implementation) for a different aggregate pair
    /// (<see cref="DiceRoll"/>/<see cref="GameLogEntryRecord"/> instead of
    /// Scene/Token).
    ///
    /// Scope narrowing (task contract section 3, mirroring
    /// ODY-S03-005's/ODY-S03-004's own precedent of narrowing a rich product
    /// entity to what one task needs): <see cref="GameLogEntryRecord"/> is
    /// 09_Dice_And_Game_Log section 23's <c>GameLogEntry</c> narrowed to a
    /// single entry kind this task actually produces -- "a dice roll was
    /// resolved" -- carrying a reference to its full <see cref="DiceRoll"/>
    /// rather than a separate <c>VisibilityAudience</c> field of its own.
    /// <c>ActionLogGroup</c>, disclosure-change commands, comments, tags, and
    /// full-text search (section 24, 26-27) are not implemented by this
    /// task (SLICE-03_IMPLEMENTATION_BACKLOG.md section 2.2/section 5).
    /// </summary>
    public interface IGameLogRepository
    {
        /// <summary>
        /// Persists a resolved <see cref="DiceRoll"/> and its describing
        /// <see cref="GameLogEntryRecord"/> in one ADR-012 section 5
        /// transaction (current-state rows + DomainEvent + AppliedCommands),
        /// exactly the same journal/projection commit boundary
        /// <c>SqliteSceneRepository</c> already uses for Scene/Token.
        /// </summary>
        Result<GameLogEntryRecord> SaveDiceRollEntry(CampaignHandle campaign, DiceRoll roll, CommandId commandId, CorrelationId correlationId);

        /// <summary>
        /// Reads every persisted <see cref="GameLogEntryRecord"/> for the
        /// campaign, ordered by <see cref="GameLogEntryRecord.AuthoritativeSequence"/>
        /// (ADR-012 section 4.1's <c>EventSequence</c> -- never by
        /// <see cref="GameLogEntryRecord.CreatedAt"/>). Returns the full,
        /// unredacted set; audience-aware filtering for a specific reader is
        /// the caller's responsibility (<c>GameLogReconnectService</c>), not
        /// this repository's -- the same "Persistence stores everything,
        /// Application decides visibility" split ADR-012 section 4.4
        /// already fixes for <c>DomainEvents</c>.
        /// </summary>
        Result<IReadOnlyList<GameLogEntryRecord>> ListGameLog(CampaignHandle campaign, CorrelationId correlationId);
    }

    /// <summary>
    /// 09_Dice_And_Game_Log section 23's <c>GameLogEntry</c>, narrowed to
    /// this task's single produced kind (see the interface doc comment).
    /// <see cref="Roll"/> carries the full re-hydrated <see cref="DiceRoll"/>
    /// (including its own <see cref="DiceRoll.Audience"/>) so that
    /// <c>DiceRollVisibilityPolicy</c> -- already accepted by ODY-S03-006 --
    /// can be reused unmodified for reconnect-time audience-aware reading,
    /// rather than this record duplicating a second, independently-drifting
    /// audience field.
    /// </summary>
    public sealed class GameLogEntryRecord
    {
        public GameLogEntryRecord(string logEntryId, CampaignId campaignId, CommandId rootCommandId, string entryType, string summaryPayload, UserId actorUserId, string diceRollId, UtcInstant createdAt, long authoritativeSequence, DiceRoll roll)
        {
            if (string.IsNullOrWhiteSpace(logEntryId)) throw new ArgumentException("LogEntryId is required.", nameof(logEntryId));
            if (!campaignId.IsValid) throw new ArgumentException("CampaignId is required.", nameof(campaignId));
            if (string.IsNullOrWhiteSpace(entryType)) throw new ArgumentException("EntryType is required.", nameof(entryType));
            if (string.IsNullOrWhiteSpace(summaryPayload)) throw new ArgumentException("SummaryPayload is required.", nameof(summaryPayload));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (string.IsNullOrWhiteSpace(diceRollId)) throw new ArgumentException("DiceRollId is required.", nameof(diceRollId));
            if (authoritativeSequence < 1) throw new ArgumentOutOfRangeException(nameof(authoritativeSequence));

            LogEntryId = logEntryId;
            CampaignId = campaignId;
            RootCommandId = rootCommandId;
            EntryType = entryType;
            SummaryPayload = summaryPayload;
            ActorUserId = actorUserId;
            DiceRollId = diceRollId;
            CreatedAt = createdAt;
            AuthoritativeSequence = authoritativeSequence;
            Roll = roll ?? throw new ArgumentNullException(nameof(roll));
        }

        public string LogEntryId { get; }
        public CampaignId CampaignId { get; }
        public CommandId RootCommandId { get; }
        public string EntryType { get; }
        public string SummaryPayload { get; }
        public UserId ActorUserId { get; }
        public string DiceRollId { get; }
        public UtcInstant CreatedAt { get; }

        /// <summary>ADR-012 section 4.1's <c>EventSequence</c> -- the only authoritative order, never <see cref="CreatedAt"/>.</summary>
        public long AuthoritativeSequence { get; }
        public DiceRoll Roll { get; }
    }
}
