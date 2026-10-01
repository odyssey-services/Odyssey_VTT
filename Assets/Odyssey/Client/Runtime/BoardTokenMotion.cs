using System;
using System.Collections.Generic;
using Odyssey.Application.Persistence;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// ODY-S11-211: which token moves the board animates. Every render pass reports each token's authoritative
    /// position through <see cref="Observe"/>; a change the local user made on this board (drag, click-to-move, the
    /// rollback of their own denied move) was announced with <see cref="MarkLocal"/> and is shown instantly, any other
    /// change (another participant, a reload) eases from where the token was drawn to its new position over
    /// <see cref="OdyMotion.RemoteUpdateDurationMs"/>. Grabbing a token (<see cref="Cancel"/>) ends its animation at
    /// once, so the local drag is always 1:1. Pure state; the board drives <see cref="Advance"/> from its scheduler.
    /// </summary>
    public sealed class BoardTokenMotion
    {
        private readonly Dictionary<string, Motion> _active = new Dictionary<string, Motion>(StringComparer.Ordinal);
        private readonly HashSet<string> _local = new HashSet<string>(StringComparer.Ordinal);
        private readonly double _durationMs;

        public BoardTokenMotion(double durationMs = OdyMotion.RemoteUpdateDurationMs)
        {
            _durationMs = durationMs;
        }

        public bool AnyActive => _active.Count > 0;

        /// <summary>The next position change observed for this token is the local user's own: show it instantly.</summary>
        public void MarkLocal(string tokenKey)
        {
            if (string.IsNullOrEmpty(tokenKey)) return;
            _local.Add(tokenKey);
            _active.Remove(tokenKey);
        }

        /// <summary>The local user grabbed the token: stop animating it (it is drawn at its authoritative position).</summary>
        public void Cancel(string tokenKey) => _active.Remove(tokenKey);

        public bool IsAnimating(string tokenKey) => _active.ContainsKey(tokenKey);

        /// <summary>
        /// One render pass saw <paramref name="authoritative"/> for a token last rendered at
        /// <paramref name="previousAuthoritative"/> (<c>null</c> = not rendered before). Returns where to draw it now.
        /// </summary>
        public TokenPosition Observe(string tokenKey, TokenPosition? previousAuthoritative, TokenPosition authoritative, bool visible)
        {
            bool local = _local.Remove(tokenKey);
            if (local || !visible || !previousAuthoritative.HasValue)
            {
                _active.Remove(tokenKey);
                return authoritative;
            }

            if (_active.TryGetValue(tokenKey, out Motion? running))
            {
                // Same destination: a re-render mid-animation keeps going. New destination: continue from where it is drawn.
                if (running.Target.Equals(authoritative)) return running.Current;
                TokenPosition from = running.Current;
                return Start(tokenKey, from, authoritative);
            }

            if (previousAuthoritative.Value.Equals(authoritative)) return authoritative;
            return Start(tokenKey, previousAuthoritative.Value, authoritative);
        }

        /// <summary>Steps every running animation; finished ones are dropped. Returns whether any is still running.</summary>
        public bool Advance(double elapsedMs)
        {
            if (_active.Count == 0) return false;
            var finished = new List<string>();
            foreach (KeyValuePair<string, Motion> entry in _active)
            {
                entry.Value.Advance(elapsedMs);
                if (entry.Value.IsDone) finished.Add(entry.Key);
            }

            foreach (string key in finished) _active.Remove(key);
            return _active.Count > 0;
        }

        /// <summary>Where a token is drawn right now if it is animating.</summary>
        public bool TryGetDisplayed(string tokenKey, out TokenPosition position)
        {
            if (_active.TryGetValue(tokenKey, out Motion? motion))
            {
                position = motion.Current;
                return true;
            }

            position = default;
            return false;
        }

        /// <summary>Forgets tokens that are no longer rendered.</summary>
        public void RetainOnly(ICollection<string> renderedKeys)
        {
            var gone = new List<string>();
            foreach (string key in _active.Keys) if (!renderedKeys.Contains(key)) gone.Add(key);
            foreach (string key in gone) _active.Remove(key);
            _local.RemoveWhere(key => !renderedKeys.Contains(key));
        }

        private TokenPosition Start(string tokenKey, TokenPosition from, TokenPosition to)
        {
            var motion = new Motion(from, to, _durationMs);
            if (motion.IsDone)
            {
                _active.Remove(tokenKey);
                return to;
            }

            _active[tokenKey] = motion;
            return motion.Current;
        }

        private sealed class Motion
        {
            private readonly OdyTween _x;
            private readonly OdyTween _y;

            public Motion(TokenPosition from, TokenPosition to, double durationMs)
            {
                Target = to;
                _x = new OdyTween(from.X, to.X, durationMs);
                _y = new OdyTween(from.Y, to.Y, durationMs);
            }

            public TokenPosition Target { get; }
            public bool IsDone => _x.IsDone && _y.IsDone;
            public TokenPosition Current => new TokenPosition(_x.Current, _y.Current);

            public void Advance(double elapsedMs)
            {
                _x.Advance(elapsedMs);
                _y.Advance(elapsedMs);
            }
        }
    }
}
