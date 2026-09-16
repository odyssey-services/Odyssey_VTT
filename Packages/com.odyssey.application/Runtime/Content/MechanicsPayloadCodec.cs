using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Content
{
    /// <summary>
    /// ODY-S06-106: `ADR-030` §7.1's own decode path for `AbilityDefinition.MechanicsPayloadRef`/
    /// `EffectDefinition.MechanicsPayloadRef`'s own JSON envelope -- deliberately NOT folded into
    /// `TypedDefinitionCodec.DecodeAbility`/`DecodeEffect`, since those decode the already-accepted
    /// CATALOG SHAPE (`ODY-S05-105`/`ADR-027`), while this payload is MECHANICS EXECUTION content this
    /// ADR alone introduces -- conflating the two would make a catalog-shape change and a
    /// mechanics-primitive-schema change the same versioned artifact when they are not (`ADR-030` §7.1's
    /// own explicit reasoning). Reuses `TypedDefinitionCodec`'s own `schemaVersion`-gated envelope
    /// convention (`RequireSupportedSchemaVersion`, `ODY-S05-105`'s own amendment) as the PATTERN, not
    /// the code -- a private helper cannot be shared across classes, so this codec's own small
    /// equivalent check is a deliberate, narrow, documented duplication of that pattern only.
    ///
    /// Envelope shape (`ADR-030` §7.1):
    /// <code>
    /// {
    ///   "schemaVersion": 1,
    ///   "primitives": [
    ///     { "kind": "AdjustResource", "resourceKind": "health", "amountFormula": "-2d6" },
    ///     { "kind": "ApplyEffect", "effectDefinitionRef": "cdef_0123456789abcdef0123456789abcdef/1" }
    ///   ]
    /// }
    /// </code>
    /// </summary>
    public static class MechanicsPayloadCodec
    {
        private const int SchemaVersion = 1;
        private const string AdjustResourceKind = "AdjustResource";
        private const string ApplyEffectKind = "ApplyEffect";

        public static string EncodePrimitives(MechanicsPrimitiveEnvelope envelope)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));

            var array = new JArray();
            foreach (MechanicsPrimitive primitive in envelope.Primitives)
            {
                switch (primitive)
                {
                    case AdjustResourcePrimitive adjust:
                        array.Add(new JObject { ["kind"] = AdjustResourceKind, ["resourceKind"] = adjust.ResourceKind.ToString(), ["amountFormula"] = adjust.AmountFormula });
                        break;
                    case ApplyEffectPrimitive applyEffect:
                        array.Add(new JObject { ["kind"] = ApplyEffectKind, ["effectDefinitionRef"] = applyEffect.EffectDefinitionRef.ToString() });
                        break;
                    default:
                        throw new ArgumentException("Unrecognized MechanicsPrimitive type.", nameof(envelope));
                }
            }

            var root = new JObject { ["schemaVersion"] = envelope.SchemaVersion, ["primitives"] = array };
            return root.ToString(Formatting.None);
        }

        public static Result<MechanicsPrimitiveEnvelope> DecodePrimitives(string payloadJson, CorrelationId correlationId)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return Result<MechanicsPrimitiveEnvelope>.Failure(MechanicsPayloadCodecFailures.MalformedPayload(correlationId));
            }

            try
            {
                JObject root = JObject.Parse(payloadJson);
                JToken? schemaVersionToken = root["schemaVersion"];
                if (schemaVersionToken == null || schemaVersionToken.Type != JTokenType.Integer || (long)schemaVersionToken != SchemaVersion)
                {
                    throw new FormatException("missing or unsupported schemaVersion");
                }

                var primitives = new List<MechanicsPrimitive>();
                JToken? primitivesToken = root["primitives"];
                if (primitivesToken != null)
                {
                    foreach (JToken entry in (JArray)primitivesToken)
                    {
                        string kind = (string)entry["kind"]!;
                        switch (kind)
                        {
                            case AdjustResourceKind:
                                ResourceDefinitionId resourceKind = ResourceDefinitionId.Parse((string)entry["resourceKind"]!);
                                string amountFormula = (string)entry["amountFormula"]!;
                                primitives.Add(new AdjustResourcePrimitive(resourceKind, amountFormula));
                                break;

                            case ApplyEffectKind:
                                ContentDefinitionRef effectRef = ContentDefinitionRef.Parse((string)entry["effectDefinitionRef"]!);
                                primitives.Add(new ApplyEffectPrimitive(effectRef));
                                break;

                            default:
                                throw new FormatException("unrecognized mechanics primitive kind: " + kind);
                        }
                    }
                }

                return Result<MechanicsPrimitiveEnvelope>.Success(new MechanicsPrimitiveEnvelope(SchemaVersion, primitives));
            }
            catch (Exception ex) when (IsMalformedPayloadException(ex))
            {
                return Result<MechanicsPrimitiveEnvelope>.Failure(MechanicsPayloadCodecFailures.MalformedPayload(correlationId));
            }
        }

        /// <summary>Mirrors `TypedDefinitionCodec`'s own identical exception allow-list -- every exception a malformed/hostile JSON payload or an unexpected missing/mistyped field can throw during decode, none of which should ever leak past this codec as a raw exception/message.</summary>
        private static bool IsMalformedPayloadException(Exception ex) =>
            ex is JsonException
            || ex is FormatException
            || ex is ArgumentException
            || ex is InvalidCastException
            || ex is NullReferenceException
            || ex is KeyNotFoundException
            || ex is IndexOutOfRangeException;
    }

    /// <summary>ODY-S06-106: codec-level failures, mirroring `TypedDefinitionCodecFailures`'s own exact convention. Reuses `ErrorCodes.ContentCatalogTypedDefinitionMalformedPayload` rather than minting a new registry entry -- the same underlying condition (a malformed typed-content JSON payload), and adding a new `ErrorCodes` row is outside this task's own allowed paths.</summary>
    public static class MechanicsPayloadCodecFailures
    {
        public static Error MalformedPayload(CorrelationId correlationId) => TypedDefinitionCodecFailures.MalformedPayload(correlationId);
    }
}
