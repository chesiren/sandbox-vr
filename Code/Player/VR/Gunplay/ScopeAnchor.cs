/// <summary>
/// A physical gun's scope: where its glass is, facing along the barrel. While the gun's held by its grip,
/// <see cref="VRScope"/> shows the zoomed view in that glass.
/// </summary>
public sealed class ScopeAnchor : Component
{
	/// <summary>
	/// How wide the glass is, in world units: as wide as the scope's eyepiece.
	/// </summary>
	[Property] public float Width { get; set; } = 3.1f;

	/// <summary>
	/// How much of the view the glass shows, in degrees.
	/// </summary>
	[Property] public float FieldOfView { get; set; } = 9f;

	/// <summary>
	/// How far past the muzzle the camera is, so the gun isn't in the picture. It looks along the barrel, so the cross
	/// is on where the bullet goes.
	/// </summary>
	[Property] public float CameraReach { get; set; } = 4f;

	/// <summary>
	/// The scope of the gun one of our hands holds by its grip, if it has one.
	/// </summary>
	public static ScopeAnchor Held( VRPlayer vr )
	{
		if ( vr is null ) return null;

		foreach ( var hand in new[] { vr.DominantGunplayHand, vr.OffGunplayHand } )
		{
			if ( !hand.IsValid() || !hand.IsHolding() ) continue;
			if ( hand.CurrentGrabbable.Interactable is not PhysicalGun gun || gun.GripHand != hand ) continue;

			if ( gun.GetComponentInChildren<ScopeAnchor>() is { } scope )
				return scope;
		}

		return null;
	}

	protected override void DrawGizmos()
	{
		Gizmo.Draw.Color = Color.Cyan.WithAlpha( 0.5f );
		Gizmo.Draw.LineBBox( BBox.FromPositionAndSize( 0, new Vector3( 0.1f, Width, Width ) ) );
		Gizmo.Draw.Line( 0, Vector3.Forward * 4f );
	}
}
