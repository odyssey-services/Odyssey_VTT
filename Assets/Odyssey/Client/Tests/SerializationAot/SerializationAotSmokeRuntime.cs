#if ODYSSEY_SERIALIZATION_AOT_SMOKE
using Odyssey.Application.Results;
using Odyssey.Application.Serialization;
using UnityEngine;
using UnityApplication = UnityEngine.Application;

namespace Odyssey.Tests.SerializationAot
{
    internal static class SerializationAotSmokeRuntime
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Run()
        {
            Result<SerializationSmokeResult> result = SerializationSmoke.Run();
            if (result.IsFailure)
            {
                Debug.LogError("serialization-aot-smoke FAIL " + result.Error.Code);
                UnityApplication.Quit(1);
                return;
            }

            Debug.Log("serialization-aot-smoke PASS payloadHash=" + result.Value.PayloadHash + " fingerprint=" + result.Value.Fingerprint + " diagnosticHash=" + result.Value.DiagnosticHash + " manifestHash=" + result.Value.ManifestHash);
            UnityApplication.Quit(0);
        }
    }
}
#endif
