using System.Collections.Generic;
using Odyssey.Application.Audience;
using Odyssey.Application.Dice;
using Odyssey.Application.Networking.Session;
using Odyssey.Application.Persistence;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.GameLog
{
    /// <summary>
    /// ODY-S03-007: restores a persisted campaign's game log for one
    /// participant after the campaign database is reopened -- a process
    /// restart or a fresh <c>SqliteGameLogRepository</c> connection to the
    /// same <c>campaign.db</c>, NOT the networked <c>ADR-017</c> protocol
    /// (<c>ProjectionSnapshot</c>/<c>ProjectionDeltaBatch</c> over
    /// <c>ISessionTransport</c>, ODY-S02-004/012). No real network exists in
    /// this revision; this class never touches <c>Odyssey.Networking</c> or
    /// <c>ReconnectContracts.cs</c>'s in-memory <c>SessionDeltaBuffer</c>,
    /// which is scoped to one live session and cannot itself survive a
    /// process restart in the first place (task contract section 3).
    ///
    /// Applies ADR-017 section 1 point 8's principle -- redaction is always
    /// computed by CURRENT, not saved, permissions -- outside its original
    /// networking context: <see cref="GetVisibleEntries"/> re-evaluates each
    /// already-persisted <see cref="GameLogEntryRecord"/> against the
    /// caller-supplied CURRENT role/group state, reusing ODY-S03-006's
    /// already-accepted <see cref="DiceRollVisibilityPolicy"/> unmodified --
    /// not a second, parallel visibility mechanism for the persisted log.
    /// </summary>
    public static class GameLogReconnectService
    {
        public static IReadOnlyList<GameLogEntryRecord> GetVisibleEntries(IReadOnlyList<GameLogEntryRecord> entries, UserId audienceUserId, BaselineRole role, ICampaignUserGroupDirectory groups)
        {
            var visible = new List<GameLogEntryRecord>();
            foreach (GameLogEntryRecord entry in entries)
            {
                if (DiceRollVisibilityPolicy.TryGetVisibleRoll(entry.Roll, audienceUserId, role, groups, out _))
                {
                    visible.Add(entry);
                }
            }

            return visible;
        }
    }
}
