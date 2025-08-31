using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Content.Shared.Roles;

namespace Content.Shared.WH40K.OrcsGameMode;

/// <summary>
/// This is used for tagging a mob as a nuke operative.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class OrcComponent : Component
{

[DataField("orcRoleId", customTypeSerializer: typeof(PrototypeIdSerializer<AntagPrototype>))]
public string OrcRoleId = "Orc";

}
