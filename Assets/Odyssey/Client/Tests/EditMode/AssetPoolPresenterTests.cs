using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;
using Odyssey.Persistence.Sqlite;
using Odyssey.Unity.Client;
using UnityEngine;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>
    /// ODY-S08-103: the asset pool's own upload flow, exercised through <see cref="AssetPoolPresenter.UploadFromDialog"/>
    /// directly with an injected fake dialog delegate -- never a real (or, in the Editor, an
    /// <c>EditorUtility.OpenFilePanel</c>-backed) file dialog. By the same testability precedent
    /// <see cref="BoardScreenPresenter.SelectToken"/>/<see cref="BoardScreenPresenter.TryMoveSelectedTokenTo"/>
    /// already established, and the same real-<see cref="SqliteSceneRepository"/>-over-a-temp-campaign
    /// convention every test in this suite uses.
    /// </summary>
    public sealed class AssetPoolPresenterTests
    {
        private static CorrelationId TestCorrelationId => CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly UnityWallClock Clock = new UnityWallClock();
        private static CommandId NewCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId NewUserId() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        private sealed class Fixture : IDisposable
        {
            public readonly TemporaryDirectory Directory = new TemporaryDirectory();
            public readonly SqliteCampaignRepository CampaignRepository;
            public readonly SqliteSceneRepository SceneRepository;
            public readonly SqliteObstacleRepository ObstacleRepository;
            public readonly CampaignHandle Campaign;
            public readonly SceneId SceneId;
            public readonly GameObject GameObject;
            public readonly UIDocument Document;
            public readonly BoardScreenPresenter Board;

            public Fixture()
            {
                CampaignRepository = new SqliteCampaignRepository(Clock);
                Campaign = CampaignRepository.Create(new CreateCampaignRequest(Directory.Path, "Asset Pool Test Campaign", "ruleset.core", "1.0.0", "0.1.0", global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost()), NewCommandId(), TestCorrelationId).Value;
                SceneRepository = new SqliteSceneRepository(Clock);
                ObstacleRepository = new SqliteObstacleRepository(Clock);
                SceneId = SceneRepository.CreateScene(Campaign, "Test Scene", NewCommandId(), TestCorrelationId).Value.SceneId;

                GameObject = new GameObject("Asset Pool Document");
                Document = GameObject.AddComponent<UIDocument>();
                Board = new BoardScreenPresenter(Document, SceneRepository, Campaign, CampaignRepository, ObstacleRepository, SceneId, NewUserId());
                Assert.That(Board.Initialize().IsSuccess, Is.True);
            }

            public AssetPoolPresenter BuildPool(Func<string?>? openImageFile = null) => new AssetPoolPresenter(Document, SceneRepository, Campaign, Board, openImageFile);

            public void Dispose()
            {
                Board.Dispose();
                UnityEngine.Object.DestroyImmediate(GameObject);
                CampaignRepository.Close(Campaign, TestCorrelationId);
                Directory.Dispose();
            }
        }

        private static string WriteTempImageSource(string name, byte[] content)
        {
            string path = Path.Combine(Path.GetTempPath(), "ody-s08-103-pool-" + Guid.NewGuid().ToString("N") + "-" + name);
            File.WriteAllBytes(path, content);
            return path;
        }

        [Test] // TC-BOARD-066
        public void UploadFromDialog_WithAChosenFile_RegistersIt_AndTheNewAssetAppearsInThePool()
        {
            using var fixture = new Fixture();
            string sourceFile = WriteTempImageSource("uploaded.png", System.Text.Encoding.UTF8.GetBytes("uploaded bytes " + Guid.NewGuid().ToString("N")));
            try
            {
                AssetPoolPresenter pool = fixture.BuildPool(() => sourceFile);
                VisualElement view = pool.BuildView();
                Assert.That(pool.Assets, Is.Empty, "the pool starts empty");

                Result<AssetManifestEntryRecord>? uploaded = pool.UploadFromDialog();

                Assert.That(uploaded, Is.Not.Null, "a chosen file must not be treated as a cancel");
                Assert.That(uploaded!.Value.IsSuccess, Is.True);
                Assert.That(pool.Assets.Select(a => a.AssetId), Is.EqualTo(new[] { uploaded.Value.Value.AssetId }));

                Result<System.Collections.Generic.IReadOnlyList<AssetManifestEntryRecord>> persisted = fixture.SceneRepository.ListAssets(fixture.Campaign, TestCorrelationId);
                Assert.That(persisted.Value.Select(a => a.AssetId), Is.EqualTo(new[] { uploaded.Value.Value.AssetId }), "the upload must actually be registered, not just shown locally");

                VisualElement? item = view.Q<VisualElement>("asset-pool-item-" + uploaded.Value.Value.AssetId);
                Assert.That(item, Is.Not.Null, "the newly uploaded asset must appear as an item in the pool panel");
            }
            finally
            {
                File.Delete(sourceFile);
            }
        }

        [Test] // TC-BOARD-070
        public void UploadFromDialog_WhenTheUserCancels_NeverCallsRegisterAsset_AndLeavesThePoolIntact()
        {
            using var fixture = new Fixture();
            string preexistingSource = WriteTempImageSource("preexisting.png", System.Text.Encoding.UTF8.GetBytes("preexisting " + Guid.NewGuid().ToString("N")));
            try
            {
                Result<AssetManifestEntryRecord> preexisting = fixture.SceneRepository.RegisterAsset(fixture.Campaign, preexistingSource, NewCommandId(), TestCorrelationId);
                Assert.That(preexisting.IsSuccess, Is.True);

                AssetPoolPresenter pool = fixture.BuildPool(() => null); // simulates the user closing/cancelling the dialog
                VisualElement view = pool.BuildView();
                Assert.That(pool.Assets.Select(a => a.AssetId), Is.EqualTo(new[] { preexisting.Value.AssetId }));

                Result<AssetManifestEntryRecord>? result = pool.UploadFromDialog();

                Assert.That(result, Is.Null, "a cancelled dialog must be reported as no result at all, not a failure");
                Result<System.Collections.Generic.IReadOnlyList<AssetManifestEntryRecord>> persisted = fixture.SceneRepository.ListAssets(fixture.Campaign, TestCorrelationId);
                Assert.That(persisted.Value.Select(a => a.AssetId), Is.EqualTo(new[] { preexisting.Value.AssetId }), "cancelling must never call RegisterAsset -- the manifest must be exactly what it was before");
                Assert.That(pool.Assets.Select(a => a.AssetId), Is.EqualTo(new[] { preexisting.Value.AssetId }));

                // The panel must still work normally afterwards -- a cancel must not leave it broken.
                Assert.That(pool.Refresh().IsSuccess, Is.True);
                Assert.That(view.Q<VisualElement>("asset-pool-item-" + preexisting.Value.AssetId), Is.Not.Null);
            }
            finally
            {
                File.Delete(preexistingSource);
            }
        }

        [Test] // TC-BOARD-071
        public void Pool_UsesOnlyPublicConstructorInjectedDependencies_NoRealDialogIsEverInvoked()
        {
            using var fixture = new Fixture();
            bool dialogInvoked = false;
            AssetPoolPresenter pool = fixture.BuildPool(() =>
            {
                dialogInvoked = true;
                return null;
            });
            pool.BuildView();

            Assert.That(dialogInvoked, Is.False, "building the view and refreshing must never itself open a dialog");

            pool.UploadFromDialog();

            Assert.That(dialogInvoked, Is.True, "UploadFromDialog is the one and only place the injected dialog delegate is called");
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "odyssey-asset-pool-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    try
                    {
                        if (Directory.Exists(Path)) Directory.Delete(Path, true);
                        return;
                    }
                    catch (IOException)
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                }
            }
        }
    }
}
