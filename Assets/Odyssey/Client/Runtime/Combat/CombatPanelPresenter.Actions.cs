using System;
using System.Collections.Generic;
using Odyssey.Application.Checks;
using Odyssey.Application.Combat;
using Odyssey.Application.Content;
using Odyssey.Application.Dice;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Checks;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    /// <summary>A character ability that can be activated (linked to a published Ability definition).</summary>
    public sealed class ActivatableAbility
    {
        public ActivatableAbility(CharacterAbility ability, AbilityDefinition? definition, string name)
        {
            Ability = ability ?? throw new ArgumentNullException(nameof(ability));
            Definition = definition;
            Name = name;
        }

        public CharacterAbility Ability { get; }
        public AbilityDefinition? Definition { get; }
        public string Name { get; }
    }

    // ODY-S11-205: acting-participant actions -- attack (preview -> resolve), ability, item use (self), check.
    public sealed partial class CombatPanelPresenter
    {
        private readonly List<InventoryItemView> _weapons = new List<InventoryItemView>();
        private readonly List<InventoryItemView> _usableItems = new List<InventoryItemView>();
        private readonly List<ActivatableAbility> _abilities = new List<ActivatableAbility>();
        private readonly List<CharacterId> _selectedTargets = new List<CharacterId>();
        private InventoryView? _actorInventory;
        private int _unequippedWeaponCount;
        private CharacterRecord? _actor;

        public IReadOnlyList<InventoryItemView> Weapons => _weapons;
        public IReadOnlyList<InventoryItemView> UsableItems => _usableItems;
        public IReadOnlyList<ActivatableAbility> Abilities => _abilities;
        public IReadOnlyList<CharacterId> SelectedTargets => _selectedTargets;

        /// <summary>The last preview (range / modifiers / armor / deltas); null after it was resolved or invalidated.</summary>
        public ProposedAttackResolution? LastPreview { get; private set; }
        public AttackOutcomeRecord? LastOutcome { get; private set; }
        public CheckOutcomeRecord? LastCheck { get; private set; }
        public string? SelectedWeaponKey { get; private set; }

        public void ToggleTarget(CharacterId target)
        {
            if (_selectedTargets.Contains(target)) _selectedTargets.Remove(target);
            else _selectedTargets.Add(target);
            LastPreview = null;
            Render();
        }

        public void SelectWeapon(string itemKey)
        {
            SelectedWeaponKey = itemKey;
            LastPreview = null;
            Render();
        }

        /// <summary>Read-only preview: distance / in range, modifiers, armor, damage deltas per target. Nothing is written.</summary>
        public Result<ProposedAttackResolution> PreviewAttack()
        {
            Result<AttackRequest> request = BuildAttackRequest();
            if (request.IsFailure) return Result<ProposedAttackResolution>.Failure(request.Error);
            Result<ProposedAttackResolution> preview = UiGuard.Run(() => AttackEvaluationService.PreviewAttack(_ports.AttackReader, _context.CampaignRepository, _ports.AttackRules, _context.Campaign, request.Value));
            if (preview.IsFailure)
            {
                ReportFailure("Attack preview", preview.Error);
                return preview;
            }

            LastPreview = preview.Value;
            Banner.Show(OdyBannerKind.Info, preview.Value.Range.IsInRange ? "In range -- review the preview, then confirm." : "Out of range: " + preview.Value.Range.Reason);
            Render();
            return preview;
        }

        /// <summary>Resolves the attack. A Pending outcome waits for the MainGM (intervention section).</summary>
        public Result<AttackOutcomeRecord> ResolveAttack()
        {
            Result<AttackRequest> request = BuildAttackRequest();
            if (request.IsFailure) return Result<AttackOutcomeRecord>.Failure(request.Error);
            CharacterId actorId = request.Value.Intent.ActorId;
            long cost = WeaponActionCost();
            Result<AttackOutcomeRecord> resolved = UiGuard.Run(() => AttackApplyService.ResolveAttack(_ports.AttackReader, _context.CampaignRepository, _ports.AttackRules, _ports.Random, _ports.AttackApply, _context.Campaign, _ports.KeyEpochId, request.Value));
            if (resolved.IsFailure)
            {
                ReportFailure("Attack", resolved.Error);
                return resolved;
            }

            LastOutcome = resolved.Value;
            LastPreview = null;
            RememberAttack(resolved.Value);
            RecordAction(actorId, cost);
            Banner.Show(resolved.Value.OutcomeKind == AttackOutcomeKind.Pending ? OdyBannerKind.Warning : OdyBannerKind.Success, DescribeOutcome(resolved.Value));
            Refresh();
            return resolved;
        }

        /// <summary>Activates a linked ability of the acting participant on the chosen targets.</summary>
        public Result<AbilityActivationRecord> ActivateAbility(CharacterAbilityId characterAbilityId, IReadOnlyList<CharacterId> targets)
        {
            if (_actor == null) return Result<AbilityActivationRecord>.Failure(UiGuard.InvalidRequest());
            ActivatableAbility? ability = null;
            foreach (ActivatableAbility candidate in _abilities) if (candidate.Ability.CharacterAbilityId.Equals(characterAbilityId)) ability = candidate;
            if (ability == null)
            {
                Banner.Show(OdyBannerKind.Error, "Choose one of the acting character's activatable abilities.");
                return Result<AbilityActivationRecord>.Failure(UiGuard.InvalidRequest());
            }

            CharacterRecord actor = _actor;
            var targetList = new List<CharacterId>(targets ?? Array.Empty<CharacterId>());
            Result<AbilityActivationRecord> activated = UiGuard.Run(() => ActivateAbilityService.ActivateAbility(_ports.AbilityReader, _context.CampaignRepository, _ports.AbilityApply, _ports.Catalog, _ports.Effects, _ports.Random, _context.Clock, _context.Campaign, _ports.KeyEpochId,
                new ActivateAbilityRequest(new ActivateAbilityIntent(actor.CharacterId, characterAbilityId, targetList, actor.Revisions.CharacterAbilitiesRevision, actor.Revisions.CharacterResourcesRevision), _context.ActorUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId())));
            if (activated.IsFailure)
            {
                ReportFailure("Ability", activated.Error);
                return activated;
            }

            RecordAction(actor.CharacterId, ability.Definition?.ActionCost ?? 1);
            Banner.Show(OdyBannerKind.Success, ability.Name + " activated" + (activated.Value.ResourceDeltas.Count > 0 ? ": " + DescribeDeltas(activated.Value.ResourceDeltas) : "."));
            Refresh();
            return activated;
        }

        /// <summary>Uses an item of the acting participant's own inventory -- always on self (the backend has no item targets).</summary>
        public Result<ItemUsageRecord> UseItem(string itemKey)
        {
            if (_actor == null || _actorInventory == null) return Result<ItemUsageRecord>.Failure(UiGuard.InvalidRequest());
            InventoryItemView? item = _actorInventory.Find(itemKey);
            if (item == null)
            {
                Banner.Show(OdyBannerKind.Error, "Choose an item from the acting character's inventory.");
                return Result<ItemUsageRecord>.Failure(UiGuard.InvalidRequest());
            }

            CharacterRecord actor = _actor;
            long inventoryRevision = _actorInventory.Inventory.Revision;
            Result<ItemUsageRecord> used = UiGuard.Run(() => UseItemService.UseItem(_ports.UseItemReader, _context.CampaignRepository, _ports.UseItemApply, _ports.Catalog, _ports.Effects, _ports.Random, _context.Clock, _context.Campaign, _ports.KeyEpochId,
                new UseItemRequest(new UseItemIntent(actor.CharacterId, item.Ref, item.Revision, inventoryRevision, actor.Revisions.CharacterResourcesRevision), _context.ActorUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId())));
            if (used.IsFailure)
            {
                ReportFailure("Use item", used.Error);
                return used;
            }

            RecordAction(actor.CharacterId, 1);
            Banner.Show(OdyBannerKind.Success, item.Name + " used on " + actor.DisplayName + (used.Value.ResourceDeltas.Count > 0 ? ": " + DescribeDeltas(used.Value.ResourceDeltas) : "."));
            Refresh();
            return used;
        }

        /// <summary>A skill/attribute check: free formula, a difficulty (DC) and who may see the roll.</summary>
        public Result<CheckOutcomeRecord> PerformCheck(CharacterId actorId, string formula, long difficultyClass, DiceRollAudienceKind audienceKind)
        {
            if (string.IsNullOrWhiteSpace(formula))
            {
                Banner.Show(OdyBannerKind.Error, "Enter a check formula, e.g. 1d20+2.");
                return Result<CheckOutcomeRecord>.Failure(UiGuard.InvalidRequest());
            }

            DiceRollAudience audience = audienceKind == DiceRollAudienceKind.GMOnly ? DiceRollAudience.GMOnly()
                : audienceKind == DiceRollAudienceKind.PlayerAndGM ? DiceRollAudience.PlayerAndGM()
                : DiceRollAudience.Public();
            Result<CheckOutcomeRecord> checkResult = UiGuard.Run(() => CheckService.PerformCheck(_ports.CheckReader, _ports.CheckApply, _ports.Characters, _ports.RollStore, _ports.Random, _context.Clock, _context.Campaign, _context.RulesetVersion, _ports.KeyEpochId,
                new CheckRequest(new CheckIntent(actorId, formula.Trim(), difficultyClass, audience), _context.ActorUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId())));
            if (checkResult.IsFailure)
            {
                ReportFailure("Check", checkResult.Error);
                return checkResult;
            }

            LastCheck = checkResult.Value;
            Banner.Show(checkResult.Value.Result == CheckResultKind.Pass ? OdyBannerKind.Success : OdyBannerKind.Warning,
                NameOf(actorId) + ": " + checkResult.Value.Result + " vs DC " + difficultyClass + (checkResult.Value.IsNaturalMaximum ? " -- critical success!" : "."));
            Render();
            return checkResult;
        }

        // ---- internals ------------------------------------------------------------------------------

        private Result<AttackRequest> BuildAttackRequest()
        {
            if (Encounter == null || !Encounter.CurrentParticipantId.HasValue)
            {
                Banner.Show(OdyBannerKind.Error, "No open turn.");
                return Result<AttackRequest>.Failure(UiGuard.InvalidRequest());
            }

            if (_selectedTargets.Count == 0)
            {
                Banner.Show(OdyBannerKind.Error, "Choose at least one target.");
                return Result<AttackRequest>.Failure(UiGuard.InvalidRequest());
            }

            InventoryItemView? weapon = SelectedWeaponKey == null ? null : _actorInventory?.Find(SelectedWeaponKey);
            if (weapon == null || weapon.Instance == null)
            {
                Banner.Show(OdyBannerKind.Error, "Choose a weapon from the acting character's inventory.");
                return Result<AttackRequest>.Failure(UiGuard.InvalidRequest());
            }

            // Fresh encounter revision: the intent pins it and the backend rejects a stale one.
            Result<CombatEncounterRecord> fresh = _ports.Encounters.Get(_context.Campaign, Encounter.EncounterId, UiCommandIds.NewCorrelationId());
            if (fresh.IsSuccess) Encounter = fresh.Value;
            CombatEncounterRecord encounter = Encounter;
            var targets = new List<CharacterId>(_selectedTargets);
            ItemInstanceId weaponId = weapon.Instance.ItemInstanceId;
            return UiGuard.Run(() => Result<AttackRequest>.Success(new AttackRequest(new AttackIntent(encounter.EncounterId, encounter.CurrentParticipantId!.Value, targets, weaponId, encounter.Revision), _context.ActorUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId())));
        }

        private long WeaponActionCost()
        {
            InventoryItemView? weapon = SelectedWeaponKey == null ? null : _actorInventory?.Find(SelectedWeaponKey);
            if (weapon == null) return 1;
            Result<WeaponDefinition> decoded = TypedDefinitionCodec.DecodeWeapon(weapon.Snapshot.ContentType, weapon.Snapshot.Payload, UiCommandIds.NewCorrelationId());
            return decoded.IsSuccess ? decoded.Value.ActionCost : 1;
        }

        /// <summary>Loads the acting participant's sheet, weapons, usable items and activatable abilities.</summary>
        private void LoadActorData()
        {
            _weapons.Clear();
            _unequippedWeaponCount = 0;
            _usableItems.Clear();
            _abilities.Clear();
            _actor = null;
            _actorInventory = null;
            if (Encounter == null || !Encounter.CurrentParticipantId.HasValue) return;
            CharacterId actorId = Encounter.CurrentParticipantId.Value;
            Result<CharacterRecord> actor = _ports.Characters.GetCharacter(_context.Campaign, actorId, UiCommandIds.NewCorrelationId());
            if (actor.IsFailure) return;
            _actor = actor.Value;

            Result<InventoryView> inventory = InventoryLocator.Load(_ports.Inventory, _ports.Catalog, _context.Campaign, InventoryLocator.CharacterInventoryId(actorId));
            if (inventory.IsSuccess)
            {
                _actorInventory = inventory.Value;
                foreach (InventoryItemView item in inventory.Value.Items)
                {
                    // The backend attacks only with an equipped weapon (ODY-S06-103); unequipped ones are counted for the hint.
                    if (!item.IsStack && item.Snapshot.ContentType == ContentDefinitionType.Weapon)
                    {
                        if (item.IsEquipped) _weapons.Add(item);
                        else _unequippedWeaponCount++;
                    }
                    else if (item.Snapshot.ContentType == ContentDefinitionType.Item)
                    {
                        // Use Item works on Item-type definitions (their built-in effects), always on self.
                        _usableItems.Add(item);
                    }
                }
            }

            if (SelectedWeaponKey != null && _actorInventory?.Find(SelectedWeaponKey) == null) SelectedWeaponKey = null;
            if (SelectedWeaponKey == null && _weapons.Count > 0) SelectedWeaponKey = _weapons[0].Key;

            foreach (CharacterAbility ability in _actor.Abilities)
            {
                if (!ability.ActivationDefinitionRef.HasValue) continue;
                ContentDefinitionRef reference = ability.ActivationDefinitionRef.Value;
                Result<ContentDefinitionRecord> definition = _ports.Catalog.GetContentDefinition(_context.Campaign, reference.DefinitionId, UiCommandIds.NewCorrelationId());
                AbilityDefinition? decoded = null;
                string name = ability.AbilityDefinitionId.ToString();
                if (definition.IsSuccess)
                {
                    name = definition.Value.Name;
                    Result<AbilityDefinition> typed = TypedDefinitionCodec.DecodeAbility(definition.Value.DefinitionType, definition.Value.PropertiesJson, UiCommandIds.NewCorrelationId());
                    if (typed.IsSuccess) decoded = typed.Value;
                }

                _abilities.Add(new ActivatableAbility(ability, decoded, name));
            }

            // Targets that are no longer participants are dropped.
            _selectedTargets.RemoveAll(target => !IsParticipant(target));
        }

        private bool IsParticipant(CharacterId id)
        {
            if (Encounter == null) return false;
            foreach (CombatParticipant participant in Encounter.Participants) if (participant.CharacterId == id) return true;
            return false;
        }

        private string DescribeOutcome(AttackOutcomeRecord outcome)
        {
            switch (outcome.OutcomeKind)
            {
                case AttackOutcomeKind.Pending:
                    return "The attack waits for a MainGM decision (an effect needs intervention).";
                case AttackOutcomeKind.Accepted:
                    return outcome.DamageDeltas.Count > 0 ? "Hit: " + DescribeDeltas(outcome.DamageDeltas) : "No damage (miss or out of range).";
                case AttackOutcomeKind.Rejected:
                    return "The attack was rejected.";
                default:
                    return "The attack was cancelled.";
            }
        }

        private string DescribeDeltas(IReadOnlyList<AttackDelta> deltas)
        {
            var parts = new List<string>();
            foreach (AttackDelta delta in deltas) parts.Add(NameOfRef(delta.TargetRef) + " " + (delta.Value >= 0 ? "+" : string.Empty) + delta.Value);
            return string.Join(", ", parts);
        }

        // ---- rendering ------------------------------------------------------------------------------

        private VisualElement BuildTargetPicker(string namePrefix, bool includeSelf)
        {
            var box = new VisualElement { name = namePrefix + "-targets" };
            box.Add(OdyUi.Text("Targets", OdyClasses.TextLabel));
            if (Encounter == null) return box;
            foreach (CombatParticipant participant in Encounter.Participants)
            {
                CharacterId id = participant.CharacterId;
                bool isSelf = Encounter.CurrentParticipantId.HasValue && Encounter.CurrentParticipantId.Value == id;
                if (isSelf && !includeSelf) continue;
                Toggle toggle = OdyUi.Toggle(NameOf(id) + (isSelf ? " (self)" : string.Empty), _selectedTargets.Contains(id), namePrefix + "-target-" + id);
                toggle.RegisterValueChangedCallback(_ => ToggleTarget(id));
                box.Add(toggle);
            }

            return box;
        }

        private bool ActorControlled => _actor != null && (ActorIsMainGm || IsControlledBy(_actor, _context.ActorUserId));

        private static bool IsControlledBy(CharacterRecord record, UserId user)
        {
            CharacterOwnership o = record.Ownership;
            if (o.PrimaryOwnerUserId.HasValue && o.PrimaryOwnerUserId.Value.Equals(user)) return true;
            foreach (UserId id in o.CoOwnerUserIds) if (id.Equals(user)) return true;
            foreach (UserId id in o.PermanentControllerUserIds) if (id.Equals(user)) return true;
            foreach (CharacterTemporaryControlGrant grant in o.TemporaryControlGrants) if (grant.UserId.Equals(user)) return true;
            return false;
        }

        private VisualElement BuildAttackSection()
        {
            VisualElement section = OdyUi.Section("Attack", "combat-attack");
            if (_actor == null) return section;
            if (!ActorControlled)
            {
                section.Add(OdyUi.Text(_actor.DisplayName + " acts now; only their owner/controller or the MainGM can act for them.", OdyClasses.FieldHint));
                return section;
            }

            section.Add(OdyUi.Text("Attacker: " + _actor.DisplayName + " (the acting participant). Hits are decided by range -- there is no hit chance and no hit location.", OdyClasses.FieldHint));
            section.Add(BuildTargetPicker("combat-attack", includeSelf: false));
            if (_weapons.Count == 0)
            {
                section.Add(OdyUi.Text(_unequippedWeaponCount > 0
                    ? _actor.DisplayName + " has " + _unequippedWeaponCount + " weapon(s) but none equipped -- the MainGM equips a weapon in the Inventory panel."
                    : _actor.DisplayName + " has no weapon in their inventory.", OdyClasses.FieldHint));
                return section;
            }

            var names = new List<string>();
            int selected = 0;
            for (int index = 0; index < _weapons.Count; index++)
            {
                names.Add(_weapons[index].Name + " -- " + _weapons[index].Summary);
                if (_weapons[index].Key == SelectedWeaponKey) selected = index;
            }

            DropdownField weapon = OdyUi.Dropdown("Weapon", names, selected, "combat-attack-weapon");
            weapon.RegisterValueChangedCallback(_ => { if (weapon.index >= 0 && weapon.index < _weapons.Count) SelectWeapon(_weapons[weapon.index].Key); });
            section.Add(weapon);
            section.Add(OdyUi.ButtonRow(
                OdyUi.Button("Preview", () => PreviewAttack(), OdyButtonVariant.Secondary, "combat-attack-preview", small: true),
                OdyUi.Button("Confirm attack", () => ResolveAttack(), OdyButtonVariant.Primary, "combat-attack-resolve", small: true)));

            if (LastPreview != null) section.Add(BuildPreviewCard(LastPreview));
            return section;
        }

        private VisualElement BuildPreviewCard(ProposedAttackResolution preview)
        {
            VisualElement card = OdyUi.Card("Preview", out VisualElement body, "combat-attack-preview-card");
            if (preview.Snapshot.Topology.Availability == AttackTopologyAvailability.Available)
            {
                foreach (AttackTargetDistanceEntry distance in preview.Snapshot.Topology.Entries) body.Add(OdyUi.KeyValue("Distance to " + NameOf(distance.TargetId), distance.Distance.ToString("0.##"), out _));
            }
            else
            {
                body.Add(OdyUi.KeyValue("Distance", "unavailable (" + preview.Snapshot.Topology.Reason + ")", out _));
            }

            body.Add(OdyUi.KeyValue("In range", preview.Range.IsInRange ? "yes" : "no -- " + preview.Range.Reason, out _, "combat-preview-range"));
            body.Add(OdyUi.KeyValue("Outcome", preview.Hit.Outcome, out _, "combat-preview-outcome"));
            var modifiers = new List<string>();
            foreach (AttackModifierEntry modifier in preview.Modifiers) modifiers.Add(modifier.Source + " " + (modifier.Value >= 0 ? "+" : string.Empty) + modifier.Value);
            body.Add(OdyUi.KeyValue("Modifiers", modifiers.Count == 0 ? "none" : string.Join(", ", modifiers), out _));
            body.Add(OdyUi.KeyValue("Armor", preview.Armor.HasValue ? preview.Armor.Value.ArmorRef + " absorbs " + preview.Armor.Value.Absorbed : "none", out _));
            body.Add(OdyUi.KeyValue("Damage", preview.DamageDeltas.Count == 0 ? "none" : DescribeDeltas(preview.DamageDeltas), out _, "combat-preview-damage"));
            if (preview.CostDeltas.Count > 0) body.Add(OdyUi.KeyValue("Costs", DescribeDeltas(preview.CostDeltas), out _));
            if (preview.EffectCandidates.Count > 0)
            {
                var effects = new List<string>();
                foreach (AttackEffectCandidate candidate in preview.EffectCandidates) effects.Add(NameOf(candidate.TargetId) + ": " + candidate.Decision);
                body.Add(OdyUi.KeyValue("Effects", string.Join(", ", effects), out _));
            }

            body.Add(OdyUi.Text("The preview has no dice: damage dice are rolled when you confirm.", OdyClasses.FieldHint));
            return card;
        }

        private VisualElement BuildAbilitySection()
        {
            VisualElement section = OdyUi.Section("Abilities", "combat-abilities");
            if (_actor == null || !ActorControlled) return section;
            if (_abilities.Count == 0)
            {
                section.Add(OdyUi.Text(_actor.DisplayName + " has no activatable ability (the MainGM links abilities to catalog Abilities in the character sheet).", OdyClasses.FieldHint));
                return section;
            }

            foreach (ActivatableAbility ability in _abilities)
            {
                VisualElement card = OdyUi.Card(ability.Name, out VisualElement body, "combat-ability-" + ability.Ability.CharacterAbilityId);
                if (ability.Definition != null)
                {
                    body.Add(OdyUi.Text(EnumChoices.Humanize(ability.Definition.EntryPointType.ToString()) + " · action cost " + ability.Definition.ActionCost + " · " + TargetRuleEditor.Describe(ability.Definition.TargetRule), OdyClasses.TextCaption));
                }

                bool includeSelf = ability.Definition == null || ability.Definition.TargetRule.AllowSelf;
                body.Add(BuildTargetPicker("combat-ability-" + ability.Ability.CharacterAbilityId, includeSelf));
                CharacterAbilityId id = ability.Ability.CharacterAbilityId;
                body.Add(OdyUi.ButtonRow(OdyUi.Button("Activate", () => ActivateAbility(id, _selectedTargets), OdyButtonVariant.Primary, "combat-ability-activate-" + id, small: true)));
                section.Add(card);
            }

            return section;
        }

        private VisualElement BuildUseItemSection()
        {
            VisualElement section = OdyUi.Section("Use an item (on self)", "combat-use-item");
            if (_actor == null || !ActorControlled) return section;
            if (_usableItems.Count == 0)
            {
                section.Add(OdyUi.Text("No usable items in " + _actor.DisplayName + "'s inventory.", OdyClasses.FieldHint));
                return section;
            }

            foreach (InventoryItemView item in _usableItems)
            {
                var row = new VisualElement();
                row.AddToClassList(OdyClasses.ListItem);
                var main = new VisualElement();
                main.AddToClassList(OdyClasses.ListItemMain);
                main.Add(OdyUi.TruncatedText(item.Name + (item.IsStack ? " × " + item.Quantity : string.Empty), OdyClasses.ListItemTitle));
                main.Add(OdyUi.TruncatedText(item.Summary, OdyClasses.ListItemMeta));
                row.Add(main);
                string key = item.Key;
                row.Add(OdyUi.Button("Use", () => UseItem(key), OdyButtonVariant.Secondary, "combat-use-item-" + key, small: true));
                section.Add(row);
            }

            section.Add(OdyUi.Text("Items always act on their user; there is no target selection for items.", OdyClasses.FieldHint));
            return section;
        }

        private VisualElement BuildCheckSection()
        {
            VisualElement section = OdyUi.Section("Check", "combat-check");
            var actors = new List<CharacterId>();
            if (Encounter != null) foreach (CombatParticipant participant in Encounter.Participants) actors.Add(participant.CharacterId);
            foreach (CharacterId candidate in _candidates) if (!actors.Contains(candidate)) actors.Add(candidate);
            if (actors.Count == 0)
            {
                section.Add(OdyUi.Text("Checks need a character on the scene or in the encounter.", OdyClasses.FieldHint));
                return section;
            }

            var names = new List<string>();
            int selected = 0;
            for (int index = 0; index < actors.Count; index++)
            {
                names.Add(NameOf(actors[index]));
                if (Encounter?.CurrentParticipantId.HasValue == true && Encounter.CurrentParticipantId!.Value == actors[index]) selected = index;
            }

            DropdownField who = OdyUi.Dropdown("Character", names, selected, "combat-check-actor");
            var row = new VisualElement();
            row.AddToClassList(OdyClasses.FormRow);
            TextField formula = OdyUi.TextField("Formula", "1d20", "combat-check-formula");
            IntegerField dc = OdyUi.IntegerField("DC", 10, "combat-check-dc");
            List<string> audiences = new List<string> { DiceRollAudienceKind.Public.ToString(), DiceRollAudienceKind.PlayerAndGM.ToString(), DiceRollAudienceKind.GMOnly.ToString() };
            DropdownField audience = OdyUi.Dropdown("Who sees it", audiences, 0, "combat-check-audience");
            row.Add(formula);
            row.Add(dc);
            row.Add(audience);
            section.Add(who);
            section.Add(row);
            section.Add(OdyUi.ButtonRow(OdyUi.Button("Roll check", () =>
            {
                int index = who.index;
                if (index < 0 || index >= actors.Count) return;
                PerformCheck(actors[index], formula.value, dc.value, EnumChoices.TryParse(audience.value, out DiceRollAudienceKind kind) ? kind : DiceRollAudienceKind.Public);
            }, OdyButtonVariant.Primary, "combat-check-roll", small: true)));

            if (LastCheck != null)
            {
                var result = new VisualElement();
                result.AddToClassList(OdyClasses.Row);
                result.Add(OdyUi.Badge(LastCheck.Result.ToString(), LastCheck.Result == CheckResultKind.Pass ? OdyStatusKind.Success : OdyStatusKind.Error, "combat-check-result"));
                if (LastCheck.IsNaturalMaximum) result.Add(OdyUi.Badge("Critical success", OdyStatusKind.Accent, "combat-check-critical"));
                result.Add(OdyUi.Text(NameOf(LastCheck.ActorId) + " · " + LastCheck.Formula + " vs DC " + LastCheck.DifficultyClass, OdyClasses.TextSmall));
                section.Add(result);
            }

            return section;
        }
    }
}
