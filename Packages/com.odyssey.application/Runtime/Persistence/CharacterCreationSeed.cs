using System;
using System.Collections.Generic;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// ODY-S04-103: the already-resolved seed input to
    /// <see cref="ICharacterRepository.BindDraftToCampaign"/>. ADR-023 section
    /// 5.3 allows template application to happen at two different moments --
    /// at <c>CreateLocalCharacterDraft</c> for a Personal template, or at
    /// <c>BindDraftToCampaign</c> for a Campaign template. Unifying both into
    /// one already-resolved shape means <c>BindDraftToCampaign</c> itself
    /// never needs to know which storage a template came from, or whether a
    /// copy already happened earlier -- it only ever receives one of:
    /// <list type="bullet">
    /// <item><see cref="None"/> -- a blank Draft/Character, no template.</item>
    /// <item><see cref="AlreadyCopied"/> -- a Personal template's deep copy
    /// that already happened once, at <c>CreateLocalCharacterDraft</c> time;
    /// carried through unchanged, never re-copied.</item>
    /// <item><see cref="FromTemplate"/> -- a Campaign template resolved fresh
    /// at bind time; the deep copy happens right here, inside this factory,
    /// using the same <see cref="CharacterTemplateSeedCopier"/> the Personal
    /// path already used.</item>
    /// </list>
    /// </summary>
    public sealed class CharacterCreationSeed
    {
        private CharacterCreationSeed(CharacterTemplateId? templateId, long? templateVersionAtCopyTime, IReadOnlyList<CopiedCharacterSeedItem> items)
        {
            TemplateId = templateId;
            TemplateVersionAtCopyTime = templateVersionAtCopyTime;
            Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public static CharacterCreationSeed None() => new CharacterCreationSeed(null, null, Array.Empty<CopiedCharacterSeedItem>());

        public static CharacterCreationSeed AlreadyCopied(CharacterTemplateId templateId, long templateVersionAtCopyTime, IReadOnlyList<CopiedCharacterSeedItem> items)
        {
            if (!templateId.IsValid) throw new ArgumentException("TemplateId is required.", nameof(templateId));
            if (items == null) throw new ArgumentNullException(nameof(items));
            return new CharacterCreationSeed(templateId, templateVersionAtCopyTime, items);
        }

        public static CharacterCreationSeed FromTemplate(CharacterTemplateId templateId, long templateVersionAtCopyTime, CharacterTemplateSeed rawSeed, UtcInstant now)
        {
            if (!templateId.IsValid) throw new ArgumentException("TemplateId is required.", nameof(templateId));
            if (rawSeed == null) throw new ArgumentNullException(nameof(rawSeed));

            IReadOnlyList<CopiedCharacterSeedItem> copied = CharacterTemplateSeedCopier.CopyWithFreshIdentifiers(rawSeed, templateId, now);
            return new CharacterCreationSeed(templateId, templateVersionAtCopyTime, copied);
        }

        public CharacterTemplateId? TemplateId { get; }
        public long? TemplateVersionAtCopyTime { get; }
        public IReadOnlyList<CopiedCharacterSeedItem> Items { get; }
    }
}
