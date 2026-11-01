/// <summary>
/// Health and armour on the off hand's wrist, painted by the screen HUD's <see cref="VitalsSection"/>. Dim until the player
/// looks at it, like a watch. Warns about low health through <see cref="VRDamagePanel"/> and the hand.
/// </summary>
public sealed class VRWatch : VRHudPanel
{
	/// <summary>
	/// How far back from the controller's aim point the watch sits, towards the elbow.
	/// </summary>
	[Property] public float WristDistance { get; set; } = 5f;

	/// <summary>
	/// How far above the wrist the watch floats.
	/// </summary>
	[Property] public float FaceHeight { get; set; } = 4.5f;

	/// <summary>
	/// How wide the panel is, in world units.
	/// </summary>
	[Property] public float Width { get; set; } = 9f;

	/// <summary>
	/// How visible the watch is when it isn't being looked at.
	/// </summary>
	[Property, Range( 0, 1 )] public float DimOpacity { get; set; } = 0.35f;

	protected override Vector2 PanelPixels => new( 640, 200 );

	protected override float Visibility => MathX.Lerp( DimOpacity, 1f, _lit );

	/// <summary>
	/// The hand the watch is on.
	/// </summary>
	public static Sandbox.VR.VRController Hand => VRSettings.Current.WatchOnDominantHand ? VRMode.DominantHand : VRMode.OffHand;

	/// <summary>
	/// Which way the watch faces: the back of the wrist, what faces up when the arm's brought across the body to read it.
	/// </summary>
	public static Vector3 FaceDirection( Transform hand ) => Hand == Input.VR.LeftHand ? hand.Rotation.Left : hand.Rotation.Right;

	Vitals _vitals;
	VitalsSection _section;
	bool _wasLow;
	float _lit;

	protected override void Place( Transform head )
	{
		var hand = Hand.AimTransform;
		var face = FaceDirection( hand );
		var position = hand.Position - hand.Rotation.Forward * WristDistance + face * FaceHeight;

		PlaceAt( position, Rotation.LookAt( face, -hand.Rotation.Up ), Width * VRSettings.Current.WatchSize );

		var toHead = (head.Position - position).Normal;
		var lookedAt = Vector3.Dot( face, toHead ) > 0.4f && Vector3.Dot( head.Rotation.Forward, -toHead ) > 0.7f;
		_lit = _lit.Approach( lookedAt ? 1f : 0f, Time.Delta * 6f );
	}

	protected override void OnUpdate()
	{
		_vitals ??= Scene.Get<Vitals>();
		_section ??= Canvas?.Section<VitalsSection>();
		if ( _section is null || !_vitals.IsValid() ) return;

		var data = _vitals.Read();
		data.ShowAmmo = false;

		_section.Compact = true;
		_section.Settings = _vitals;
		_section.Data = data;

		var low = data.ShowVitals && data.MaxHealth > 0 && data.Health > 0 && data.Health <= data.MaxHealth * _vitals.LowHealthFraction;
		if ( low && !_wasLow ) VRMode.Pulse( Hand, 0.8f, 1f );
		_wasLow = low;

		var icon = Player.FindLocalPlayer()?.GetComponent<PlayerDamageIndicators>()?.RadialDamageIcon;
		Scene.Get<VRDamagePanel>()?.SetLowHealth( low, icon );
	}
}
