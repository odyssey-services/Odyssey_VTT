using Odyssey.Application.Versions;

namespace Odyssey.Unity.Client
{
    internal static partial class OdysseyGeneratedBuildIdentity
    {
        internal static BuildIdentity? LoadOrNull()
        {
            BuildIdentity? identity = null;
            GetGenerated(ref identity);
            return identity;
        }

        static partial void GetGenerated(ref BuildIdentity? identity);
    }

    public interface IBuildIdentityProvider
    {
        BuildIdentity? Current { get; }
    }

    public sealed class GeneratedBuildIdentityProvider : IBuildIdentityProvider
    {
        private readonly BuildIdentity? _identity;

        public GeneratedBuildIdentityProvider()
        {
            _identity = OdysseyGeneratedBuildIdentity.LoadOrNull();
        }

        public BuildIdentity? Current => _identity;
    }
}
