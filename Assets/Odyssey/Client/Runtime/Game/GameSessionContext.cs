using System;
using Odyssey.Application.Persistence;
using Odyssey.Application.Time;
using Odyssey.Domain.Identity;
using Odyssey.Rules.Versions;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// ODY-S11-201: what every game-screen panel needs, gathered once by the screen composition
    /// (<see cref="TrialScreenPresenter"/>) and handed to each panel's constructor -- an explicit parameter object,
    /// not a registry: nothing is looked up by type, and nothing outlives the screen that created it (ADR-005).
    /// Later phases extend it with the ports their panels call (catalog, characters, inventory, combat).
    /// </summary>
    public sealed class GameSessionContext
    {
        public GameSessionContext(
            CampaignHandle campaign,
            ICampaignRepository campaignRepository,
            SceneId sceneId,
            IWallClock clock,
            RoleSelection selection,
            PresentationRuntime presentationRuntime,
            RulesetVersion rulesetVersion,
            VisualElement modalHost)
        {
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            CampaignRepository = campaignRepository ?? throw new ArgumentNullException(nameof(campaignRepository));
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));
            SceneId = sceneId;
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Selection = selection ?? throw new ArgumentNullException(nameof(selection));
            PresentationRuntime = presentationRuntime ?? throw new ArgumentNullException(nameof(presentationRuntime));
            RulesetVersion = rulesetVersion ?? throw new ArgumentNullException(nameof(rulesetVersion));
            ModalHost = modalHost ?? throw new ArgumentNullException(nameof(modalHost));
        }

        public CampaignHandle Campaign { get; }
        public ICampaignRepository CampaignRepository { get; }
        public SceneId SceneId { get; }
        public IWallClock Clock { get; }
        public RoleSelection Selection { get; }
        public PresentationRuntime PresentationRuntime { get; }
        public RulesetVersion RulesetVersion { get; }

        /// <summary>Where confirmation dialogs mount (the screen root, above the board and overlays).</summary>
        public VisualElement ModalHost { get; }

        /// <summary>The campaign's active ruleset key as catalog compatibility lists spell it (<c>rulesetId@version</c>).</summary>
        public string ActiveRulesetKey => Campaign.Manifest.RulesetId + "@" + Campaign.Manifest.RulesetVersion;

        public UserId ActorUserId => Selection.ActorUserId;
        public bool ActorIsMainGm => Selection.ActorIsMainGm;
    }
}
