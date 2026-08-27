using System;
using Odyssey.Application.Networking.Session;
using Odyssey.Domain.Identity;

namespace Odyssey.Unity.Client
{
    public sealed class RoleSelection
    {
        public static readonly UserId DefaultPlayerUserId = UserId.Parse("user_00000000000000000000000000000002");
        public static readonly UserId DefaultMainGmUserId = UserId.Parse("user_00000000000000000000000000000001");
        public static readonly UserId DefaultObserverUserId = UserId.Parse("user_00000000000000000000000000000003");

        private BaselineRole _role;

        public RoleSelection()
            : this(DefaultPlayerUserId, DefaultMainGmUserId, DefaultObserverUserId, BaselineRole.Player)
        {
        }

        public RoleSelection(UserId playerUserId, UserId mainGmUserId, UserId observerUserId, BaselineRole initialRole)
        {
            if (!playerUserId.IsValid) throw new ArgumentException("Player UserId is required.", nameof(playerUserId));
            if (!mainGmUserId.IsValid) throw new ArgumentException("MainGM UserId is required.", nameof(mainGmUserId));
            if (!observerUserId.IsValid) throw new ArgumentException("Observer UserId is required.", nameof(observerUserId));
            EnsureKnownRole(initialRole);

            PlayerUserId = playerUserId;
            MainGmUserId = mainGmUserId;
            ObserverUserId = observerUserId;
            _role = initialRole;
        }

        public event Action<RoleSelectionSnapshot>? Changed;

        public UserId PlayerUserId { get; }
        public UserId MainGmUserId { get; }
        public UserId ObserverUserId { get; }
        public BaselineRole Role => _role;
        public UserId ActorUserId => UserIdFor(_role);
        public bool ActorIsMainGm => _role == BaselineRole.MainGM;
        public bool ActorCanCreateRoll => _role != BaselineRole.Observer;
        public RoleSelectionSnapshot Current => new RoleSelectionSnapshot(ActorUserId, ActorIsMainGm, ActorCanCreateRoll, _role);

        public void SelectRole(BaselineRole role)
        {
            EnsureKnownRole(role);
            if (_role == role) return;

            _role = role;
            Changed?.Invoke(Current);
        }

        public IDisposable Subscribe(Action<RoleSelectionSnapshot> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            Changed += handler;
            return new Subscription(this, handler);
        }

        private UserId UserIdFor(BaselineRole role)
        {
            switch (role)
            {
                case BaselineRole.MainGM:
                    return MainGmUserId;
                case BaselineRole.Player:
                    return PlayerUserId;
                case BaselineRole.Observer:
                    return ObserverUserId;
                default:
                    throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown baseline role.");
            }
        }

        private static void EnsureKnownRole(BaselineRole role)
        {
            if (!Enum.IsDefined(typeof(BaselineRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
        }

        private sealed class Subscription : IDisposable
        {
            private readonly RoleSelection _selection;
            private readonly Action<RoleSelectionSnapshot> _handler;
            private bool _disposed;

            public Subscription(RoleSelection selection, Action<RoleSelectionSnapshot> handler)
            {
                _selection = selection;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _selection.Changed -= _handler;
                _disposed = true;
            }
        }
    }

    public readonly struct RoleSelectionSnapshot
    {
        public RoleSelectionSnapshot(UserId actorUserId, bool actorIsMainGm, bool actorCanCreateRoll, BaselineRole role)
        {
            ActorUserId = actorUserId;
            ActorIsMainGm = actorIsMainGm;
            ActorCanCreateRoll = actorCanCreateRoll;
            Role = role;
        }

        public UserId ActorUserId { get; }
        public bool ActorIsMainGm { get; }
        public bool ActorCanCreateRoll { get; }
        public BaselineRole Role { get; }
    }
}
