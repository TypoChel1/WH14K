using Robust.Shared.Prototypes;

namespace Content.Server.WH40K.OrcsGameMode;

/// <summary>
/// This is used for tagging a spawn point as a nuke operative spawn point
/// and providing loadout + name for the operative on spawn.
/// TODO: Remove once systems can request spawns from the ghost role system directly.
/// </summary>
[RegisterComponent, EntityCategory("Spawner")]
public sealed partial class OrcSpawnerComponent : Component;
