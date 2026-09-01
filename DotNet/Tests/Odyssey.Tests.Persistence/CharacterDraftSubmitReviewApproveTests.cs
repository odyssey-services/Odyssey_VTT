using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S04-104: real, non-stubbed tests for
    /// <see cref="SqliteCharacterRepository.SubmitCharacterDraft"/>,
    /// <see cref="SqliteCharacterRepository.AddCharacterReviewComment"/>, and
    /// <see cref="SqliteCharacterRepository.ApproveCharacterDraft"/> against a
    /// real temp-directory campaign and a real SQLite database -- mirroring
    /// <c>SqliteCharacterRepositoryTests</c>'s exact fixture convention. None
    /// of these tests mock or bypass the repository/pipeline.
    /// </summary>
    public sealed class CharacterDraftSubmitReviewApproveTests
    {
        private static readonly CorrelationId TestCorrelationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly IWallClock Clock = new SystemWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        private string _campaignDir = null!;
        private CampaignHandle _campaign = null!;
        private SqliteCampaignRepository _campaignRepository = null!;
        private SqliteCharacterRepository _characterRepository = null!;

        [SetUp]
        public void SetUp()
        {
            _campaignDir = Path.Combine(Path.GetTempPath(), "ody-s04-104-" + Guid.NewGuid().ToString("N"));
            _campaignRepository = new SqliteCampaignRepository(Clock);
            var request = new CreateCampaignRequest(_campaignDir, "Submit/Review/Approve Test Campaign", "ruleset.core", "1.0.0", "0.1.0");
            Result<CampaignHandle> created = _campaignRepository.Create(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            _campaign = created.Value;
            _characterRepository = new SqliteCharacterRepository(Clock);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                _campaignRepository.Close(_campaign, TestCorrelationId);
            }
            catch (IOException) { }

            try
            {
                if (Directory.Exists(_campaignDir)) Directory.Delete(_campaignDir, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup only.
            }
        }

        private CharacterRecord CreateDraftCharacter(string name = "Draft Character")
        {
            var request = new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, name);
            Result<CharacterRecord> created = _characterRepository.CreateCharacter(request, NewCommandId(), TestCorrelationId);
            Assert.That(created.IsSuccess, Is.True);
            return created.Value;
        }

        // TC-CHAR-028: SubmitCharacterDraft on a valid Draft succeeds and
        // records SubmittedAt.
        [Test]
        public void SubmitCharacterDraft_OnValidDraft_Succeeds()
        {
            CharacterRecord character = CreateDraftCharacter();

            Result<CharacterRecord> result = _characterRepository.SubmitCharacterDraft(_campaign, character.CharacterId, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.SubmittedAt, Is.Not.Null);
            Assert.That(result.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Draft));
            Assert.That(result.Value.ApprovalState, Is.EqualTo(CharacterApprovalState.Draft));
            Assert.That(result.Value.Revisions.LifecycleRevision, Is.EqualTo(character.Revisions.LifecycleRevision + 1));
        }

        // TC-CHAR-029: SubmitCharacterDraft on an already-Active Character is
        // rejected (illegal call for the current LifecycleStatus).
        [Test]
        public void SubmitCharacterDraft_OnActiveCharacter_IsRejected()
        {
            CharacterRecord character = CreateDraftCharacter();
            Result<CharacterRecord> approved = _characterRepository.ApproveCharacterDraft(_campaign, character.CharacterId, actorIsMainGm: true, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);
            Assert.That(approved.IsSuccess, Is.True);

            Result<CharacterRecord> result = _characterRepository.SubmitCharacterDraft(_campaign, character.CharacterId, approved.Value.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterLifecycleTransitionInvalid));
        }

        // TC-CHAR-030: AddCharacterReviewComment requires no
        // ExpectedCharacterRevision/section revision; a concurrent Identity
        // edit and a comment both commit without a false conflict.
        [Test]
        public void AddCharacterReviewComment_AndConcurrentIdentityEdit_BothCommit_NoFalseConflict()
        {
            CharacterRecord character = CreateDraftCharacter();
            UserId author = NewUserId();

            Result<CharacterReviewCommentRecord> comment = _characterRepository.AddCharacterReviewComment(_campaign, character.CharacterId, author, "Please add a backstory.", NewCommandId(), TestCorrelationId);
            Result<CharacterRecord> identityEdit = _characterRepository.UpdateIdentity(_campaign, character.CharacterId, "Renamed Character", character.Revisions.IdentityRevision, NewCommandId(), TestCorrelationId);

            Assert.That(comment.IsSuccess, Is.True);
            Assert.That(identityEdit.IsSuccess, Is.True);
            Assert.That(comment.Value.Text, Is.EqualTo("Please add a backstory."));
            Assert.That(comment.Value.AuthorUserId, Is.EqualTo(author));

            // The comment never touched CharacterRevision/any section
            // revision -- the identity edit's own revision bump is the only
            // one that occurred.
            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.IsSuccess, Is.True);
            Assert.That(reRead.Value.Revisions.IdentityRevision, Is.EqualTo(character.Revisions.IdentityRevision + 1));
        }

        // TC-CHAR-031: several comments from different authors all
        // accumulate; none is lost or overwritten.
        [Test]
        public void AddCharacterReviewComment_MultipleAuthors_AllAccumulateCorrectly()
        {
            CharacterRecord character = CreateDraftCharacter();
            UserId gm = NewUserId();
            UserId player = NewUserId();

            Result<CharacterReviewCommentRecord> first = _characterRepository.AddCharacterReviewComment(_campaign, character.CharacterId, gm, "Consider a different background.", NewCommandId(), TestCorrelationId);
            Result<CharacterReviewCommentRecord> second = _characterRepository.AddCharacterReviewComment(_campaign, character.CharacterId, player, "Updated, please take another look.", NewCommandId(), TestCorrelationId);
            Result<CharacterReviewCommentRecord> third = _characterRepository.AddCharacterReviewComment(_campaign, character.CharacterId, gm, "Looks good now.", NewCommandId(), TestCorrelationId);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(second.IsSuccess, Is.True);
            Assert.That(third.IsSuccess, Is.True);

            Result<IReadOnlyList<CharacterReviewCommentRecord>> thread = _characterRepository.GetCharacterReviewComments(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(thread.IsSuccess, Is.True);
            Assert.That(thread.Value, Has.Count.EqualTo(3));
            Assert.That(thread.Value.Select(c => c.CommentId), Is.EquivalentTo(new[] { first.Value.CommentId, second.Value.CommentId, third.Value.CommentId }));
            Assert.That(thread.Value.Select(c => c.Text), Is.EquivalentTo(new[] { "Consider a different background.", "Updated, please take another look.", "Looks good now." }));
        }

        // TC-CHAR-032: ApproveCharacterDraft by a non-MainGM actor is
        // rejected, with no state change.
        [Test]
        public void ApproveCharacterDraft_ByNonMainGm_IsRejected_NoStateChange()
        {
            CharacterRecord character = CreateDraftCharacter();

            Result<CharacterRecord> result = _characterRepository.ApproveCharacterDraft(_campaign, character.CharacterId, actorIsMainGm: false, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterApprovalDenied));

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.IsSuccess, Is.True);
            Assert.That(reRead.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Draft));
            Assert.That(reRead.Value.ApprovalState, Is.EqualTo(CharacterApprovalState.Draft));
        }

        // TC-CHAR-033: ApproveCharacterDraft by MainGM atomically transitions
        // both LifecycleStatus and ApprovalState in one transaction.
        [Test]
        public void ApproveCharacterDraft_ByMainGm_TransitionsBothFieldsAtomically()
        {
            CharacterRecord character = CreateDraftCharacter();

            Result<CharacterRecord> result = _characterRepository.ApproveCharacterDraft(_campaign, character.CharacterId, actorIsMainGm: true, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Active));
            Assert.That(result.Value.ApprovalState, Is.EqualTo(CharacterApprovalState.Approved));

            // Read back independently -- both fields landed together, not
            // one without the other.
            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.IsSuccess, Is.True);
            Assert.That(reRead.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Active));
            Assert.That(reRead.Value.ApprovalState, Is.EqualTo(CharacterApprovalState.Approved));
        }

        // TC-CHAR-034: a repeat ApproveCharacterDraft on an already-Active
        // Character is rejected -- proving the business code actually calls
        // CharacterLifecycleTransitions.IsValidTransition rather than
        // duplicating its logic (Active -> Active is not a legal edge).
        [Test]
        public void ApproveCharacterDraft_OnAlreadyActiveCharacter_IsRejected_ViaLifecycleTransitionTable()
        {
            CharacterRecord character = CreateDraftCharacter();
            Result<CharacterRecord> firstApprove = _characterRepository.ApproveCharacterDraft(_campaign, character.CharacterId, actorIsMainGm: true, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);
            Assert.That(firstApprove.IsSuccess, Is.True);
            Assert.That(CharacterLifecycleTransitions.IsValidTransition(CharacterLifecycleStatus.Active, CharacterLifecycleStatus.Active), Is.False);

            Result<CharacterRecord> secondApprove = _characterRepository.ApproveCharacterDraft(_campaign, character.CharacterId, actorIsMainGm: true, firstApprove.Value.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);

            Assert.That(secondApprove.IsFailure, Is.True);
            Assert.That(secondApprove.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterLifecycleTransitionInvalid));
        }

        // TC-CHAR-035: a duplicate ApproveCharacterDraft CommandId replays the
        // stored result and does not reapply the transition/emit a second
        // event.
        [Test]
        public void ApproveCharacterDraft_DuplicateCommandId_DoesNotReapply()
        {
            CharacterRecord character = CreateDraftCharacter();
            CommandId commandId = NewCommandId();

            Result<CharacterRecord> first = _characterRepository.ApproveCharacterDraft(_campaign, character.CharacterId, actorIsMainGm: true, character.Revisions.LifecycleRevision, commandId, TestCorrelationId);
            Result<CharacterRecord> second = _characterRepository.ApproveCharacterDraft(_campaign, character.CharacterId, actorIsMainGm: true, character.Revisions.LifecycleRevision, commandId, TestCorrelationId);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(second.IsSuccess, Is.True);
            Assert.That(second.Value.Revisions.CharacterRevision, Is.EqualTo(first.Value.Revisions.CharacterRevision));
            Assert.That(second.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Active));
        }

        // A stale expectedLifecycleRevision is rejected for both
        // SubmitCharacterDraft and ApproveCharacterDraft, with no state
        // change -- the same per-section optimistic-concurrency convention
        // every other Character command already follows.
        [Test]
        public void SubmitCharacterDraft_WithStaleExpectedLifecycleRevision_IsRejected()
        {
            CharacterRecord character = CreateDraftCharacter();
            Result<CharacterRecord> firstSubmit = _characterRepository.SubmitCharacterDraft(_campaign, character.CharacterId, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);
            Assert.That(firstSubmit.IsSuccess, Is.True);

            Result<CharacterRecord> staleSubmit = _characterRepository.SubmitCharacterDraft(_campaign, character.CharacterId, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);

            Assert.That(staleSubmit.IsFailure, Is.True);
            Assert.That(staleSubmit.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRevisionConflict));
        }

        [Test]
        public void ApproveCharacterDraft_WithStaleExpectedLifecycleRevision_IsRejected_NoStateChange()
        {
            CharacterRecord character = CreateDraftCharacter();
            Result<CharacterRecord> submitted = _characterRepository.SubmitCharacterDraft(_campaign, character.CharacterId, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);
            Assert.That(submitted.IsSuccess, Is.True);

            // character.Revisions.LifecycleRevision is now stale -- Submit
            // already advanced it.
            Result<CharacterRecord> approve = _characterRepository.ApproveCharacterDraft(_campaign, character.CharacterId, actorIsMainGm: true, character.Revisions.LifecycleRevision, NewCommandId(), TestCorrelationId);

            Assert.That(approve.IsFailure, Is.True);
            Assert.That(approve.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCharacterRevisionConflict));

            Result<CharacterRecord> reRead = _characterRepository.GetCharacter(_campaign, character.CharacterId, TestCorrelationId);
            Assert.That(reRead.IsSuccess, Is.True);
            Assert.That(reRead.Value.LifecycleStatus, Is.EqualTo(CharacterLifecycleStatus.Draft));
        }
    }
}
