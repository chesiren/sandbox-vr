using Sandbox.UI;

/// <summary>
/// The sniper's zoomed view in the scope's glass, whenever it's held. On a screen the whole view zooms, which is no good stuck to
/// the eyes: this is a second camera along the barrel, seen by bringing the weapon up to the eye, so there's nothing to press.
/// It only renders while the sniper's in hand and showing.
/// </summary>
public sealed class VRScope : VRHudPanel
{
	/// <summary>
	/// How wide the glass is, in world units: as wide as the scope's eyepiece.
	/// </summary>
	[Property] public float Width { get; set; } = 3.1f;

	/// <summary>
	/// How far above the controller's aim point the glass is.
	/// </summary>
	[Property] public float Height { get; set; } = 2.7f;

	/// <summary>
	/// How far left of the controller's aim point the glass is.
	/// </summary>
	[Property] public float Side { get; set; } = 0.2f;

	/// <summary>
	/// How far along the barrel the glass is from the controller's aim point. It's on the end of the scope's eyepiece, behind that point.
	/// </summary>
	[Property] public float Forward { get; set; } = -5.2f;

	/// <summary>
	/// How far along the barrel the camera is, past the weapon so it isn't in the picture.
	/// </summary>
	[Property] public float CameraReach { get; set; } = 30f;

	/// <summary>
	/// How sharp the picture is, in pixels across.
	/// </summary>
	[Property] public int Resolution { get; set; } = 512;

	protected override Vector2 PanelPixels => new( 512, 512 );

	protected override float Visibility => _showing ? 1f : 0f;

	CameraComponent _camera;
	Texture _texture;
	Panel _glass;
	bool _showing;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( Panel is null || Panel.ChildrenCount > 0 ) return;

		_glass = Panel.Add.Panel( "glass" );
		_glass.Add.Panel( "line horizontal" );
		_glass.Add.Panel( "line vertical" );
		_glass.Add.Panel( "dot" );
	}

	protected override void OnStart()
	{
		var go = new GameObject( true, "VR Scope Camera" );
		go.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;
		go.SetParent( GameObject );

		_texture = Texture.CreateRenderTarget( "vr_scope", ImageFormat.RGBA8888, new Vector2( Resolution ) );

		_camera = go.AddComponent<CameraComponent>();
		_camera.IsMainCamera = false;
		_camera.RenderTarget = _texture;
		_camera.RenderExcludeTags.Add( "ui" );
		_camera.RenderExcludeTags.Add( "firstperson" );
		_camera.Enabled = false;

		_glass?.Style.SetBackgroundImage( _texture );
	}

	protected override void OnDestroy()
	{
		_texture?.Dispose();

		base.OnDestroy();
	}

	protected override void Place( Transform head )
	{
		if ( !_showing ) return;

		// The pose the weapon's placed from, see VRPlayer.PlaceHeldWeapon
		var aim = VRPlayer.Local?.Aim ?? VRMode.DominantHand.AimTransform;
		var position = aim.Position + aim.Rotation.Up * Height + aim.Rotation.Left * Side + aim.Rotation.Forward * Forward;

		// Facing back along the barrel, like glass: it's for looking through, not for turning to the eyes
		PlaceAt( position, Rotation.LookAt( -aim.Rotation.Forward, aim.Rotation.Up ), Width );

		_camera.WorldPosition = aim.Position + aim.Rotation.Forward * CameraReach;
		_camera.WorldRotation = aim.Rotation;
	}

	protected override void OnUpdate()
	{
		if ( !_camera.IsValid() ) return;

		var vr = VRPlayer.Local;
		var sniper = vr?.Player.GetComponent<PlayerInventory>()?.ActiveWeapon as SniperWeapon;

		// The weapon's hidden for these, see VRPlayer's body
		_showing = sniper.IsValid() && !SpawnMenuHost.IsOpen && !vr.WheelOpen && !VRKeyboard.IsOpen;
		_camera.Enabled = _showing;

		if ( _showing )
			_camera.FieldOfView = sniper.ScopedFov;
	}
}
