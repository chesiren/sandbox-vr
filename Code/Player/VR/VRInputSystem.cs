/// <summary>
/// Presses the VR controllers' actions at the very start of the frame. Components read input in their own
/// update, in no particular order - the spawn menu checking its button before the player had mapped the
/// controllers never saw it held.
/// </summary>
public sealed class VRInputSystem : GameObjectSystem<VRInputSystem>
{
	public VRInputSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, -100, OnStartUpdate, "VRInput" );

		// Before ControlSystem reads the driver's actions
		Listen( Stage.StartFixedUpdate, -100, OnStartFixedUpdate, "VRInputFixed" );
	}

	void OnStartUpdate()
	{
		if ( Scene != Game.ActiveScene ) return;

		VRPlayer.Local?.MapInput();
	}

	void OnStartFixedUpdate()
	{
		if ( Scene != Game.ActiveScene ) return;

		VRPlayer.Local?.MapFixedInput();
	}
}
