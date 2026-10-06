using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     Where a lava larva is attached (to a Cyclops or a vehicle), as decided by its simulating player.
/// </summary>
[Serializable]
[DataContract]
public class LavaLarvaMetadata : EntityMetadata
{
    /// <summary>
    ///     Id of the entity holding the LavaLarvaTarget the larva is attached to, or null if the larva isn't attached.
    /// </summary>
    [DataMember(Order = 1)]
    public NitroxId? TargetId { get; }

    /// <summary>
    ///     Index of the larva's point in LavaLarvaTarget.attachPoints.
    /// </summary>
    [DataMember(Order = 2)]
    public int AttachPointIndex { get; }

    [IgnoreConstructor]
    protected LavaLarvaMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public LavaLarvaMetadata(NitroxId? targetId, int attachPointIndex)
    {
        TargetId = targetId;
        AttachPointIndex = attachPointIndex;
    }

    public override string ToString()
    {
        return $"[{nameof(LavaLarvaMetadata)} TargetId: {TargetId}, AttachPointIndex: {AttachPointIndex}]";
    }
}
