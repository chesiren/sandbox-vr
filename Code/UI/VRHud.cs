using Sandbox.VR;

/// <summary>
/// Moves the screen HUD into the world for VR: a screen panel only draws on the monitor, so the HUD becomes a
/// world panel floating in front of the player, and a laser from the dominant hand clicks it. Added at runtime
/// to the HUD's GameObject by <see cref="VRPlayer"/>, never present outside VR.
/// </summary>
public sealed class VRHud : Component
{
	/// <summary>
	/// How far in front of the eyes the HUD floats.
	/// </summary>
	[Property] public float Distance { get; set; } = 32f;

	/// <summary>
	/// How far below the eyes the HUD sits, so it isn't in the way of looking around.
	/// </summary>
	[Property] public float Drop { get; set; } = 6f;

	/// <summary>
	/// Scale of the HUD panel. At 0.12 it is about 1.2 metres across.
	/// </summary>
	[Property] public float Scale { get; set; } = 0.12f;

	/// <summary>
	/// How quickly the HUD catches up with where you look. Higher is snappier.
	/// </summary>
	[Property] public float FollowSpeed { get; set; } = 12f;

	GameObject _pointer;
	WorldInput _input;
	LineRenderer _laser;

	float _yaw = float.NaN;
	float _pitch;
	bool _wasMenuOpen;

	/// <summary>
	/// The panel the dominant hand's laser is over, if any.
	/// </summary>
	public static bool IsPointing { get; private set; }

	/// <summary>
	/// Moves the scene's HUD into the world, once.
	/// </summary>
	public static async System.Threading.Tasks.Task Enable( Scene scene )
	{
		var layer = scene.GetAllComponents<HudLayer>().FirstOrDefault();
		if ( !layer.IsValid() || layer.GetComponent<VRHud>().IsValid() ) return;

		var hud = layer.GameObject;

		// The screen panel has to go, not just be disabled: child panels keep parenting to it while it exists
		hud.Components.Get<ScreenPanel>( true )?.Destroy();
		await System.Threading.Tasks.Task.Yield();

		if ( !hud.IsValid() ) return;

		var panel = hud.AddComponent<WorldPanel>();
		// World panels lay out at half their pixel size, so this gives the HUD the same 1920x1080 layout as the screen
		panel.PanelSize = new Vector2( 3840, 2160 );
		panel.LookAtCamera = false;
		panel.InteractionRange = 2000f;

		// These pin labels to things in the world by projecting them onto the screen, which a floating panel isn't
		foreach ( var overlay in hud.GetComponentsInChildren<SignalsOverlay>( true ) ) overlay.GameObject.Enabled = false;
		foreach ( var overlay in hud.GetComponentsInChildren<OwnerLabel>( true ) ) overlay.GameObject.Enabled = false;

		// Rebuild every panel under the HUD against the world panel
		hud.Enabled = false;
		hud.Enabled = true;

		hud.AddComponent<VRHud>();
	}

	protected override void OnStart()
	{
		_pointer = new GameObject( true, "VR Pointer" );
		_pointer.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;
		_input = _pointer.AddComponent<WorldInput>();
		_input.LeftMouseAction = "attack1";
		_input.RightMouseAction = "attack2";
		_input.VRHandSource = VRMode.LeftHanded ? VRHand.HandSources.Left : VRHand.HandSources.Right;

		_laser = _pointer.AddComponent<LineRenderer>();
		_laser.UseVectorPoints = true;
		_laser.Width = 0.25f;
		_laser.Color = Color.White.WithAlpha( 0.6f );
		_laser.CastShadows = false;
		_laser.Opaque = false;

		_yaw = float.NaN;
	}

	protected override void OnDestroy()
	{
		if ( _pointer.IsValid() ) _pointer.Destroy();
		IsPointing = false;
	}

	protected override void OnUpdate()
	{
		if ( !VRMode.IsActive || !_pointer.IsValid() ) return;

		var head = Input.VR.Head;
		var menuOpen = SpawnMenuHost.IsOpen || IsScoreboardOpen;

		PlacePanel( head, menuOpen );
		UpdatePointer( menuOpen );
	}

	static bool IsScoreboardOpen => Input.Down( "score" );

	void PlacePanel( Transform head, bool menuOpen )
	{
		var look = head.Rotation.Angles();
		var pitch = look.pitch.Clamp( -60f, 60f );

		if ( float.IsNaN( _yaw ) || (menuOpen && !_wasMenuOpen) )
		{
			// A menu opens straight ahead
			_yaw = look.yaw;
			_pitch = pitch;
		}
		else if ( !menuOpen )
		{
			// ...and stays put while it's used; the HUD follows the view, just softened
			var t = 1f - MathF.Exp( -FollowSpeed * Time.Delta );
			_yaw += Angles.NormalizeAngle( look.yaw - _yaw ) * t;
			_pitch += (pitch - _pitch) * t;
		}

		_wasMenuOpen = menuOpen;

		var rotation = Rotation.From( _pitch, _yaw, 0 );
		var forward = rotation.Forward;
		var position = head.Position + forward * Distance + rotation.Down * (menuOpen ? 0f : Drop);

		WorldPosition = position;
		WorldRotation = Rotation.LookAt( -forward, rotation.Up );
		WorldScale = Scale;
	}

	void UpdatePointer( bool menuOpen )
	{
		var aim = VRMode.DominantHand.AimTransform;
		_pointer.WorldTransform = aim;

		IsPointing = _input.Hovered.IsValid();

		var showLaser = menuOpen || IsPointing;
		_laser.Enabled = showLaser;

		if ( showLaser )
		{
			var length = Distance * 1.5f;
			_laser.VectorPoints = new List<Vector3> { aim.Position, aim.Position + aim.Forward * length };
		}
	}
}
