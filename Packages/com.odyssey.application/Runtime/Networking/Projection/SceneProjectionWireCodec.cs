using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Odyssey.Application.Results;
using Odyssey.Application.Serialization;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Networking.Projection
{
    /// <summary>
    /// ODY-S02-010's ProjectionSnapshot wire codec, carried as NetworkEnvelope
    /// payload bytes (ADR-015 section 6.1) over the reliable channel.
    /// Hand-written canonical JSON, following the same
    /// JsonTextWriter/JsonTextReader array-walk pattern
    /// ManifestAndDiagnosticCodecs.cs's LogEventV1JsonCodec already
    /// establishes for array-bearing payloads -- SessionAdmissionWireCodecs's
    /// flat JsonObjectReader does not support arrays, and VisibleEntities is
    /// one. ADR-003 section 3: no reflection-based/auto-mapping
    /// serialization for production wire content.
    /// </summary>
    public static class ProjectionSnapshotWireCodec
    {
        public const string ContractType = "odyssey.projection.snapshot";
        private const int ContractVersionValue = 1;
        private const int MaxBytes = JsonPayloadLimits.EventPayloadBytes;

        public static Result<byte[]> Write(ProjectionSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            StringWriter stringWriter = new StringWriter(CultureInfo.InvariantCulture);
            using (JsonTextWriter writer = CreateWriter(stringWriter))
            {
                writer.WriteStartObject();
                WriteString(writer, "contractType", ContractType);
                WriteInt(writer, "contractVersion", ContractVersionValue);
                WriteString(writer, "snapshotId", snapshot.SnapshotId);
                WriteString(writer, "sessionId", snapshot.SessionId.ToString());
                WriteString(writer, "audienceUserId", snapshot.AudienceUserId.ToString());
                WriteLong(writer, "baseSessionSequence", snapshot.BaseSessionSequence);
                WriteLong(writer, "projectionRevision", snapshot.ProjectionRevision);
                WriteLong(writer, "permissionRevision", snapshot.PermissionRevision);
                WriteString(writer, "payloadHash", snapshot.PayloadHash);
                WriteString(writer, "createdAtHostTime", snapshot.CreatedAtHostTime.ToString());
                WriteEntities(writer, snapshot.VisibleEntities);
                writer.WriteEndObject();
            }

            return Result<byte[]>.Success(CanonicalJson.ToUtf8Bytes(stringWriter.ToString()));
        }

        public static Result<ProjectionSnapshot> Read(byte[] utf8Json)
        {
            try
            {
                if (utf8Json == null) throw new ArgumentNullException(nameof(utf8Json));
                Result structural = JsonObjectReader.ValidateJson(utf8Json, MaxBytes);
                if (structural.IsFailure) return Result<ProjectionSnapshot>.Failure(structural.Error);

                ReadModel model = ReadModel.Parse(utf8Json);
                if (model.ContractType != ContractType || model.ContractVersion != ContractVersionValue) return Result<ProjectionSnapshot>.Failure(SerializationFailures.UnsupportedContract());
                model.ValidateRequired();

                List<SceneEntity> entities = new List<SceneEntity>();
                foreach (EntityReadModel entityModel in model.Entities)
                {
                    UserId? assignedTo = entityModel.AssignedToUserId == null ? (UserId?)null : UserId.Parse(entityModel.AssignedToUserId);
                    entities.Add(new SceneEntity(entityModel.EntityId!, entityModel.DisplayName!, ParseVisibilityToken(entityModel.Visibility!), assignedTo));
                }

                ProjectionSnapshot snapshot = new ProjectionSnapshot(
                    model.SnapshotId!,
                    SessionId.Parse(model.SessionId!),
                    UserId.Parse(model.AudienceUserId!),
                    model.BaseSessionSequence,
                    model.ProjectionRevision,
                    model.PermissionRevision,
                    entities,
                    model.PayloadHash!,
                    UtcInstant.Parse(model.CreatedAtHostTime!));
                return Result<ProjectionSnapshot>.Success(snapshot);
            }
            catch (DecoderFallbackException)
            {
                return Result<ProjectionSnapshot>.Failure(SerializationFailures.InvalidPayload());
            }
            catch (FormatException)
            {
                return Result<ProjectionSnapshot>.Failure(SerializationFailures.InvalidPayload());
            }
            catch (JsonException)
            {
                return Result<ProjectionSnapshot>.Failure(SerializationFailures.InvalidPayload());
            }
            catch (ArgumentException)
            {
                return Result<ProjectionSnapshot>.Failure(SerializationFailures.InvalidPayload());
            }
        }

        private static JsonTextWriter CreateWriter(TextWriter textWriter)
        {
            return new JsonTextWriter(textWriter)
            {
                Formatting = Formatting.None,
                Culture = CultureInfo.InvariantCulture,
                FloatFormatHandling = FloatFormatHandling.Symbol,
                StringEscapeHandling = StringEscapeHandling.Default
            };
        }

        private static void WriteString(JsonTextWriter writer, string name, string value)
        {
            writer.WritePropertyName(name);
            writer.WriteValue(value);
        }

        private static void WriteInt(JsonTextWriter writer, string name, int value)
        {
            writer.WritePropertyName(name);
            writer.WriteValue(value);
        }

        private static void WriteLong(JsonTextWriter writer, string name, long value)
        {
            writer.WritePropertyName(name);
            writer.WriteValue(value);
        }

        private static void WriteNullableString(JsonTextWriter writer, string name, string? value)
        {
            writer.WritePropertyName(name);
            if (value == null) writer.WriteNull();
            else writer.WriteValue(value);
        }

        private static void WriteEntities(JsonTextWriter writer, IReadOnlyList<SceneEntity> entities)
        {
            writer.WritePropertyName("visibleEntities");
            writer.WriteStartArray();
            for (int index = 0; index < entities.Count; index++)
            {
                SceneEntity entity = entities[index];
                writer.WriteStartObject();
                WriteString(writer, "entityId", entity.EntityId);
                WriteString(writer, "displayName", entity.DisplayName);
                WriteString(writer, "visibility", ToVisibilityToken(entity.Visibility));
                WriteNullableString(writer, "assignedToUserId", entity.AssignedToUserId?.ToString());
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static string ToVisibilityToken(SceneEntityVisibility value)
        {
            switch (value)
            {
                case SceneEntityVisibility.Public: return "public";
                case SceneEntityVisibility.HiddenGameplay: return "hidden_gameplay";
                default: throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        private static SceneEntityVisibility ParseVisibilityToken(string value)
        {
            switch (value)
            {
                case "public": return SceneEntityVisibility.Public;
                case "hidden_gameplay": return SceneEntityVisibility.HiddenGameplay;
                default: throw new FormatException("Unknown SceneEntityVisibility token.");
            }
        }

        private static string ReadStringValue(JsonTextReader reader)
        {
            if (reader.TokenType != JsonToken.String) throw new JsonSerializationException("Expected string.");
            return (string)reader.Value!;
        }

        private static string? ReadNullableStringValue(JsonTextReader reader)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            if (reader.TokenType != JsonToken.String) throw new JsonSerializationException("Expected string or null.");
            return (string)reader.Value!;
        }

        private static int ReadInt32Value(JsonTextReader reader)
        {
            if (reader.TokenType != JsonToken.Integer) throw new JsonSerializationException("Expected integer.");
            return Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
        }

        private static long ReadInt64Value(JsonTextReader reader)
        {
            if (reader.TokenType != JsonToken.Integer) throw new JsonSerializationException("Expected integer.");
            return Convert.ToInt64(reader.Value, CultureInfo.InvariantCulture);
        }

        private static List<EntityReadModel> ReadEntities(JsonTextReader reader)
        {
            if (reader.TokenType != JsonToken.StartArray) throw new JsonSerializationException("Expected visibleEntities array.");
            List<EntityReadModel> entities = new List<EntityReadModel>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray) return entities;
                if (reader.TokenType != JsonToken.StartObject) throw new JsonSerializationException("Expected entity object.");
                entities.Add(ReadEntity(reader));
            }

            throw new JsonSerializationException("Unclosed visibleEntities array.");
        }

        private static EntityReadModel ReadEntity(JsonTextReader reader)
        {
            EntityReadModel model = new EntityReadModel();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                {
                    if (model.EntityId == null || model.DisplayName == null || model.Visibility == null) throw new JsonSerializationException("Missing entity field.");
                    return model;
                }

                if (reader.TokenType != JsonToken.PropertyName) throw new JsonSerializationException("Expected entity field.");
                string name = (string)reader.Value!;
                if (!seen.Add(name)) throw new JsonSerializationException("Duplicate entity field.");
                if (!reader.Read()) throw new JsonSerializationException("Missing entity value.");
                switch (name)
                {
                    case "entityId": model.EntityId = ReadStringValue(reader); break;
                    case "displayName": model.DisplayName = ReadStringValue(reader); break;
                    case "visibility": model.Visibility = ReadStringValue(reader); break;
                    case "assignedToUserId": model.AssignedToUserId = ReadNullableStringValue(reader); break;
                    default: throw new JsonSerializationException("Unexpected entity field.");
                }
            }

            throw new JsonSerializationException("Unclosed entity object.");
        }

        private sealed class EntityReadModel
        {
            public string? EntityId { get; set; }
            public string? DisplayName { get; set; }
            public string? Visibility { get; set; }
            public string? AssignedToUserId { get; set; }
        }

        private sealed class ReadModel
        {
            public string? ContractType { get; set; }
            public int ContractVersion { get; set; }
            public string? SnapshotId { get; set; }
            public string? SessionId { get; set; }
            public string? AudienceUserId { get; set; }
            public long BaseSessionSequence { get; set; }
            public long ProjectionRevision { get; set; }
            public long PermissionRevision { get; set; }
            public string? PayloadHash { get; set; }
            public string? CreatedAtHostTime { get; set; }
            public List<EntityReadModel> Entities { get; set; } = new List<EntityReadModel>();

            public void ValidateRequired()
            {
                if (ContractType == null || SnapshotId == null || SessionId == null || AudienceUserId == null || PayloadHash == null || CreatedAtHostTime == null)
                {
                    throw new JsonSerializationException("Missing required projection snapshot field.");
                }
            }

            public static ReadModel Parse(byte[] utf8Json)
            {
                JsonTextReader reader = new JsonTextReader(new StringReader(new UTF8Encoding(false, true).GetString(utf8Json)))
                {
                    DateParseHandling = DateParseHandling.None,
                    FloatParseHandling = FloatParseHandling.Decimal,
                    MaxDepth = JsonPayloadLimits.MaxDepth
                };
                ReadModel model = new ReadModel();
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                if (!reader.Read() || reader.TokenType != JsonToken.StartObject) throw new JsonSerializationException("Expected object.");
                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.EndObject)
                    {
                        if (reader.Read()) throw new JsonSerializationException("Trailing token.");
                        return model;
                    }

                    if (reader.TokenType != JsonToken.PropertyName) throw new JsonSerializationException("Expected property.");
                    string name = (string)reader.Value!;
                    if (!seen.Add(name)) throw new JsonSerializationException("Duplicate property.");
                    if (!reader.Read()) throw new JsonSerializationException("Missing value.");
                    switch (name)
                    {
                        case "contractType": model.ContractType = ReadStringValue(reader); break;
                        case "contractVersion": model.ContractVersion = ReadInt32Value(reader); break;
                        case "snapshotId": model.SnapshotId = ReadStringValue(reader); break;
                        case "sessionId": model.SessionId = ReadStringValue(reader); break;
                        case "audienceUserId": model.AudienceUserId = ReadStringValue(reader); break;
                        case "baseSessionSequence": model.BaseSessionSequence = ReadInt64Value(reader); break;
                        case "projectionRevision": model.ProjectionRevision = ReadInt64Value(reader); break;
                        case "permissionRevision": model.PermissionRevision = ReadInt64Value(reader); break;
                        case "payloadHash": model.PayloadHash = ReadStringValue(reader); break;
                        case "createdAtHostTime": model.CreatedAtHostTime = ReadStringValue(reader); break;
                        case "visibleEntities": model.Entities = ReadEntities(reader); break;
                        default: throw new JsonSerializationException("Unexpected property.");
                    }
                }

                throw new JsonSerializationException("Unclosed object.");
            }
        }
    }
}
