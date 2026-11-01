/// <summary>
/// Above the object the inspect menu highlights, for the screen HUD's <see cref="OwnerLabel"/> to sit on.
/// </summary>
public sealed class VROwnerPlate : VRHudPanel
{
	/// <summary>
	/// How far above the object the label floats.
	/// </summary>
	[Property] public float Gap { get; set; } = 6f;

	/// <summary>
	/// How big the label looks, as its width per unit of distance. It stays readable from any distance.
	/// </summary>
	[Property] public float Size { get; set; } = 0.3f;

	protected override Vector2 PanelPixels => new( 800, 120 );

	Vector3 _target;

	protected override void Place( Transform head )
	{
		// Left where it was while the label fades out
		if ( InspectCursor.IsOpen && SpawnMenuHost.GetActiveMode() is ContextMenuHost { Hovered: { } hovered } && hovered.IsValid() )
		{
			var bounds = hovered.GetBounds();
			_target = bounds.Center + Vector3.Up * (bounds.Size.z * 0.5f + Gap);
		}

		PlaceAt( _target, Rotation.LookAt( head.Position - _target, Vector3.Up ), head.Position.Distance( _target ) * Size );
	}
}
