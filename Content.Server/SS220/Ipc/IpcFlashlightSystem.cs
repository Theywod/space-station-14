// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

using Content.Shared.Anomaly.Components;
using Content.Shared.Light;
using Content.Shared.SS220.Ipc;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.SS220.Ipc;

public sealed partial class IpcFlashlightSystem : EntitySystem
{
    private const string LightRegistryKey = "PointLight";

    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedPointLightSystem _light = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<IpcFlashlightVisualsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<IpcFlashlightVisualsComponent, LightToggleEvent>(OnLightToggle);
        SubscribeLocalEvent<IpcFlashlightVisualsComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<InnerBodyAnomalyComponent, ComponentStartup>(OnAnomalyStartup);
        EntityManager.ComponentRemoved += OnComponentRemoved;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        EntityManager.ComponentRemoved -= OnComponentRemoved;
    }

    private void OnComponentRemoved(RemovedComponentEventArgs args)
    {
        if (args.BaseArgs.Component is not InnerBodyAnomalyComponent)
            return;

        if (!TryComp(args.BaseArgs.Owner, out IpcFlashlightVisualsComponent? visuals) ||
            visuals.SpawnedLight is not { } light ||
            !_light.TryGetLight(light, out var pointLight))
            return;

        _light.SetColor(light, Color.White, pointLight);
    }

    private void OnStartup(Entity<IpcFlashlightVisualsComponent> ent, ref ComponentStartup args)
    {
        if (!_proto.TryIndex(ent.Comp.LightEntity, out _))
            return;

        var light = EntityManager.SpawnAttachedTo(ent.Comp.LightEntity, new EntityCoordinates(ent.Owner, default));
        ent.Comp.SpawnedLight = light;
        ApplyAnomalyColor(ent);
    }

    private void OnLightToggle(Entity<IpcFlashlightVisualsComponent> ent, ref LightToggleEvent args)
    {
        if (ent.Comp.SpawnedLight is not { } light || !_light.TryGetLight(light, out var pointLight))
            return;

        _light.SetEnabled(light, args.IsOn, pointLight);
    }

    private void OnShutdown(Entity<IpcFlashlightVisualsComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.SpawnedLight is not { } light)
            return;

        if (!TerminatingOrDeleted(light))
            QueueDel(light);

        ent.Comp.SpawnedLight = null;
    }

    private void OnAnomalyStartup(Entity<InnerBodyAnomalyComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp(ent.Owner, out IpcFlashlightVisualsComponent? visuals))
            return;

        ApplyAnomalyColor((ent.Owner, visuals));
    }

    private void ApplyAnomalyColor(Entity<IpcFlashlightVisualsComponent> ent)
    {
        if (ent.Comp.SpawnedLight is not { } light || !_light.TryGetLight(light, out var pointLight))
            return;

        if (!TryComp(ent.Owner, out InnerBodyAnomalyComponent? anomaly) || anomaly.InjectionProto is not { } protoId)
            return;

        if (!_proto.TryIndex<EntityPrototype>(protoId, out var proto) ||
            !proto.Components.TryGetValue(LightRegistryKey, out var entry) ||
            entry.Component is not SharedPointLightComponent injectedLight)
            return;

        _light.SetColor(light, injectedLight.Color, pointLight);
    }
}
