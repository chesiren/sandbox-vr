using Sandbox.UI;

/// <summary>
/// The screen HUD's <see cref="AmmoPickupFeed"/>, just above the held weapon's ammo, see <see cref="VRWeaponAmmo"/>.
/// </summary>
public sealed class VRAmmoPickups : VRHudPanel
{
	/// <summary>
	/// How far above the controller's aim point the panel's centre is.
	/// </summary>
	[Property] public float Height { get; set; } = 9f;

	/// <summary>
	/// How wide the panel is, in world units.
	/// </summary>
	[Property] public float Width { get; set; } = 10f;

	protected override Vector2 PanelPixels => new( 700, 400 );

	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( Panel is null || Panel.ChildrenCount > 0 ) return;

		Panel.AddChild<AmmoPickupFeed>().AddClass( "vr" );
	}

	protected override void Place( Transform head )
	{
		var hand = VRMode.DominantHand.AimTransform;
		var position = hand.Position + hand.Rotation.Up * Height;

		PlaceAt( position, Rotation.LookAt( head.Position - position, hand.Rotation.Up ), Width );
	}
}
