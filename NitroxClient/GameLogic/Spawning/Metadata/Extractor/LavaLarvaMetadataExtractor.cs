using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

public class LavaLarvaMetadataExtractor : EntityMetadataExtractor<AttachToVehicle, LavaLarvaMetadata>
{
    public override LavaLarvaMetadata Extract(AttachToVehicle attachToVehicle)
    {
        return LavaLarvas.CreateMetadata(attachToVehicle);
    }
}
