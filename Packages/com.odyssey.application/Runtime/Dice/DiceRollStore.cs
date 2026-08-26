using System.Collections.Generic;

namespace Odyssey.Application.Dice
{
    /// <summary>
    /// ODY-S03-005: in-memory store of record for this task's <see cref="DiceRoll"/>/
    /// <see cref="RollOverride"/> entities. Deliberately not durable and not routed
    /// through SLICE-01's SQLite append-only journal -- unlike ODY-S03-004 (which
    /// extended the already-durable SqliteSceneRepository because durable board
    /// persistence was that task's own premise), no durable dice-roll store exists
    /// yet to extend, and building one is explicitly ODY-S03-007's job
    /// (SLICE-03_IMPLEMENTATION_BACKLOG.md section 5: "Implements GameLogEntry...
    /// persistence via ADR-012's... journal contract"). This mirrors ODY-S02-011's
    /// own justified use of a fresh in-memory store for a concept with no durable
    /// counterpart yet, not a reuse of that class itself.
    ///
    /// Still enforces the append-only *semantics* the eventual durable store must
    /// also honor: a roll's identity (<see cref="DiceRoll.RollId"/>) never
    /// disappears or gets replaced by a different roll's data -- only
    /// <see cref="DiceRoll.WithModifierEntries"/>/<see cref="DiceRoll.WithStatus"/>
    /// produce a new value under the same key; a reroll/cancel/override always adds
    /// a new entry, never deletes an existing one.
    /// </summary>
    public sealed class DiceRollStore
    {
        private readonly Dictionary<string, DiceRoll> _rolls = new Dictionary<string, DiceRoll>(System.StringComparer.Ordinal);
        private readonly Dictionary<string, List<RollOverride>> _overridesByRollId = new Dictionary<string, List<RollOverride>>(System.StringComparer.Ordinal);
        private long _nextId;

        public bool TryGet(string rollId, out DiceRoll roll) => _rolls.TryGetValue(rollId, out roll!);

        internal void Save(DiceRoll roll) => _rolls[roll.RollId] = roll;

        internal void AddOverride(RollOverride rollOverride)
        {
            if (!_overridesByRollId.TryGetValue(rollOverride.DiceRollId, out List<RollOverride>? list))
            {
                list = new List<RollOverride>();
                _overridesByRollId[rollOverride.DiceRollId] = list;
            }

            list.Add(rollOverride);
        }

        public IReadOnlyList<RollOverride> GetOverrides(string rollId) =>
            _overridesByRollId.TryGetValue(rollId, out List<RollOverride>? list) ? list : System.Array.Empty<RollOverride>();

        internal string NewId(string prefix) => prefix + "_" + (++_nextId).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
