using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Odyssey.Tests.Networking.HiddenDataBoundary.Harness
{
    /// <summary>
    /// Wire-shape DTOs for this harness only -- the actual bytes carried inside
    /// NetworkEnvelope.Payload over the real InProcessSessionTransport. Kept
    /// separate from ProjectionSnapshot/ProjectionDeltaBatch (the domain-shaped
    /// harness types) on purpose: ADR-017 section 11 requires wire payload to
    /// remain distinct from domain types, and only what actually crosses the
    /// wire can prove a hidden entity's bytes were never sent.
    /// </summary>
    public sealed class EntityWireDto
    {
        [JsonInclude] public string Id { get; set; } = string.Empty;
        [JsonInclude] public string DisplayName { get; set; } = string.Empty;
        [JsonInclude] public string Classification { get; set; } = string.Empty;
    }

    public sealed class SnapshotWireDto
    {
        [JsonInclude] public string SnapshotId { get; set; } = string.Empty;
        [JsonInclude] public string AudienceUserId { get; set; } = string.Empty;
        [JsonInclude] public long BaseSessionSequence { get; set; }
        [JsonInclude] public List<EntityWireDto> Entities { get; set; } = new();
        [JsonInclude] public List<string> AllowedCommands { get; set; } = new();
    }

    public sealed class OperationWireDto
    {
        [JsonInclude] public string Kind { get; set; } = string.Empty;
        [JsonInclude] public string TargetId { get; set; } = string.Empty;
        [JsonInclude] public EntityWireDto? Entity { get; set; }
    }

    public sealed class DeltaWireDto
    {
        [JsonInclude] public string AudienceUserId { get; set; } = string.Empty;
        [JsonInclude] public long SequenceFrom { get; set; }
        [JsonInclude] public long SequenceTo { get; set; }
        [JsonInclude] public List<OperationWireDto> Operations { get; set; } = new();
    }

    public static class WireCodec
    {
        public static byte[] EncodeSnapshot(ProjectionSnapshot snapshot)
        {
            var dto = new SnapshotWireDto
            {
                SnapshotId = snapshot.SnapshotId,
                AudienceUserId = snapshot.AudienceUserId,
                BaseSessionSequence = snapshot.BaseSessionSequence,
                AllowedCommands = new List<string>(snapshot.AllowedCommands)
            };
            foreach (var entity in snapshot.Entities)
            {
                dto.Entities.Add(new EntityWireDto { Id = entity.Id, DisplayName = entity.DisplayName, Classification = entity.Classification.ToString() });
            }

            return JsonSerializer.SerializeToUtf8Bytes(dto);
        }

        public static ProjectionSnapshot DecodeSnapshot(byte[] bytes)
        {
            var dto = JsonSerializer.Deserialize<SnapshotWireDto>(bytes)!;
            var entities = new List<GameEntity>();
            foreach (var entity in dto.Entities)
            {
                entities.Add(new GameEntity(entity.Id, entity.DisplayName, System.Enum.Parse<DataClassification>(entity.Classification)));
            }

            return new ProjectionSnapshot(dto.SnapshotId, dto.AudienceUserId, dto.BaseSessionSequence, entities, dto.AllowedCommands);
        }

        public static byte[] EncodeDelta(ProjectionDeltaBatch batch)
        {
            var dto = new DeltaWireDto
            {
                AudienceUserId = batch.AudienceUserId,
                SequenceFrom = batch.SequenceFrom,
                SequenceTo = batch.SequenceTo
            };
            foreach (var operation in batch.Operations)
            {
                dto.Operations.Add(new OperationWireDto
                {
                    Kind = operation.Kind.ToString(),
                    TargetId = operation.TargetId,
                    Entity = operation.Entity == null ? null : new EntityWireDto { Id = operation.Entity.Id, DisplayName = operation.Entity.DisplayName, Classification = operation.Entity.Classification.ToString() }
                });
            }

            return JsonSerializer.SerializeToUtf8Bytes(dto);
        }

        public static ProjectionDeltaBatch DecodeDelta(byte[] bytes)
        {
            var dto = JsonSerializer.Deserialize<DeltaWireDto>(bytes)!;
            var operations = new List<ProjectionOperation>();
            foreach (var operation in dto.Operations)
            {
                ProjectionOperationKind kind = System.Enum.Parse<ProjectionOperationKind>(operation.Kind);
                operations.Add(kind switch
                {
                    ProjectionOperationKind.AddEntity => ProjectionOperation.AddEntity(new GameEntity(operation.Entity!.Id, operation.Entity.DisplayName, System.Enum.Parse<DataClassification>(operation.Entity.Classification))),
                    ProjectionOperationKind.RemoveFromProjection => ProjectionOperation.RemoveFromProjection(operation.TargetId),
                    ProjectionOperationKind.AddCapability => ProjectionOperation.AddCapability(operation.TargetId),
                    ProjectionOperationKind.RemoveCapability => ProjectionOperation.RemoveCapability(operation.TargetId),
                    _ => throw new JsonException("Unknown operation kind.")
                });
            }

            return new ProjectionDeltaBatch(dto.AudienceUserId, dto.SequenceFrom, dto.SequenceTo, operations);
        }

        /// <summary>Raw UTF-8 text search over the exact wire bytes -- the strongest possible "never sent" check.</summary>
        public static bool WireBytesContain(byte[] bytes, string needle)
        {
            string text = System.Text.Encoding.UTF8.GetString(bytes);
            return text.Contains(needle, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
