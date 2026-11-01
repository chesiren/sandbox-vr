/// <summary>
/// At the end of the spawn laser, for the screen HUD's <see cref="CloudLoadRing"/> to sit on: what's on its way lands there.
/// </summary>
public sealed class VRLoadRing : VRHudPanel
{
	/// <summary>
	/// How big the ring looks, as the panel's width per unit of distance. It stays the same size from any distance.
	/// </summary>
	[Property] public float Size { get; set; } = 0.08f;

	/// <summary>
	/// How far towards the player the ring comes off the surface, so it doesn't sink into it.
	/// </summary>
	[Property] public float SurfaceGap { get; set; } = 4f;

	protected override Vector2 PanelPixels => new( 128, 128 );

	protected override void Place( Transform head )
	{
		if ( !CloudLoadTracker.IsLoading ) return;

		var point = VRHud.SpawnAimPoint( Scene );
		var position = point + (head.Position - point).Normal * SurfaceGap;

		PlaceAt( position, Rotation.LookAt( head.Position - position, Vector3.Up ), head.Position.Distance( position ) * Size );
	}
}
