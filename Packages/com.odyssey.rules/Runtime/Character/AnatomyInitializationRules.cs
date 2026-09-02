using System.Collections.Generic;
using Odyssey.Domain.Character;

namespace Odyssey.Rules.Character
{
    /// <summary>
    /// ODY-S04-109: ADR-024 section 9's cost/content-fixture assignment,
    /// extended to anatomy initialization by this task -- exactly mirroring
    /// <see cref="ResourceInitializationRules"/>'s own reasoning.
    ///
    /// TEST FIXTURE ONLY -- NOT production Ruleset content. No
    /// <c>AnatomyProfileDefinition</c> catalog exists anywhere in this
    /// codebase (confirmed by search before writing this file). This
    /// fixture provides the smallest humanoid body-part set needed to prove
    /// <c>InitializeCharacterAnatomy</c>/<c>RemoveBodyPart</c>'s dependency
    /// check end-to-end -- two attached limbs (<c>LeftArm</c>/<c>RightArm</c>,
    /// each <see cref="BodyPart.AttachedToBodyPartId"/> pointing at
    /// <c>Torso</c>) alongside an unattached <c>Head</c>, so a test can
    /// exercise both "has a dependent" (removing <c>Torso</c>) and
    /// "independent, removes cleanly" (removing <c>Head</c>) without any
    /// additional fixture content. A future Ruleset-catalog task must
    /// replace this file's body with a real per-<c>AnatomyProfileDefinitionId</c>
    /// content-driven lookup without changing
    /// <c>InitializeCharacterAnatomy</c>'s own call site.
    /// </summary>
    public static class AnatomyInitializationRules
    {
        public const string DefaultAnatomyProfileVersion = "0.1.0-fixture";

        public static IReadOnlyList<BodyPart> DefaultHumanoidBodyParts() => new List<BodyPart>
        {
            new BodyPart(BodyPartId.Parse("Head"), "Head", damageLimit: 10, attachedToBodyPartId: null, properties: "{}"),
            new BodyPart(BodyPartId.Parse("Torso"), "Torso", damageLimit: 20, attachedToBodyPartId: null, properties: "{}"),
            new BodyPart(BodyPartId.Parse("LeftArm"), "Left Arm", damageLimit: 10, attachedToBodyPartId: BodyPartId.Parse("Torso"), properties: "{}"),
            new BodyPart(BodyPartId.Parse("RightArm"), "Right Arm", damageLimit: 10, attachedToBodyPartId: BodyPartId.Parse("Torso"), properties: "{}"),
        };
    }
}
