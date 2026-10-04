using System.Collections;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using NitroxClient.GameLogic.Spawning.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning;

public class PathBasedChildEntitySpawner : SyncEntitySpawner<PathBasedChildEntity>
{
    protected override IEnumerator SpawnAsync(PathBasedChildEntity entity, TaskResult<Optional<GameObject>> result)
    {
        SpawnSync(entity, result);
        yield break;
    }

    protected override bool SpawnSync(PathBasedChildEntity entity, TaskResult<Optional<GameObject>> result)
    {
        Optional<GameObject> owner = NitroxEntity.GetObjectFrom(entity.ParentId);

        if (!owner.HasValue)
        {
            Log.Error($"Unable to find parent entity: {entity}");
            result.Set(Optional.Empty);
            return true;
        }

        Transform child = owner.Value.transform.Find(entity.Path);

        if (!child)
        {
            Log.Error($"Could not locate child at path {entity.Path} in {owner.Value.name}");
            result.Set(Optional.Empty);
            return true;
        }

        // Cyclops damage points get their id from CyclopsDamagePointEntitySpawner. Older saves still hold a path based entity for their
        // WeldablePoint, which would replace that id and make players disagree on which entity to destroy when the point is repaired.
        if (child.GetComponent<CyclopsDamagePoint>())
        {
            result.Set(Optional.Empty);
            return true;
        }

        GameObject gameObject = child.gameObject;
        NitroxEntity.SetNewId(gameObject, entity.Id);

        result.Set(gameObject);
        return true;
    }

    protected override bool SpawnsOwnChildren(PathBasedChildEntity entity) => false;
}
