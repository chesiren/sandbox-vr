/// <summary>
/// The held weapon's ammo, just above it where the thumb is, painted by the screen HUD's <see cref="VitalsSection"/>. A click
/// in the hand when it's fired empty.
/// </summary>
public sealed class VRWeaponAmmo : VRHudPanel
{
	/// <summary>
	/// How far above the controller's aim point the counter sits.
	/// </summary>
	[Property] public float Height { get; set; } = 3.5f;

	/// <summary>
	/// How far back from the controller's aim point the counter sits.
	/// </summary>
	[Property] public float Back { get; set; } = 1f;

	/// <summary>
	/// How wide the panel is, in world units.
	/// </summary>
	[Property] public float Width { get; set; } = 8f;

	protected override Vector2 PanelPixels => new( 600, 200 );

	Vitals _vitals;
	VitalsSection _section;

	protected override void Place( Transform head )
	{
		// The pose the weapon's placed from, see VRPlayer.PlaceHeldWeapon
		var hand = VRPlayer.Local?.Aim ?? VRMode.DominantHand.AimTransform;
		var position = hand.Position + hand.Rotation.Up * Height - hand.Rotation.Forward * Back;

		PlaceAt( position, Rotation.LookAt( head.Position - position, hand.Rotation.Up ), Width );
	}

	protected override void OnUpdate()
	{
		_vitals ??= Scene.Get<Vitals>();
		_section ??= Canvas?.Section<VitalsSection>();
		if ( _section is null || !_vitals.IsValid() ) return;

		var data = _vitals.Read();
		data.ShowVitals = false;

		_section.Compact = true;
		_section.Settings = _vitals;
		_section.Data = data;

		var vr = VRPlayer.Local;
		if ( vr is null || vr.PointingAtUI ) return;

		if ( data.ShowAmmo && data.UsesClips && data.Clip <= 0 && Input.Pressed( "attack1" ) )
			VRMode.PulseDominant( 0.25f, 0.2f );
	}
}
