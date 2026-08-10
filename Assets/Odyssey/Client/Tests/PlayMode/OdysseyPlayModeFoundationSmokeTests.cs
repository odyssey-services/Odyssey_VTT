using System.Collections;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Odyssey.Tests.Unity.PlayMode
{
    public sealed class OdysseyPlayModeFoundationSmokeTests
    {
        [UnityTest]
        public IEnumerator BootstrapSceneCanBeLoadedByPath()
        {
            const string scenePath = "Assets/Odyssey/Client/Scenes/Bootstrap.unity";

            yield return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);

            Scene scene = SceneManager.GetActiveScene();
            Assert.That(scene.path, Is.EqualTo(scenePath));
            Assert.That(scene.isLoaded, Is.True);
        }
    }
}
