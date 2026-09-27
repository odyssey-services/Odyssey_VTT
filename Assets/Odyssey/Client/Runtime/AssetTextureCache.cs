using System;
using System.Collections.Generic;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;
using UnityEngine;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// ODY-S08-103: the <c>ReadAssetContent</c> + <c>ImageConversion.LoadImage</c> + per-<see cref="AssetId"/>
    /// cache pattern <see cref="BoardScreenPresenter"/> established in <c>ODY-S08-101</c>, extracted so
    /// <see cref="AssetPoolPresenter"/> can reuse it verbatim instead of a second, independent texture-loading
    /// implementation -- both presenters now call this type rather than either owning its own copy.
    /// <see cref="AssetId"/> is minted fresh by every <c>RegisterAsset</c> call, so an id's content never
    /// changes and no revision-based cache invalidation is needed; only successful loads are cached, a
    /// failed load is retried on the next call. One instance per presenter lifetime (not shared across
    /// presenters), matching the lifetime <see cref="BoardScreenPresenter"/>'s own cache already had.
    /// </summary>
    public sealed class AssetTextureCache : IDisposable
    {
        private readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private bool _disposed;

        public Result<Texture2D> Load(ISceneRepository sceneRepository, CampaignHandle campaign, AssetId assetId, CorrelationId correlationId)
        {
            string key = assetId.ToString();
            if (_cache.TryGetValue(key, out Texture2D? cached) && cached != null)
            {
                return Result<Texture2D>.Success(cached);
            }

            Result<byte[]> content = sceneRepository.ReadAssetContent(campaign, assetId, correlationId);
            if (content.IsFailure)
            {
                return Result<Texture2D>.Failure(content.Error);
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, content.Value))
            {
                DestroyTexture(texture);
                return Result<Texture2D>.Failure(BoardScreenErrors.TextureDecodeFailed());
            }

            _cache[key] = texture;
            return Result<Texture2D>.Success(texture);
        }

        public void Dispose()
        {
            if (_disposed) return;
            foreach (Texture2D texture in _cache.Values) DestroyTexture(texture);
            _cache.Clear();
            _disposed = true;
        }

        private static void DestroyTexture(Texture2D texture)
        {
            if (texture == null) return;
            if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(texture);
            else UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
