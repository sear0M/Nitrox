using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class LavaLarvaMetadataProcessor : EntityMetadataProcessor<LavaLarvaMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, LavaLarvaMetadata metadata)
    {
        // Not injected: LavaLarvas needs Entities, which needs the metadata processors
        Resolve<LavaLarvas>().ApplyAttachment(gameObject, metadata);
    }
}
