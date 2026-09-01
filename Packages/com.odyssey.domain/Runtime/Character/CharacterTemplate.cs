using System;
using System.Collections.Generic;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Domain.Character
{
    /// <summary>
    /// ODY-S04-103: ADR-023 section 5.2's <c>TemplateScope</c> -- the single
    /// distinguishing field between <c>PersonalCharacterTemplate</c> and
    /// <c>CampaignCharacterTemplate</c>. They are never two aggregate types
    /// (ADR-023 section 5.1); this enum is the entire difference.
    /// </summary>
    public enum TemplateScope
    {
        Personal = 1,
        Campaign = 2
    }

    /// <summary>
    /// ODY-S04-103: 10_Characters_And_Progression section 9.1 names a
    /// <c>Status</c> field on <c>CharacterTemplate</c> without enumerating its
    /// values. This task's own minimal decision: a template is either usable
    /// (<see cref="Active"/>) or no longer offered for new Drafts/Characters
    /// (<see cref="Archived"/>, via <c>ArchiveCharacterTemplate</c>) -- the
    /// smallest set that makes the named command meaningful.
    /// </summary>
    public enum CharacterTemplateStatus
    {
        Active = 1,
        Archived = 2
    }

    /// <summary>
    /// ODY-S04-103: one generic nested seed entry inside a
    /// <c>CharacterTemplate</c>'s seed data. Product section 9.1 names several
    /// concrete seed categories (<c>AttributeSeeds</c>, <c>SkillSeeds</c>,
    /// <c>AbilitySeeds</c>, <c>ResourceSeeds</c>) whose own concrete typed
    /// nested entities are not implemented until ODY-S04-108/109 -- this task
    /// does not invent that production schema early. Instead it models every
    /// seed category generically (a free-text <see cref="Category"/> plus a
    /// flat name/value pair), which is exactly enough to prove ADR-023 section
    /// 5.3's fresh-identifier deep-copy mechanism and CAP-INV-006 for real,
    /// without deciding ability/resource/anatomy mechanics this task does not
    /// own. A later task may translate copied items of a given category into
    /// its own concrete typed entities; it is not required to consume this
    /// shape at all.
    /// </summary>
    public sealed class CharacterTemplateSeedItem
    {
        public CharacterTemplateSeedItem(TemplateSeedItemId seedItemId, string category, string name, string? value)
        {
            if (!seedItemId.IsValid) throw new ArgumentException("SeedItemId is required.", nameof(seedItemId));
            if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("Category is required.", nameof(category));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));

            SeedItemId = seedItemId;
            Category = category;
            Name = name;
            Value = value;
        }

        public TemplateSeedItemId SeedItemId { get; }
        public string Category { get; }
        public string Name { get; }
        public string? Value { get; }
    }

    /// <summary>
    /// ODY-S04-103: a template's full seed data -- an ordered, duplicate-free
    /// (by <see cref="CharacterTemplateSeedItem.SeedItemId"/>) collection of
    /// nested entries.
    /// </summary>
    public sealed class CharacterTemplateSeed
    {
        public CharacterTemplateSeed(IReadOnlyList<CharacterTemplateSeedItem> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterTemplateSeedItem item in items)
            {
                if (!seen.Add(item.SeedItemId.ToString()))
                {
                    throw new ArgumentException("Duplicate SeedItemId in template seed.", nameof(items));
                }
            }

            Items = items;
        }

        public static CharacterTemplateSeed Empty() => new CharacterTemplateSeed(Array.Empty<CharacterTemplateSeedItem>());

        public IReadOnlyList<CharacterTemplateSeedItem> Items { get; }
    }

    /// <summary>
    /// ODY-S04-103: one nested instance produced by
    /// <see cref="CharacterTemplateSeedCopier.CopyWithFreshIdentifiers"/> --
    /// ADR-023 section 5.3's independent copy. <see cref="SourceTemplateId"/>/
    /// <see cref="SourceSeedItemId"/> are immutable, point-in-time provenance
    /// only; nothing in this codebase re-resolves them back into the source
    /// template's current state.
    /// </summary>
    public sealed class CopiedCharacterSeedItem
    {
        public CopiedCharacterSeedItem(TemplateSeedItemId newSeedItemId, CharacterTemplateId? sourceTemplateId, TemplateSeedItemId sourceSeedItemId, string category, string name, string? value)
        {
            if (!newSeedItemId.IsValid) throw new ArgumentException("NewSeedItemId is required.", nameof(newSeedItemId));
            if (!sourceSeedItemId.IsValid) throw new ArgumentException("SourceSeedItemId is required.", nameof(sourceSeedItemId));
            if (string.IsNullOrWhiteSpace(category)) throw new ArgumentException("Category is required.", nameof(category));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));

            NewSeedItemId = newSeedItemId;
            SourceTemplateId = sourceTemplateId;
            SourceSeedItemId = sourceSeedItemId;
            Category = category;
            Name = name;
            Value = value;
        }

        public TemplateSeedItemId NewSeedItemId { get; }
        public CharacterTemplateId? SourceTemplateId { get; }
        public TemplateSeedItemId SourceSeedItemId { get; }
        public string Category { get; }
        public string Name { get; }
        public string? Value { get; }
    }

    /// <summary>
    /// ODY-S04-103: ADR-023 section 5.3's independent-copy mechanism --
    /// "reads the template's current seed data... copies each value... mints
    /// a fresh identifier for every nested instance... never reusing a
    /// template-scoped identifier as a Character-scoped one." This is the one
    /// place in the codebase that mints the fresh identifiers; callers never
    /// mint their own for this purpose. Because the copy is by value and the
    /// template reference is recorded only as provenance
    /// (<see cref="CopiedCharacterSeedItem.SourceTemplateId"/>), a later edit
    /// to the source template has no code path back into an already-copied
    /// result -- this is what makes CAP-INV-006 true by construction.
    /// </summary>
    public static class CharacterTemplateSeedCopier
    {
        public static IReadOnlyList<CopiedCharacterSeedItem> CopyWithFreshIdentifiers(CharacterTemplateSeed seed, CharacterTemplateId sourceTemplateId, UtcInstant now)
        {
            if (seed == null) throw new ArgumentNullException(nameof(seed));

            var copies = new List<CopiedCharacterSeedItem>(seed.Items.Count);
            foreach (CharacterTemplateSeedItem item in seed.Items)
            {
                TemplateSeedItemId newId = TemplateSeedItemId.NewId(now);
                copies.Add(new CopiedCharacterSeedItem(newId, sourceTemplateId, item.SeedItemId, item.Category, item.Name, item.Value));
            }

            return copies;
        }
    }
}
