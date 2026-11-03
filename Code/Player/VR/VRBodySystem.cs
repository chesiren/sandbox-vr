/// <summary>
/// Poses VR players' bodies just before the bones are worked out. The player controller animates the body in its own
/// update like any player's, in no particular order with ours - posing it here comes after all of that, every frame.
/// </summary>
public sealed class VRBodySystem : GameObjectSystem<VRBodySystem>
{
	public VRBodySystem( Scene scene ) : base( scene )
	{
		Listen( Stage.UpdateBones, -100, PoseBodies, "VRBody" );
	}

	void PoseBodies()
	{
		foreach ( var vr in Scene.GetAllComponents<VRPlayer>() )
		{
			if ( vr.IsVR ) vr.PoseBody();
		}
	}
}
