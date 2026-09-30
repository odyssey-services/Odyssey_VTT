using System;
using Odyssey.Application.Audience;
using Odyssey.Application.Checks;
using Odyssey.Application.Combat;
using Odyssey.Application.Dice;
using Odyssey.Application.Persistence;
using Odyssey.Application.Random;
using Odyssey.Rules.Combat;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// ODY-S11-205: the ports the combat panel calls, gathered by the screen composition and passed to one constructor --
    /// an explicit parameter object (nothing is resolved by type), so the panel stays constructor-injected (ADR-005).
    /// </summary>
    public sealed class CombatPorts
    {
        public CombatPorts(
            ICharacterRepository characters,
            IInventoryRepository inventory,
            IContentCatalogRepository catalog,
            ISceneRepository scenes,
            ICombatEncounterRepository encounters,
            IAttackStateReader attackReader,
            IAttackApplyRepository attackApply,
            IAttackRulesEvaluator attackRules,
            IActivateAbilityStateReader abilityReader,
            IActivateAbilityRepository abilityApply,
            IUseItemStateReader useItemReader,
            IUseItemRepository useItemApply,
            IActiveEffectRepository effects,
            ICheckStateReader checkReader,
            ICheckRepository checkApply,
            DiceRollStore rollStore,
            IGameLogRepository gameLog,
            ICampaignUserGroupDirectory groups,
            IAuthoritativeRandomStreamFactory random,
            RngKeyEpochId keyEpochId)
        {
            Characters = characters ?? throw new ArgumentNullException(nameof(characters));
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
            Encounters = encounters ?? throw new ArgumentNullException(nameof(encounters));
            AttackReader = attackReader ?? throw new ArgumentNullException(nameof(attackReader));
            AttackApply = attackApply ?? throw new ArgumentNullException(nameof(attackApply));
            AttackRules = attackRules ?? throw new ArgumentNullException(nameof(attackRules));
            AbilityReader = abilityReader ?? throw new ArgumentNullException(nameof(abilityReader));
            AbilityApply = abilityApply ?? throw new ArgumentNullException(nameof(abilityApply));
            UseItemReader = useItemReader ?? throw new ArgumentNullException(nameof(useItemReader));
            UseItemApply = useItemApply ?? throw new ArgumentNullException(nameof(useItemApply));
            Effects = effects ?? throw new ArgumentNullException(nameof(effects));
            CheckReader = checkReader ?? throw new ArgumentNullException(nameof(checkReader));
            CheckApply = checkApply ?? throw new ArgumentNullException(nameof(checkApply));
            RollStore = rollStore ?? throw new ArgumentNullException(nameof(rollStore));
            GameLog = gameLog ?? throw new ArgumentNullException(nameof(gameLog));
            Groups = groups ?? throw new ArgumentNullException(nameof(groups));
            Random = random ?? throw new ArgumentNullException(nameof(random));
            KeyEpochId = keyEpochId;
        }

        public ICharacterRepository Characters { get; }
        public IInventoryRepository Inventory { get; }
        public IContentCatalogRepository Catalog { get; }
        public ISceneRepository Scenes { get; }
        public ICombatEncounterRepository Encounters { get; }
        public IAttackStateReader AttackReader { get; }
        public IAttackApplyRepository AttackApply { get; }
        public IAttackRulesEvaluator AttackRules { get; }
        public IActivateAbilityStateReader AbilityReader { get; }
        public IActivateAbilityRepository AbilityApply { get; }
        public IUseItemStateReader UseItemReader { get; }
        public IUseItemRepository UseItemApply { get; }
        public IActiveEffectRepository Effects { get; }
        public ICheckStateReader CheckReader { get; }
        public ICheckRepository CheckApply { get; }
        public DiceRollStore RollStore { get; }
        public IGameLogRepository GameLog { get; }
        public ICampaignUserGroupDirectory Groups { get; }
        public IAuthoritativeRandomStreamFactory Random { get; }
        public RngKeyEpochId KeyEpochId { get; }
    }
}
