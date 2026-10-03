using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public class EntitySpawnedByClientProcessorTest
{
    private AuthProcessorContext context;
    private EntityRegistry entityRegistry;
    private EntitySpawnedByClientProcessor processor;
    private WorldEntityManager worldEntityManager;

    [TestInitialize]
    public void Init()
    {
        NopPacketSender packetSender = new();
        PlayerManager playerManager = new(null, null, null, NullLogger<PlayerManager>.Instance);
        entityRegistry = new EntityRegistry(NullLogger<EntityRegistry>.Instance);
        worldEntityManager = new WorldEntityManager(packetSender, entityRegistry, null, playerManager, null, NullLogger<WorldEntityManager>.Instance);
        EntitySimulation entitySimulation = new(packetSender, entityRegistry, worldEntityManager, new SimulationOwnershipData(), playerManager, NullLogger<EntitySimulation>.Instance);
        processor = new EntitySpawnedByClientProcessor(playerManager, entityRegistry, worldEntityManager, entitySimulation);
        context = new AuthProcessorContext(CreatePlayer(1), packetSender);
    }

    [TestMethod]
    public async Task Process_IdReusedByWorldEntityInAnotherCell_UnregistersPreviousCell()
    {
        NitroxId id = new();
        WorldEntity previousEntity = CreateWorldEntity(id, new NitroxVector3(10, -20, 30));
        entityRegistry.AddOrUpdate(previousEntity);
        worldEntityManager.TrackEntityInTheWorld(previousEntity);
        WorldEntity newEntity = CreateWorldEntity(id, new NitroxVector3(500, -20, 500));
        newEntity.AbsoluteEntityCell.Should().NotBe(previousEntity.AbsoluteEntityCell);

        await processor.Process(context, new EntitySpawnedByClient(newEntity, requireSimulation: false));

        worldEntityManager.GetEntities(previousEntity.AbsoluteEntityCell).Should().BeEmpty();
        worldEntityManager.GetEntities(newEntity.AbsoluteEntityCell).Should().ContainSingle().Which.Should().BeSameAs(newEntity);
    }

    [TestMethod]
    public async Task Process_IdReusedByInventoryItem_UnregistersPreviousCell()
    {
        NitroxId id = new();
        WorldEntity previousEntity = CreateWorldEntity(id, new NitroxVector3(10, -20, 30));
        entityRegistry.AddOrUpdate(previousEntity);
        worldEntityManager.TrackEntityInTheWorld(previousEntity);
        InventoryItemEntity inventoryItem = new(id, "classId", new NitroxTechType("Peeper"), null, new NitroxId(), []);

        await processor.Process(context, new EntitySpawnedByClient(inventoryItem, requireSimulation: false));

        worldEntityManager.GetEntities(previousEntity.AbsoluteEntityCell).Should().BeEmpty();
        entityRegistry.GetEntityById(id).Value.Should().BeSameAs(inventoryItem);
    }

    [TestMethod]
    public async Task Process_IdReusedByGlobalRootEntity_TracksNewGlobalRootEntity()
    {
        NitroxId id = new();
        GlobalRootEntity previousEntity = CreateGlobalRootEntity(id);
        entityRegistry.AddOrUpdate(previousEntity);
        worldEntityManager.TrackEntityInTheWorld(previousEntity);
        GlobalRootEntity newEntity = CreateGlobalRootEntity(id);

        await processor.Process(context, new EntitySpawnedByClient(newEntity, requireSimulation: false));

        worldEntityManager.GetGlobalRootEntities().Should().ContainSingle().Which.Should().BeSameAs(newEntity);
    }

    [TestMethod]
    public async Task Process_IdReusedByInventoryItemOfGlobalRootEntity_UntracksGlobalRootEntity()
    {
        NitroxId id = new();
        GlobalRootEntity previousEntity = CreateGlobalRootEntity(id);
        entityRegistry.AddOrUpdate(previousEntity);
        worldEntityManager.TrackEntityInTheWorld(previousEntity);
        InventoryItemEntity inventoryItem = new(id, "classId", new NitroxTechType("Peeper"), null, new NitroxId(), []);

        await processor.Process(context, new EntitySpawnedByClient(inventoryItem, requireSimulation: false));

        worldEntityManager.GetGlobalRootEntities().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Process_IdReusedAfterEntityWithInvalidCellLevel_UpdatesRegistry()
    {
        NitroxId id = new();
        WorldEntity previousEntity = new(new NitroxVector3(10, -20, 30), NitroxQuaternion.Identity, NitroxVector3.One, new NitroxTechType("Peeper"), 100, "classId", false, id, null);
        entityRegistry.AddOrUpdate(previousEntity);
        WorldEntity newEntity = CreateWorldEntity(id, new NitroxVector3(10, -20, 30));

        await processor.Process(context, new EntitySpawnedByClient(newEntity, requireSimulation: false));

        entityRegistry.GetEntityById(id).Value.Should().BeSameAs(newEntity);
        worldEntityManager.GetEntities(newEntity.AbsoluteEntityCell).Should().ContainSingle().Which.Should().BeSameAs(newEntity);
    }

    private static GlobalRootEntity CreateGlobalRootEntity(NitroxId id)
    {
        return new GlobalRootEntity(new NitroxTransform(new NitroxVector3(10, -20, 30), NitroxQuaternion.Identity, NitroxVector3.One), 0, "classId", false, id, new NitroxTechType("Seamoth"), null, null, []);
    }

    private static WorldEntity CreateWorldEntity(NitroxId id, NitroxVector3 position)
    {
        return new WorldEntity(position, NitroxQuaternion.Identity, NitroxVector3.One, new NitroxTechType("Peeper"), 0, "classId", false, id, null);
    }

    private static Player CreatePlayer(ushort sessionId)
    {
        return new Player(sessionId, sessionId, $"Player{sessionId}", false, null, NitroxVector3.Zero, NitroxQuaternion.Identity, new NitroxId(), Optional.Empty, Perms.PLAYER,
                          new PlayerStatsData(100, 100, 100, 100, 100, 0), SubnauticaGameMode.SURVIVAL, [], [], new Dictionary<string, NitroxId>(), new Dictionary<string, float>(),
                          new Dictionary<string, PingInstancePreference>(), [], false, true);
    }
}
