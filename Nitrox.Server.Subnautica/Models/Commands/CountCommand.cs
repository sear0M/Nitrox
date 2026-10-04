using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Server.Subnautica.Models.Commands;

[RequiresPermission(Perms.ADMIN)]
internal sealed class CountCommand(EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData, PlayerManager playerManager) : ICommandHandler<string>
{
    private const int MAX_LISTED_ENTITIES = 10;

    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly PlayerManager playerManager = playerManager;
    private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;

    [Description("Counts the entities of a TechType known by the server and lists the ones closest to the players")]
    public async Task Execute(ICommandContext context, [Description("TechType name, e.g. ReaperLeviathan")] string techTypeName)
    {
        List<Entity> entities = entityRegistry.GetAllEntities()
                                              .Where(entity => string.Equals(entity.TechType?.Name, techTypeName, StringComparison.OrdinalIgnoreCase))
                                              .ToList();
        List<Player> players = playerManager.GetConnectedPlayers();

        StringBuilder builder = new();
        builder.AppendLine($"{entities.Count} entities with TechType {techTypeName} (only the areas generated so far are known by the server)");
        foreach (WorldEntity worldEntity in entities.OfType<WorldEntity>()
                                                     .OrderBy(entity => GetDistanceToClosestPlayer(entity, players))
                                                     .Take(MAX_LISTED_ENTITIES))
        {
            bool isLocked = simulationOwnershipData.TryGetLock(worldEntity.Id, out SimulationOwnershipData.PlayerLock playerLock);
            builder.AppendLine($" └ {worldEntity.Id} at {worldEntity.Transform.Position}, {GetDistanceToClosestPlayer(worldEntity, players):0}m from the closest player, simulated by {(isLocked ? playerLock.Player.Name : "nobody")}");
        }

        await context.ReplyAsync(builder.ToString());
    }

    private static float GetDistanceToClosestPlayer(WorldEntity worldEntity, List<Player> players)
    {
        if (players.Count == 0)
        {
            return float.MaxValue;
        }

        return players.Min(player => NitroxVector3.Distance(player.Position, worldEntity.Transform.Position));
    }
}
