using System;
using NUnit.Framework;
using Odyssey.Domain.Character;
using Odyssey.Domain.Content;
using Odyssey.Domain.Effects;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;

namespace Odyssey.Tests.Domain.Effects
{
    /// <summary>
    /// ODY-S05-502: real construction/validation/invariant tests for
    /// <see cref="ActiveEffect"/>, <see cref="ActiveEffectSourceRef"/>,
    /// <see cref="ActiveEffectTargetRef"/>, and <see cref="EffectMechanicsSnapshot"/>.
    /// No persistence, no stacking-policy resolution (`ODY-S05-503`), no
    /// duration/expiry mechanism (`ODY-S05-504`/`505`), and no removal
    /// command (`ODY-S05-506`) are exercised here.
    /// </summary>
    public sealed class ActiveEffectTests
    {
        private static readonly UtcInstant Now = UtcInstant.Parse("2026-09-12T00:00:00.0000000Z");
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
        private static ContentDefinitionRef NewEffectRef() => new ContentDefinitionRef(ContentDefinitionId.NewId(Now), 1);

        [Test] // TC-ACTIVEEFFECT-011
        public void ActiveEffect_ConstructsWithValidFields_AndExposesThemUnchanged()
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            var snapshot = new EffectMechanicsSnapshot(effectRef, 1, ContentDefinitionType.Effect, "{\"duration\":\"instant\"}");
            ActiveEffectSourceRef sourceRef = ActiveEffectSourceRef.ForItem(InventoryItemRef.ForInstance(ItemInstanceId.NewId(Now)));
            ActiveEffectTargetRef targetRef = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Now));
            UserId appliedByUserId = NewUserId();
            ActiveEffectId activeEffectId = ActiveEffectId.NewId(Now);

            var effect = new ActiveEffect(activeEffectId, effectRef, snapshot, sourceRef, targetRef, ActiveEffectStatus.Active, 1, appliedByUserId, Now, null, 1);

            Assert.That(effect.ActiveEffectId, Is.EqualTo(activeEffectId));
            Assert.That(effect.EffectDefinitionRef, Is.EqualTo(effectRef));
            Assert.That(effect.EffectMechanicsSnapshot, Is.EqualTo(snapshot));
            Assert.That(effect.SourceRef, Is.EqualTo(sourceRef));
            Assert.That(effect.TargetRef, Is.EqualTo(targetRef));
            Assert.That(effect.Status, Is.EqualTo(ActiveEffectStatus.Active));
            Assert.That(effect.StackCount, Is.EqualTo(1));
            Assert.That(effect.AppliedByUserId, Is.EqualTo(appliedByUserId));
            Assert.That(effect.AppliedAt, Is.EqualTo(Now));
            Assert.That(effect.ExpiresAt, Is.Null);
            Assert.That(effect.Revision, Is.EqualTo(1));
        }

        [Test] // TC-ACTIVEEFFECT-012
        public void ActiveEffect_RejectsAMechanicsSnapshotThatDoesNotMatchTheEffectDefinitionRef()
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            ContentDefinitionRef otherRef = NewEffectRef();
            var mismatchedSnapshot = new EffectMechanicsSnapshot(otherRef, 1, ContentDefinitionType.Effect, "{}");
            ActiveEffectSourceRef sourceRef = ActiveEffectSourceRef.ForGMDirect();
            ActiveEffectTargetRef targetRef = ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Now));

            Action act = () => new ActiveEffect(
                ActiveEffectId.NewId(Now), effectRef, mismatchedSnapshot, sourceRef, targetRef,
                ActiveEffectStatus.Active, 1, NewUserId(), Now, null, 1);
            Assert.Throws<ArgumentException>(act);
        }

        [TestCase(0L)] // TC-ACTIVEEFFECT-013
        [TestCase(-1L)]
        public void ActiveEffect_RejectsAStackCountBelowOne(long stackCount)
        {
            ContentDefinitionRef effectRef = NewEffectRef();
            var snapshot = new EffectMechanicsSnapshot(effectRef, 1, ContentDefinitionType.Effect, "{}");

            Action act = () => new ActiveEffect(
                ActiveEffectId.NewId(Now), effectRef, snapshot, ActiveEffectSourceRef.ForGMDirect(), ActiveEffectTargetRef.ForCharacter(CharacterId.NewId(Now)),
                ActiveEffectStatus.Active, stackCount, NewUserId(), Now, null, 1);
            Assert.Throws<ArgumentOutOfRangeException>(act);
        }

        [Test] // TC-ACTIVEEFFECT-014
        public void ActiveEffectSourceRef_ForItemAndForEquippedItem_RequireAValidItemRef()
        {
            InventoryItemRef itemRef = InventoryItemRef.ForStack(ItemStackId.NewId(Now));

            ActiveEffectSourceRef itemSource = ActiveEffectSourceRef.ForItem(itemRef);
            ActiveEffectSourceRef equippedSource = ActiveEffectSourceRef.ForEquippedItem(itemRef);

            Assert.That(itemSource.Kind, Is.EqualTo(ActiveEffectSourceKind.Item));
            Assert.That(itemSource.ItemRef, Is.EqualTo(itemRef));
            Assert.That(itemSource.IsValid, Is.True);
            Assert.That(equippedSource.Kind, Is.EqualTo(ActiveEffectSourceKind.EquippedItem));
            Assert.That(equippedSource.IsValid, Is.True);
            Action forItemAct = () => ActiveEffectSourceRef.ForItem(default);
            Action forEquippedItemAct = () => ActiveEffectSourceRef.ForEquippedItem(default);
            Assert.Throws<ArgumentException>(forItemAct);
            Assert.Throws<ArgumentException>(forEquippedItemAct);
        }

        [Test] // TC-ACTIVEEFFECT-015
        public void ActiveEffectSourceRef_ForActionAndForGMDirect_CarryNoItemReference_AndAreValid()
        {
            ActiveEffectSourceRef action = ActiveEffectSourceRef.ForAction();
            ActiveEffectSourceRef gmDirect = ActiveEffectSourceRef.ForGMDirect();

            Assert.That(action.Kind, Is.EqualTo(ActiveEffectSourceKind.Action));
            Assert.That(action.ItemRef.IsValid, Is.False);
            Assert.That(action.IsValid, Is.True, "Action/GMDirect sources are valid without any item reference");
            Assert.That(gmDirect.Kind, Is.EqualTo(ActiveEffectSourceKind.GMDirect));
            Assert.That(gmDirect.IsValid, Is.True);
        }

        [Test] // TC-ACTIVEEFFECT-016
        public void ActiveEffectTargetRef_ForCharacterAndForItemInstance_RequireTheirOwnId()
        {
            CharacterId characterId = CharacterId.NewId(Now);
            ItemInstanceId itemInstanceId = ItemInstanceId.NewId(Now);

            ActiveEffectTargetRef characterTarget = ActiveEffectTargetRef.ForCharacter(characterId);
            ActiveEffectTargetRef itemTarget = ActiveEffectTargetRef.ForItemInstance(itemInstanceId);

            Assert.That(characterTarget.Kind, Is.EqualTo(ActiveEffectTargetKind.Character));
            Assert.That(characterTarget.CharacterId, Is.EqualTo(characterId));
            Assert.That(characterTarget.IsValid, Is.True);
            Assert.That(itemTarget.Kind, Is.EqualTo(ActiveEffectTargetKind.ItemInstance));
            Assert.That(itemTarget.ItemInstanceId, Is.EqualTo(itemInstanceId));
            Assert.That(itemTarget.IsValid, Is.True);
            Action forCharacterAct = () => ActiveEffectTargetRef.ForCharacter(default);
            Action forItemInstanceAct = () => ActiveEffectTargetRef.ForItemInstance(default);
            Assert.Throws<ArgumentException>(forCharacterAct);
            Assert.Throws<ArgumentException>(forItemInstanceAct);
        }

        [Test] // TC-ACTIVEEFFECT-017
        public void ActiveEffectTargetRef_SceneObjectKind_IsDeclaredButUnconstructable()
        {
            // ADR-027 section 8.2 names a scene object as a supported ActiveEffect
            // target, but no SceneObject domain type exists anywhere in the
            // codebase today (ODY-S05-501's own finding) -- this enum value
            // exists for future extension only; default(ActiveEffectTargetRef)
            // is never a real SceneObject reference and must never report Valid.
            Assert.That(Enum.IsDefined(typeof(ActiveEffectTargetKind), ActiveEffectTargetKind.SceneObject), Is.True);
            Assert.That(default(ActiveEffectTargetRef).IsValid, Is.False);
        }

        [Test] // TC-ACTIVEEFFECT-018
        public void EffectMechanicsSnapshot_ConstructsWithValidFields_AndExposesThemUnchanged()
        {
            ContentDefinitionRef effectRef = NewEffectRef();

            var snapshot = new EffectMechanicsSnapshot(effectRef, 3, ContentDefinitionType.Effect, "{\"stack\":\"policy\"}");

            Assert.That(snapshot.SourceDefinitionRef, Is.EqualTo(effectRef));
            Assert.That(snapshot.DefinitionSnapshotVersion, Is.EqualTo(3));
            Assert.That(snapshot.ContentType, Is.EqualTo(ContentDefinitionType.Effect));
            Assert.That(snapshot.Payload, Is.EqualTo("{\"stack\":\"policy\"}"));
        }

        [Test] // TC-ACTIVEEFFECT-019
        public void EffectMechanicsSnapshot_RejectsAnEmptyPayload()
        {
            Action act = () => new EffectMechanicsSnapshot(NewEffectRef(), 1, ContentDefinitionType.Effect, "");
            Assert.Throws<ArgumentException>(act);
        }
    }
}
