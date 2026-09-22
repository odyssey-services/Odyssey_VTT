using System;
using System.Collections.Generic;
using Odyssey.Application.Checks;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Checks;
using Odyssey.Domain.Identity;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>
    /// ODY-S07-102: the sole `ICheckStateReader` implementation -- mirrors `SqliteAttackStateReader.Read`'s
    /// own `State(CharacterRecord)` helper exactly: one `GetCharacter` call, then a plain `foreach` copy of
    /// `record.Attributes`/`record.Skills` into two dictionaries, no second database read.
    /// </summary>
    public sealed class SqliteCheckStateReader : ICheckStateReader
    {
        private readonly ICharacterRepository _characters;

        public SqliteCheckStateReader(ICharacterRepository characters)
        {
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
        }

        public Result<CheckParticipantState> Read(CampaignHandle campaign, CharacterId actorId, CorrelationId correlationId)
        {
            Result<CharacterRecord> character = _characters.GetCharacter(campaign, actorId, correlationId);
            if (character.IsFailure)
            {
                return Result<CheckParticipantState>.Failure(character.Error);
            }

            return Result<CheckParticipantState>.Success(State(character.Value));
        }

        private static CheckParticipantState State(CharacterRecord record)
        {
            var attributeValues = new Dictionary<AttributeDefinitionId, long>(record.Attributes.Count);
            foreach (AttributeValue attribute in record.Attributes) attributeValues[attribute.AttributeDefinitionId] = attribute.EffectiveValue;

            var skillValues = new Dictionary<SkillDefinitionId, long>(record.Skills.Count);
            foreach (CharacterSkill skill in record.Skills) skillValues[skill.SkillDefinitionId] = skill.EffectiveLevel;

            return new CheckParticipantState(record.CharacterId, attributeValues, skillValues);
        }
    }
}
