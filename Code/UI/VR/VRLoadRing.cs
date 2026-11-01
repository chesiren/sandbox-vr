/// <summary>
/// At the base of the spawn laser, for the screen HUD's <see cref="CloudLoadRing"/> to sit on: right by the hand it's spawning
/// from, where it can't be missed, rather than wherever the laser ends.
/// </summary>
public sealed class VRLoadRing : VRHudPanel
{
	/// <summary>
	/// How wide the ring is, in world units.
	/// </summary>
	[Property] public float Width { get; set; } = 6f;

	/// <summary>
	/// How far along the laser the ring sits, in front of the hand so the controller doesn't cover it.
	/// </summary>
	[Property] public float HandGap { get; set; } = 7f;

	protected override Vector2 PanelPixels => new( 256, 256 );

	protected override void Place( Transform head )
	{
		if ( !CloudLoadTracker.IsLoading ) return;

		var aim = VRMode.OffHand.AimTransform;
		var position = aim.Position + aim.Forward * HandGap;

		PlaceAt( position, Rotation.LookAt( head.Position - position, Vector3.Up ), Width );
	}
}
