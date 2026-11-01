/// <summary>
/// Above the dominant hand, for the screen HUD's <see cref="ToolInfoPanel"/> to sit on: the tool's name and which buttons do what.
/// </summary>
public sealed class VRToolHints : VRHudPanel
{
	/// <summary>
	/// How far above the controller's aim point the panel's centre is.
	/// </summary>
	[Property] public float Height { get; set; } = 9f;

	/// <summary>
	/// How wide the panel is, in world units.
	/// </summary>
	[Property] public float Width { get; set; } = 16f;

	protected override Vector2 PanelPixels => new( 1680, 600 );

	protected override void Place( Transform head )
	{
		var hand = VRMode.DominantHand.AimTransform;
		var position = hand.Position + Vector3.Up * Height;

		PlaceAt( position, Rotation.LookAt( head.Position - position, Vector3.Up ), Width );
	}
}
