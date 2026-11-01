/// <summary>
/// In front of the eyes for the free camera's settings, see <see cref="FreeCamOverlay"/>, on the monitor otherwise. The laser
/// clicks them, and it stays where it opened.
/// </summary>
public sealed class VRFreeCamPanel : VRHudPanel
{
	/// <summary>
	/// How wide the panel is, in world units.
	/// </summary>
	[Property] public float Width { get; set; } = 24f;

	/// <summary>
	/// How far in front of the eyes it opens.
	/// </summary>
	[Property] public float Distance { get; set; } = 30f;

	protected override Vector2 PanelPixels => new( 1200, 1200 );

	Vector3 _anchorLocal;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		InteractionRange = 2000f;

		var head = Input.VR.Head;
		var look = head.Rotation.Angles().WithRoll( 0 ).ToRotation();
		_anchorLocal = Input.VR.Anchor.PointToLocal( head.Position + look.Forward * Distance );
	}

	protected override void Place( Transform head )
	{
		var position = Input.VR.Anchor.PointToWorld( _anchorLocal );

		PlaceAt( position, Rotation.LookAt( head.Position - position, Vector3.Up ), Width );
	}
}
