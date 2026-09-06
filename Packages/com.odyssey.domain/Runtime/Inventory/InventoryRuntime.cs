using System;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Domain.Inventory
{
    public readonly struct InventoryId : IEquatable<InventoryId>
    {
        private const string Prefix = "inv_";
        private const int HexLength = 32;
        private readonly string _value;

        private InventoryId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static InventoryId NewId(UtcInstant now) => new InventoryId(Prefix + Uuid7.NewHex32(now));
        public static bool TryParse(string? value, out InventoryId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new InventoryId(v));
        public static InventoryId Parse(string value) => TryParse(value, out InventoryId id) ? id : throw new FormatException("InventoryId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(InventoryId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is InventoryId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(InventoryId left, InventoryId right) => left.Equals(right);
        public static bool operator !=(InventoryId left, InventoryId right) => !left.Equals(right);
    }

    public readonly struct ItemInstanceId : IEquatable<ItemInstanceId>
    {
        private const string Prefix = "iinst_";
        private const int HexLength = 32;
        private readonly string _value;

        private ItemInstanceId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static ItemInstanceId NewId(UtcInstant now) => new ItemInstanceId(Prefix + Uuid7.NewHex32(now));
        public static bool TryParse(string? value, out ItemInstanceId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new ItemInstanceId(v));
        public static ItemInstanceId Parse(string value) => TryParse(value, out ItemInstanceId id) ? id : throw new FormatException("ItemInstanceId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(ItemInstanceId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is ItemInstanceId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(ItemInstanceId left, ItemInstanceId right) => left.Equals(right);
        public static bool operator !=(ItemInstanceId left, ItemInstanceId right) => !left.Equals(right);
    }

    public readonly struct ItemStackId : IEquatable<ItemStackId>
    {
        private const string Prefix = "istack_";
        private const int HexLength = 32;
        private readonly string _value;

        private ItemStackId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static ItemStackId NewId(UtcInstant now) => new ItemStackId(Prefix + Uuid7.NewHex32(now));
        public static bool TryParse(string? value, out ItemStackId id) => CanonicalId.TryParse(value, Prefix, HexLength, out id, static v => new ItemStackId(v));
        public static ItemStackId Parse(string value) => TryParse(value, out ItemStackId id) ? id : throw new FormatException("ItemStackId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(ItemStackId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is ItemStackId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(ItemStackId left, ItemStackId right) => left.Equals(right);
        public static bool operator !=(ItemStackId left, ItemStackId right) => !left.Equals(right);
    }

    public enum InventoryOwnerKind
    {
        Character = 1,
        Scene = 2
    }

    public readonly struct InventoryOwnerRef : IEquatable<InventoryOwnerRef>
    {
        private InventoryOwnerRef(InventoryOwnerKind kind, string targetRef, string? locationKey)
        {
            Kind = kind;
            TargetRef = targetRef;
            LocationKey = locationKey;
        }

        public InventoryOwnerKind Kind { get; }
        public string TargetRef { get; }
        public string? LocationKey { get; }
        public bool IsValid => Enum.IsDefined(typeof(InventoryOwnerKind), Kind) && !string.IsNullOrEmpty(TargetRef) && (Kind != InventoryOwnerKind.Scene || IsToken(LocationKey));

        public static InventoryOwnerRef ForCharacter(CharacterId characterId)
        {
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            return new InventoryOwnerRef(InventoryOwnerKind.Character, characterId.ToString(), null);
        }

        public static InventoryOwnerRef ForScene(SceneId sceneId, string locationKey)
        {
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));
            if (!IsToken(locationKey)) throw new ArgumentException("Location key is required and must be canonical.", nameof(locationKey));
            return new InventoryOwnerRef(InventoryOwnerKind.Scene, sceneId.ToString(), locationKey);
        }

        public bool Equals(InventoryOwnerRef other) => Kind == other.Kind && string.Equals(TargetRef, other.TargetRef, StringComparison.Ordinal) && string.Equals(LocationKey, other.LocationKey, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is InventoryOwnerRef other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Kind, TargetRef == null ? 0 : StringComparer.Ordinal.GetHashCode(TargetRef), LocationKey == null ? 0 : StringComparer.Ordinal.GetHashCode(LocationKey));

        internal static bool IsToken(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > 96 || value.Trim() != value) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-')) return false;
            }

            return true;
        }
    }

    public enum InventoryLocationKind
    {
        Contained = 1,
        Equipped = 2,
        SceneDropped = 3,
        Other = 4
    }

    public readonly struct InventoryLocationRef : IEquatable<InventoryLocationRef>
    {
        private InventoryLocationRef(InventoryLocationKind kind, string targetRef, string detailRef)
        {
            Kind = kind;
            TargetRef = targetRef;
            DetailRef = detailRef;
        }

        public InventoryLocationKind Kind { get; }
        public string TargetRef { get; }
        public string DetailRef { get; }
        public bool IsValid => Enum.IsDefined(typeof(InventoryLocationKind), Kind) && !string.IsNullOrEmpty(TargetRef) && !string.IsNullOrEmpty(DetailRef);

        public static InventoryLocationRef Contained(InventoryId inventoryId, string containerKey)
        {
            if (!inventoryId.IsValid) throw new ArgumentException("InventoryId is required.", nameof(inventoryId));
            if (!InventoryOwnerRef.IsToken(containerKey)) throw new ArgumentException("Container key is required and must be canonical.", nameof(containerKey));
            return new InventoryLocationRef(InventoryLocationKind.Contained, inventoryId.ToString(), containerKey);
        }

        public static InventoryLocationRef Equipped(InventoryId inventoryId, string equipmentSlotRef)
        {
            if (!inventoryId.IsValid) throw new ArgumentException("InventoryId is required.", nameof(inventoryId));
            if (!InventoryOwnerRef.IsToken(equipmentSlotRef)) throw new ArgumentException("Equipment slot ref is required and must be canonical.", nameof(equipmentSlotRef));
            return new InventoryLocationRef(InventoryLocationKind.Equipped, inventoryId.ToString(), equipmentSlotRef);
        }

        public static InventoryLocationRef SceneDropped(SceneId sceneId, string locationKey)
        {
            if (!sceneId.IsValid) throw new ArgumentException("SceneId is required.", nameof(sceneId));
            if (!InventoryOwnerRef.IsToken(locationKey)) throw new ArgumentException("Location key is required and must be canonical.", nameof(locationKey));
            return new InventoryLocationRef(InventoryLocationKind.SceneDropped, sceneId.ToString(), locationKey);
        }

        public static InventoryLocationRef Other(string locationKind, string targetRef)
        {
            if (!InventoryOwnerRef.IsToken(locationKind)) throw new ArgumentException("Location kind is required and must be canonical.", nameof(locationKind));
            if (string.IsNullOrWhiteSpace(targetRef) || targetRef.Length > 128 || targetRef.Trim() != targetRef) throw new ArgumentException("Target ref is required.", nameof(targetRef));
            return new InventoryLocationRef(InventoryLocationKind.Other, locationKind, targetRef);
        }

        public bool Equals(InventoryLocationRef other) => Kind == other.Kind && string.Equals(TargetRef, other.TargetRef, StringComparison.Ordinal) && string.Equals(DetailRef, other.DetailRef, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is InventoryLocationRef other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Kind, TargetRef == null ? 0 : StringComparer.Ordinal.GetHashCode(TargetRef), DetailRef == null ? 0 : StringComparer.Ordinal.GetHashCode(DetailRef));
    }

    public enum InventoryItemRefKind
    {
        ItemInstance = 1,
        ItemStack = 2
    }

    public readonly struct InventoryItemRef : IEquatable<InventoryItemRef>
    {
        public InventoryItemRef(InventoryItemRefKind kind, ItemInstanceId itemInstanceId, ItemStackId itemStackId)
        {
            bool hasInstance = itemInstanceId.IsValid;
            bool hasStack = itemStackId.IsValid;
            if (!Enum.IsDefined(typeof(InventoryItemRefKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (kind == InventoryItemRefKind.ItemInstance && (!hasInstance || hasStack)) throw new ArgumentException("An item-instance ref must contain exactly one ItemInstanceId.");
            if (kind == InventoryItemRefKind.ItemStack && (!hasStack || hasInstance)) throw new ArgumentException("An item-stack ref must contain exactly one ItemStackId.");

            Kind = kind;
            ItemInstanceId = itemInstanceId;
            ItemStackId = itemStackId;
        }

        public InventoryItemRefKind Kind { get; }
        public ItemInstanceId ItemInstanceId { get; }
        public ItemStackId ItemStackId { get; }
        public bool IsValid => (Kind == InventoryItemRefKind.ItemInstance && ItemInstanceId.IsValid && !ItemStackId.IsValid) || (Kind == InventoryItemRefKind.ItemStack && ItemStackId.IsValid && !ItemInstanceId.IsValid);
        public static InventoryItemRef ForInstance(ItemInstanceId itemInstanceId) => new InventoryItemRef(InventoryItemRefKind.ItemInstance, itemInstanceId, default);
        public static InventoryItemRef ForStack(ItemStackId itemStackId) => new InventoryItemRef(InventoryItemRefKind.ItemStack, default, itemStackId);
        public bool Equals(InventoryItemRef other) => Kind == other.Kind && ItemInstanceId.Equals(other.ItemInstanceId) && ItemStackId.Equals(other.ItemStackId);
        public override bool Equals(object? obj) => obj is InventoryItemRef other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Kind, ItemInstanceId, ItemStackId);
    }

    public readonly struct ItemMechanicsSnapshot : IEquatable<ItemMechanicsSnapshot>
    {
        public ItemMechanicsSnapshot(ContentDefinitionRef sourceDefinitionRef, long definitionSnapshotVersion, ContentDefinitionType contentType, string payload)
        {
            if (!sourceDefinitionRef.IsValid) throw new ArgumentException("Source definition ref is required.", nameof(sourceDefinitionRef));
            if (definitionSnapshotVersion < 1) throw new ArgumentOutOfRangeException(nameof(definitionSnapshotVersion));
            if (!Enum.IsDefined(typeof(ContentDefinitionType), contentType)) throw new ArgumentOutOfRangeException(nameof(contentType));
            if (string.IsNullOrWhiteSpace(payload)) throw new ArgumentException("Payload is required.", nameof(payload));

            SourceDefinitionRef = sourceDefinitionRef;
            DefinitionSnapshotVersion = definitionSnapshotVersion;
            ContentType = contentType;
            Payload = payload;
        }

        public ContentDefinitionRef SourceDefinitionRef { get; }
        public long DefinitionSnapshotVersion { get; }
        public ContentDefinitionType ContentType { get; }
        public string Payload { get; }
        public bool Equals(ItemMechanicsSnapshot other) => SourceDefinitionRef.Equals(other.SourceDefinitionRef) && DefinitionSnapshotVersion == other.DefinitionSnapshotVersion && ContentType == other.ContentType && string.Equals(Payload, other.Payload, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is ItemMechanicsSnapshot other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(SourceDefinitionRef, DefinitionSnapshotVersion, ContentType, Payload == null ? 0 : StringComparer.Ordinal.GetHashCode(Payload));
    }

    public readonly struct ItemStackQuantity : IEquatable<ItemStackQuantity>, IComparable<ItemStackQuantity>
    {
        private ItemStackQuantity(long value) => Value = value;
        public long Value { get; }
        public bool IsValid => Value >= 1;
        public static ItemStackQuantity Create(long value) => value >= 1 ? new ItemStackQuantity(value) : throw new ArgumentOutOfRangeException(nameof(value), "Live stack quantity must be positive.");
        public int CompareTo(ItemStackQuantity other) => Value.CompareTo(other.Value);
        public bool Equals(ItemStackQuantity other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is ItemStackQuantity other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
