// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

using Content.IntegrationTests.Fixtures;
using Content.Server.SS220.Ipc;
using Content.Shared.SS220.Ipc;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.SS220.Tests.Ipc;

[TestFixture]
[TestOf(typeof(IpcModuleSlotSystem))]
public sealed class IpcModuleSlotTest : GameTest
{
    private static readonly EntProtoId MobIpc = "MobIpc";
    private static readonly EntProtoId RadioModule = "IpcRadioModule";
    private static readonly EntProtoId DrainModule = "IpcDrainModule";
    private static readonly EntProtoId SensorModule = "IpcSuitSensorModule";
    private static readonly EntProtoId FlashlightModule = "IpcFlashlightModule";
    private static readonly EntProtoId MinerFlashlightModule = "IpcMinerFlashlightModule";

    [Test]
    public async Task ModulesInsertAndEject()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        EntityUid ipc = default;
        await server.WaitPost(() => ipc = entMan.SpawnEntity(MobIpc, map.GridCoords));
        await server.WaitIdleAsync();

        var slots = entMan.System<IpcModuleSlotSystem>();
        var containerSystem = entMan.System<SharedContainerSystem>();

        // The IPC starts with the radio and screen modules from the container fill.
        await server.WaitAssertion(() =>
        {
            Assert.That(containerSystem.TryGetContainer(ipc, IpcModuleSlotSystem.ModulesContainerId, out var container), Is.True);
            Assert.That(container!.ContainedEntities, Has.Count.EqualTo(2));
        });

        // Ejecting a module removes it, reinserting it adds it back.
        await server.WaitAssertion(() =>
        {
            containerSystem.TryGetContainer(ipc, IpcModuleSlotSystem.ModulesContainerId, out var container);
            Assert.That(container!.ContainedEntities, Has.Count.EqualTo(2));

            var radio = FindModule(entMan, container, RadioModule);
            var ipcComp = entMan.GetComponent<IpcComponent>(ipc);

            Assert.That(slots.TryEjectModule((ipc, ipcComp), radio), Is.True);
            Assert.That(container.ContainedEntities, Has.Count.EqualTo(1));

            Assert.That(slots.TryInsertModule((ipc, ipcComp), radio), Is.True);
            Assert.That(container.ContainedEntities, Has.Count.EqualTo(2));
        });

        // A duplicate module cannot be installed.
        await server.WaitAssertion(() =>
        {
            var second = entMan.SpawnEntity(RadioModule, map.GridCoords);
            var ipcComp = entMan.GetComponent<IpcComponent>(ipc);
            Assert.That(slots.TryInsertModule((ipc, ipcComp), second), Is.False);
        });

        // The modules container cannot hold more than the capacity.
        await server.WaitAssertion(() =>
        {
            var ipcComp = entMan.GetComponent<IpcComponent>(ipc);
            Assert.That(ipcComp.ModuleCapacity, Is.EqualTo(5));

            Assert.That(slots.TryInsertModule((ipc, ipcComp), entMan.SpawnEntity(DrainModule, map.GridCoords)), Is.True);
            Assert.That(slots.TryInsertModule((ipc, ipcComp), entMan.SpawnEntity(SensorModule, map.GridCoords)), Is.True);
            Assert.That(slots.TryInsertModule((ipc, ipcComp), entMan.SpawnEntity(FlashlightModule, map.GridCoords)), Is.True);
            Assert.That(slots.TryInsertModule((ipc, ipcComp), entMan.SpawnEntity(MinerFlashlightModule, map.GridCoords)), Is.False);
        });

        // Modules that add the same components to the IPC conflict with each other.
        await server.WaitAssertion(() =>
        {
            containerSystem.TryGetContainer(ipc, IpcModuleSlotSystem.ModulesContainerId, out var container);
            var ipcComp = entMan.GetComponent<IpcComponent>(ipc);

            var sensor = FindModule(entMan, container!, SensorModule);
            Assert.That(slots.TryEjectModule((ipc, ipcComp), sensor), Is.True);

            // The flashlight module is installed, so the miner flashlight module
            // cannot be inserted: it would overwrite the flashlight components.
            var miner = entMan.SpawnEntity(MinerFlashlightModule, map.GridCoords);
            Assert.That(slots.TryInsertModule((ipc, ipcComp), miner), Is.False);

            Assert.That(slots.TryInsertModule((ipc, ipcComp), sensor), Is.True);
        });
    }

    private static EntityUid FindModule(IEntityManager entMan, BaseContainer container, EntProtoId proto)
    {
        foreach (var contained in container.ContainedEntities)
        {
            if (entMan.GetComponent<MetaDataComponent>(contained).EntityPrototype == proto)
                return contained;
        }

        Assert.Fail($"Module {proto} not found in the container.");
        return default;
    }
}
