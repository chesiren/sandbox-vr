using Sandbox.VR;

/// <summary>
/// Moves the screen HUD into the world for VR, a screen panel only draws on the monitor. Menus get a world panel of their
/// own that a laser from the dominant hand clicks, what's left of the screen HUD floats in front of the player, and the
/// <see cref="VRHudPanel"/>s replace the rest. Added at runtime to the HUD's GameObject by <see cref="VRPlayer"/>, never
/// present outside VR.
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
	/// Scale of the HUD panel. At 0.15 it is about 1.5 metres across.
	/// </summary>
	[Property] public float Scale { get; set; } = 0.15f;

	/// <summary>
	/// How far away menus open. Closer than the HUD, so they're big enough to read and point at.
	/// </summary>
	[Property] public float MenuDistance { get; set; } = 26f;

	/// <summary>
	/// How quickly the HUD catches up with where you look. Higher is snappier.
	/// </summary>
	[Property] public float FollowSpeed { get; set; } = 12f;

	/// <summary>
	/// Scale of the panel while the spawn menu's on the hand: small enough to move about with it.
	/// </summary>
	[Property] public float HandMenuScale { get; set; } = 0.14f;

	/// <summary>
	/// How far above the hand the spawn menu's centre sits, so the hand stays clear of it to point past.
	/// </summary>
	[Property] public float HandMenuHeight { get; set; } = 8f;

	/// <summary>
	/// How far beyond the hand the spawn menu sits, seen from the head, so it isn't right in front of the eyes.
	/// </summary>
	[Property] public float HandMenuDistance { get; set; } = 10f;

	/// <summary>
	/// How quickly the spawn menu catches up with the hand. Softens the hand's shake, so it's easier to click.
	/// </summary>
	[Property] public float HandMenuFollowSpeed { get; set; } = 20f;

	/// <summary>
	/// The same while the laser's on the menu, when a steady target matters more than keeping up.
	/// </summary>
	[Property] public float HandMenuAimingFollowSpeed { get; set; } = 6f;

	/// <summary>
	/// How far off a button the laser can stray and stay on it, in degrees. The hand shakes by about this much.
	/// </summary>
	[Property] public float MagnetAngle { get; set; } = 0.6f;

	/// <summary>
	/// Nothing bigger than this, in panel pixels, holds on to the laser: lists and pages aren't buttons.
	/// </summary>
	const float MagnetMaxSize = 700f;

	/// <summary>
	/// How far down the trigger is when the laser stops moving for a click. The engine clicks at 0.75, and the hand moves on the way.
	/// </summary>
	const float ClickFreezeTrigger = 0.3f;

	/// <summary>
	/// How long the laser stays still for a click, after that the trigger's being held to drag.
	/// </summary>
	const float ClickFreezeTime = 0.35f;

	GameObject _pointer;
	WorldInput _input;
	LineRenderer _laser;
	LineRenderer _dot;
	LineRenderer _spawnLaser;
	readonly List<GameObject> _parts = new();

	/// <summary>
	/// The panel the menus are on, moved around separately from the HUD.
	/// </summary>
	Sandbox.WorldPanel _menus;

	float _yaw = float.NaN;
	float _pitch;
	float _menuYaw;
	float _menuPitch;
	bool _wasMenuOpen;

	/// <summary>
	/// The panel the dominant hand's laser is over, if any.
	/// </summary>
	public static bool IsPointing { get; private set; }

	/// <summary>
	/// The watch's button has the scoreboard open, there's no button for it on the controllers.
	/// </summary>
	public static bool ScoreboardOpen { get; set; }

	/// <summary>
	/// Moves the scene's HUD into the world, once.
	/// </summary>
	public static async System.Threading.Tasks.Task Enable( Scene scene )
	{
		var layer = scene.GetAllComponents<HudLayer>().FirstOrDefault();
		if ( !layer.IsValid() || layer.GetComponent<VRHud>().IsValid() ) return;

		var hud = layer.GameObject;

		// Off while it has no panel to build against, or everything under it updates without one
		hud.Enabled = false;

		// The screen panel has to go, not just be disabled: child panels keep parenting to it while it exists
		hud.Components.Get<ScreenPanel>( true )?.Destroy();

		// Long enough for the old panels to finish deleting, which clears the @refs of whatever's built in the meantime
		await GameTask.DelaySeconds( 0.25f );

		if ( !hud.IsValid() ) return;

		var panel = hud.AddComponent<WorldPanel>();
		// World panels lay out at half their pixel size, so this gives the HUD the same 1920x1080 layout as the screen
		panel.PanelSize = new Vector2( 3840, 2160 );
		panel.LookAtCamera = false;
		// Nothing on it gets clicked: if the laser could hover it, the trigger wouldn't reach the weapon
		panel.InteractionRange = 0f;

		// Menus go on a panel of their own, with the same layout
		var menusObject = new GameObject( true, "VR Menus" );
		menusObject.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;

		var menus = menusObject.AddComponent<Sandbox.WorldPanel>();
		menus.PanelSize = panel.PanelSize;
		menus.LookAtCamera = false;

		foreach ( var menu in hud.GetComponentsInChildren<SpawnMenuHost>( true ) ) menu.GameObject.SetParent( menusObject );
		foreach ( var menu in hud.GetComponentsInChildren<Sandbox.UI.Scoreboard>( true ) ) menu.GameObject.SetParent( menusObject );
		foreach ( var menu in hud.GetComponentsInChildren<DragHandler>( true ) ) menu.GameObject.SetParent( menusObject );

		// These keep their looks and whatever calls them, on a panel of their own where they're of use
		var toolHints = VRHudPanel.Create<VRToolHints>( "VR Tool Hints" );
		foreach ( var info in hud.GetComponentsInChildren<ToolInfoPanel>( true ) ) info.GameObject.SetParent( toolHints.GameObject );

		var notices = VRHudPanel.Create<VRNoticeBand>( "VR Notices" );
		foreach ( var list in hud.GetComponentsInChildren<Sandbox.UI.Notices>( true ) ) list.GameObject.SetParent( notices.GameObject );

		var watchBack = VRHudPanel.Create<VRWatchBack>( "VR Watch Back" );
		foreach ( var feed in hud.GetComponentsInChildren<Feed>( true ) ) feed.GameObject.SetParent( watchBack.GameObject );
		foreach ( var voices in hud.GetComponentsInChildren<Facepunch.UI.Voices>( true ) ) voices.GameObject.SetParent( watchBack.GameObject );
		foreach ( var chat in hud.GetComponentsInChildren<ChatBox>( true ) ) chat.GameObject.SetParent( watchBack.GameObject );

		var ownerPlate = VRHudPanel.Create<VROwnerPlate>( "VR Owner Plate" );
		foreach ( var label in hud.GetComponentsInChildren<OwnerLabel>( true ) ) label.GameObject.SetParent( ownerPlate.GameObject );

		var signals = VRHudPanel.Create<VRSignals>( "VR Signals" );
		foreach ( var overlay in hud.GetComponentsInChildren<SignalsOverlay>( true ) ) overlay.GameObject.SetParent( signals.GameObject );

		var loadRing = VRHudPanel.Create<VRLoadRing>( "VR Load Ring" );
		foreach ( var ring in hud.GetComponentsInChildren<CloudLoadRing>( true ) ) ring.GameObject.SetParent( loadRing.GameObject );

		// Rebuild every panel under the HUD against the world panel
		hud.Enabled = true;
		await System.Threading.Tasks.Task.Yield();

		// The panels built against the screen got moved onto the world panel too - only keep the rebuilt ones
		var root = panel.GetPanel();
		if ( root is not null )
		{
			foreach ( var child in root.Children.ToList() )
			{
				if ( child != layer.Panel )
					child.Delete( true );
			}
		}

		var vrHud = hud.AddComponent<VRHud>();
		vrHud._menus = menus;
		vrHud._parts.Add( toolHints.GameObject );
		vrHud._parts.Add( notices.GameObject );
		vrHud._parts.Add( watchBack.GameObject );
		vrHud._parts.Add( ownerPlate.GameObject );
		vrHud._parts.Add( loadRing.GameObject );
		vrHud._parts.Add( signals.GameObject );
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

		_spawnLaser = _pointer.AddComponent<LineRenderer>();
		_spawnLaser.UseVectorPoints = true;
		_spawnLaser.Width = 0.25f;
		_spawnLaser.Color = Color.Cyan.WithAlpha( 0.6f );
		_spawnLaser.CastShadows = false;
		_spawnLaser.Opaque = false;

		_yaw = float.NaN;

		_parts.Add( VRHudPanel.Create<VRDamagePanel>( "VR Damage" ).GameObject );
		_parts.Add( VRHudPanel.Create<VRWatch>( "VR Watch" ).GameObject );
		_parts.Add( VRHudPanel.Create<VRWeaponAmmo>( "VR Weapon Ammo" ).GameObject );
		_parts.Add( VRHudPanel.Create<VRWeaponWheel>( "VR Weapon Wheel" ).GameObject );
		_parts.Add( VRHudPanel.Create<VRPrompt>( "VR Prompt" ).GameObject );
		_parts.Add( VRHudPanel.Create<VRAmmoPickups>( "VR Ammo Pickups" ).GameObject );
		_parts.Add( VRHudPanel.Create<VRKeyboard>( "VR Keyboard" ).GameObject );
		_parts.Add( VRHudPanel.Create<VRScope>( "VR Scope" ).GameObject );

		var aim = new GameObject( true, "VR Aim" );
		aim.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;
		aim.AddComponent<VRAimDot>();
		_parts.Add( aim );

		if ( _menus.IsValid() ) _parts.Add( _menus.GameObject );
	}

	protected override void OnDestroy()
	{
		if ( _pointer.IsValid() ) _pointer.Destroy();
		foreach ( var part in _parts )
		{
			if ( part.IsValid() ) part.Destroy();
		}
		IsPointing = false;
		ScoreboardOpen = false;
	}

	protected override void OnUpdate()
	{
		if ( !VRMode.IsActive || !_pointer.IsValid() ) return;

		var head = Input.VR.Head;
		var menuOpen = SpawnMenuHost.IsOpen || IsScoreboardOpen;

		PlaceHud( head, menuOpen );
		PlaceMenus( head, menuOpen );

		UpdatePointer( menuOpen );
		UpdateSpawnLaser();
	}

	static bool IsScoreboardOpen => Input.Down( "score" );

	bool _wasOnHand;
	Vector3 _handMenuLocal;

	/// <summary>
	/// The spawn menu floats above the off hand, facing the player, so it goes wherever the hand does.
	/// </summary>
	void PlaceOnHand( Transform head )
	{
		if ( !_menus.IsValid() ) return;

		// Softened in the play area's space, or it would trail behind the hand while walking or driving
		var anchor = Input.VR.Anchor;
		var hand = VRMode.OffHand.Transform.Position;
		var away = (hand - head.Position).WithZ( 0 ).Normal;
		var target = anchor.PointToLocal( hand + away * HandMenuDistance + Vector3.Up * HandMenuHeight );

		// Left up with a tap, it stays where it is and the hand's free
		if ( SpawnMenuHost.IsPinned && _wasOnHand ) target = _handMenuLocal;

		var t = 1f - MathF.Exp( -(IsPointing ? HandMenuAimingFollowSpeed : HandMenuFollowSpeed) * Time.Delta );
		_handMenuLocal = _wasOnHand ? Vector3.Lerp( _handMenuLocal, target, t ) : target;

		var position = anchor.PointToWorld( _handMenuLocal );

		_menus.WorldPosition = position;
		_menus.WorldRotation = Rotation.LookAt( head.Position - position, Vector3.Up );
		_menus.WorldScale = HandMenuScale;
	}

	/// <summary>
	/// Shows where the spawn menu's items will land: where the off hand points, see <see cref="Player.SpawnTransform"/>.
	/// </summary>
	void UpdateSpawnLaser()
	{
		_spawnLaser.Enabled = SpawnMenuHost.IsOpen;
		if ( !_spawnLaser.Enabled ) return;

		_spawnLaser.VectorPoints = new List<Vector3> { VRMode.OffHand.AimTransform.Position, SpawnAimPoint( Scene ) };
	}

	/// <summary>
	/// Where the off hand points, for what's being spawned.
	/// </summary>
	public static Vector3 SpawnAimPoint( Scene scene )
	{
		var aim = VRMode.OffHand.AimTransform;

		return scene.Trace.Ray( aim.Position, aim.Position + aim.Forward * 2048 )
			.WithoutTags( "player" )
			.Run().EndPosition;
	}

	/// <summary>
	/// What's left of the screen HUD follows the view, just softened, and gets out of the way of a menu.
	/// </summary>
	void PlaceHud( Transform head, bool menuOpen )
	{
		var look = head.Rotation.Angles();
		var pitch = look.pitch.Clamp( -60f, 60f );

		if ( float.IsNaN( _yaw ) )
		{
			_yaw = look.yaw;
			_pitch = pitch;
		}

		var t = 1f - MathF.Exp( -FollowSpeed * Time.Delta );
		_yaw += Angles.NormalizeAngle( look.yaw - _yaw ) * t;
		_pitch += (pitch - _pitch) * t;

		var rotation = Rotation.From( _pitch, _yaw, 0 );
		var forward = rotation.Forward;

		WorldPosition = head.Position + forward * Distance + rotation.Down * Drop;
		WorldRotation = Rotation.LookAt( -forward, rotation.Up );
		WorldScale = Scale;

		var root = GetComponent<Sandbox.WorldPanel>()?.GetPanel();
		if ( root is not null )
			root.Style.Opacity = menuOpen ? 0f : 1f;
	}

	/// <summary>
	/// The spawn menu goes on the hand. Others open where you're looking and stay put while they're used. Only clickable
	/// while one's open, the closed ones would catch the laser.
	/// </summary>
	void PlaceMenus( Transform head, bool menuOpen )
	{
		if ( !_menus.IsValid() ) return;

		_menus.InteractionRange = menuOpen ? 2000f : 0f;

		if ( SpawnMenuHost.IsOpen )
		{
			PlaceOnHand( head );
		}
		else if ( menuOpen )
		{
			if ( !_wasMenuOpen || _wasOnHand )
			{
				var look = head.Rotation.Angles();
				_menuYaw = look.yaw;
				_menuPitch = look.pitch.Clamp( -60f, 60f );
			}

			var rotation = Rotation.From( _menuPitch, _menuYaw, 0 );

			_menus.WorldPosition = head.Position + rotation.Forward * MenuDistance;
			_menus.WorldRotation = Rotation.LookAt( -rotation.Forward, rotation.Up );
			_menus.WorldScale = Scale;
		}

		_wasOnHand = SpawnMenuHost.IsOpen;
		_wasMenuOpen = menuOpen;
	}

	/// <summary>
	/// Where the laser meets the panel: a very short, wide, round-capped line is a dot.
	/// </summary>
	LineRenderer CreateDot()
	{
		var dot = new GameObject( true, "VR Pointer Dot" );
		dot.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;
		dot.SetParent( _pointer );

		var line = dot.AddComponent<LineRenderer>();
		line.UseVectorPoints = true;
		line.Width = 0.6f;
		line.StartCap = SceneLineObject.CapStyle.Rounded;
		line.EndCap = SceneLineObject.CapStyle.Rounded;
		line.Color = Color.White;
		line.CastShadows = false;
		return line;
	}

	void UpdatePointer( bool menuOpen )
	{
		if ( !_dot.IsValid() ) _dot = CreateDot();

		var aim = StabilizedAim( VRMode.DominantHand.AimTransform );
		_pointer.WorldTransform = aim;

		IsPointing = _input.Hovered.IsValid();
		UpdatePointerHaptics();
		UpdateSecondaryButton();
		KeepPopupsOnPanel();
		UpdateMenuPanel();

		var showLaser = menuOpen || IsPointing || VRKeyboard.IsOpen;
		_laser.Enabled = showLaser;

		// Only stop on the panel over actual UI - the plane goes on forever past the menu's edges
		var hit = IsPointing ? HitPanel( aim.ForwardRay ) : null;
		_dot.Enabled = hit.HasValue;

		if ( !showLaser ) return;

		// The laser stops on the panel, with a dot where it touches
		var end = hit ?? aim.Position + aim.Forward * MenuDistance * 1.5f;

		// Inspecting, it's what it lands on in the world that gets picked
		if ( !hit.HasValue && InspectCursor.IsOpen )
			end = Scene.Trace.Ray( aim.ForwardRay, 4096 ).IgnoreGameObjectHierarchy( Player.FindLocalPlayer()?.GameObject ).Run().EndPosition;

		_laser.VectorPoints = new List<Vector3> { aim.Position, end };

		if ( hit is { } point )
		{
			var towardsViewer = (point - aim.Position).Normal * -0.1f;
			_dot.VectorPoints = new List<Vector3> { point + towardsViewer, point + towardsViewer * 1.5f };
		}
	}

	Sandbox.UI.Panel _magnet;
	Sandbox.UI.Panel _frozenRoot;
	Vector2 _frozenPoint;
	bool _triggerStarted;
	RealTimeSince _sinceFrozen;

	/// <summary>
	/// The laser, kept steady: the rest of the UI goes by where it lands, so this is all it takes. It stays on a button until it's
	/// clearly left it, and holds still while the trigger goes down, since pulling it moves the hand.
	/// </summary>
	Transform StabilizedAim( Transform aim )
	{
		var hovered = _input.Hovered;

		if ( hovered != _magnet )
			_magnet = hovered.IsValid() && hovered.Box.Rect.Width < MagnetMaxSize && hovered.Box.Rect.Height < MagnetMaxSize ? hovered : null;

		var trigger = VRMode.DominantHand.Trigger.Value;
		var started = trigger > ClickFreezeTrigger;

		if ( started && !_triggerStarted && hovered.IsValid() )
		{
			_frozenRoot = hovered.FindRootPanel();
			_sinceFrozen = 0;

			// From where the laser was last frame, this frame's has already moved
			if ( !_frozenRoot.RayToLocalPosition( _pointer.WorldTransform.ForwardRay, out _frozenPoint, out _ ) )
				_frozenRoot = null;
		}

		_triggerStarted = started;

		if ( !started || _sinceFrozen > ClickFreezeTime )
			_frozenRoot = null;

		if ( _frozenRoot.IsValid() )
			return AimAtPoint( aim, _frozenRoot, _frozenPoint );

		if ( !_magnet.IsValid() ) return aim;

		var root = _magnet.FindRootPanel();
		if ( !root.IsValid() || !root.RayToLocalPosition( aim.ForwardRay, out var point, out var distance ) )
		{
			_magnet = null;
			return aim;
		}

		var rect = _magnet.Box.Rect;
		if ( rect.IsInside( point ) ) return aim;

		var reach = distance * MathF.Tan( MathX.DegreeToRadian( MagnetAngle ) ) * PixelsPerUnit( root, aim, point );
		if ( !rect.Grow( reach ).IsInside( point ) )
		{
			_magnet = null;
			return aim;
		}

		var inside = rect.Shrink( 1f );
		return AimAtPoint( aim, root, new Vector2( point.x.Clamp( inside.Left, inside.Right ), point.y.Clamp( inside.Top, inside.Bottom ) ) );
	}

	/// <summary>
	/// How many of the panel's pixels make a world unit, measured by moving a ray along it.
	/// </summary>
	float PixelsPerUnit( Sandbox.UI.Panel root, Transform aim, Vector2 point )
	{
		var ray = aim.ForwardRay;
		if ( !root.RayToLocalPosition( ray, out _, out var distance ) ) return 1f;

		var world = ray.Position + ray.Forward * distance;
		var moved = world + GetPanelRotation( root ).Right;
		var sideways = new Ray( ray.Position, (moved - ray.Position).Normal );

		return root.RayToLocalPosition( sideways, out var other, out _ ) ? (other - point).Length : 1f;
	}

	/// <summary>
	/// The laser from the same hand, turned onto a point on the panel.
	/// </summary>
	Transform AimAtPoint( Transform aim, Sandbox.UI.Panel root, Vector2 target )
	{
		var ray = aim.ForwardRay;
		if ( !root.RayToLocalPosition( ray, out var point, out var distance ) ) return aim;

		var rotation = GetPanelRotation( root );
		var world = ray.Position + ray.Forward * distance;

		// One unit each way along the panel shows how its pixels lie, whichever way it's facing
		var right = new Ray( ray.Position, (world + rotation.Right - ray.Position).Normal );
		var up = new Ray( ray.Position, (world + rotation.Up - ray.Position).Normal );
		if ( !root.RayToLocalPosition( right, out var alongRight, out _ ) || !root.RayToLocalPosition( up, out var alongUp, out _ ) ) return aim;

		var perRight = alongRight.x - point.x;
		var perUp = alongUp.y - point.y;
		if ( MathF.Abs( perRight ) < 0.001f || MathF.Abs( perUp ) < 0.001f ) return aim;

		var offset = rotation.Right * ((target.x - point.x) / perRight) + rotation.Up * ((target.y - point.y) / perUp);

		return new Transform( aim.Position, Rotation.LookAt( world + offset - aim.Position, aim.Rotation.Up ) );
	}

	static Rotation GetPanelRotation( Sandbox.UI.Panel root ) => root is Sandbox.UI.WorldPanel world ? world.Rotation : Rotation.Identity;

	/// <summary>
	/// Context menus open under the screen's mouse, which has nothing to do with a world panel and leaves them off the edge of it.
	/// They go where the laser is instead, all on the panel.
	/// </summary>
	void KeepPopupsOnPanel()
	{
		var root = _menus?.GetPanel();
		if ( root is null ) return;

		var bounds = root.Box.Rect;
		_placedPopups.RemoveWhere( x => !x.IsValid() );

		foreach ( var popup in root.Children.OfType<Sandbox.UI.Popup>() )
		{
			var rect = popup.Box.Rect;
			if ( rect.Width <= 0f ) continue;

			var position = rect.Position;

			if ( popup.Position == Sandbox.UI.Popup.PositionMode.UnderMouse && _placedPopups.Add( popup ) && root.RayToLocalPosition( _pointer.WorldTransform.ForwardRay, out var point, out _ ) )
				position = point;
			else if ( bounds.IsInside( rect, true ) )
				continue;

			position.x = position.x.Clamp( bounds.Left, MathF.Max( bounds.Left, bounds.Right - rect.Width ) );
			position.y = position.y.Clamp( bounds.Top, MathF.Max( bounds.Top, bounds.Bottom - rect.Height ) );

			popup.Style.Left = Sandbox.UI.Length.Pixels( (position.x - bounds.Left) * root.ScaleFromScreen );
			popup.Style.Top = Sandbox.UI.Length.Pixels( (position.y - bounds.Top) * root.ScaleFromScreen );
		}
	}

	readonly HashSet<Sandbox.UI.Popup> _placedPopups = new();

	VRHintBar _hintBar;

	/// <summary>
	/// The class everything on the menus' panel goes by for its size in a headset, and the bar that stands in for tooltips.
	/// </summary>
	void UpdateMenuPanel()
	{
		var root = _menus?.GetPanel();
		if ( root is null ) return;

		root.SetClass( "vr", true );

		if ( !_hintBar.IsValid() ) _hintBar = root.AddChild<VRHintBar>();

		_hintBar.ShowFor( _input.Hovered );
	}

	/// <summary>
	/// B closes what's on top of the menu, otherwise it's the right click, which the engine's input doesn't do in VR.
	/// </summary>
	void UpdateSecondaryButton()
	{
		if ( !IsPointing || !Input.Pressed( "attack2" ) ) return;
		if ( SpawnMenuHost.IsOpen && SpawnMenuHost.CloseOverlay() ) return;

		var hovered = _input.Hovered;
		foreach ( var name in new[] { "onmousedown", "onmouseup", "onrightclick" } )
			hovered.CreateEvent( new Sandbox.UI.MousePanelEvent( name, hovered, "mouseright" ) { MouseButton = MouseButtons.Right } );
	}

	Sandbox.UI.Panel _lastHovered;
	RealTimeSince _timeSinceHoverPulse;

	/// <summary>
	/// A tick when the laser moves onto something new, and a firmer one on clicking. Sweeping across a menu passes
	/// over many panels quickly, so the ticks are spaced out.
	/// </summary>
	void UpdatePointerHaptics()
	{
		if ( IsPointing && Input.Pressed( "attack1" ) )
			VRMode.PulseDominant( 0.35f, 0.3f );

		var hovered = _input.Hovered;
		if ( hovered == _lastHovered ) return;
		_lastHovered = hovered;

		if ( !hovered.IsValid() || _timeSinceHoverPulse < 0.08f ) return;
		_timeSinceHoverPulse = 0;

		VRMode.PulseDominant( 0.1f, 0.2f );
	}

	/// <summary>
	/// The object of the panel the laser's on.
	/// </summary>
	GameObject PointedPanel()
	{
		var root = _input.Hovered?.FindRootPanel();
		if ( root is null ) return null;

		return Scene.GetAllComponents<Sandbox.WorldPanel>().FirstOrDefault( x => x.GetPanel() == root )?.GameObject;
	}

	/// <summary>
	/// Where a ray crosses the panel's plane, in front of the hand.
	/// </summary>
	Vector3? HitPanel( Ray ray )
	{
		var panel = PointedPanel();
		if ( !panel.IsValid() ) return null;

		var normal = panel.WorldRotation.Forward;
		var denominator = Vector3.Dot( ray.Forward, normal );
		if ( MathF.Abs( denominator ) < 0.0001f ) return null;

		var distance = Vector3.Dot( panel.WorldPosition - ray.Position, normal ) / denominator;
		if ( distance <= 0f || distance > MenuDistance * 3f ) return null;

		return ray.Position + ray.Forward * distance;
	}
}
