/// <summary>
/// Where the held weapon points: a dot on the surface in the world, the HUD's colour, and a short line from the hand. Sitting
/// with nothing in hand, a dot for each of the seat's weapons instead. There's no crosshair in a headset, the screen overlay it
/// paints on is stuck in front of the eyes.
/// </summary>
public sealed class VRAimDot : Component
{
	/// <summary>
	/// How long the line from the hand is.
	/// </summary>
	[Property] public float LineLength { get; set; } = 12f;

	/// <summary>
	/// How big the dot looks, as its width per unit of distance. It stays the same size from any distance.
	/// </summary>
	[Property] public float DotSize { get; set; } = 0.012f;

	const float MaxDistance = 4096f;
	const float MinDotWidth = 0.4f;

	LineRenderer _line;
	readonly List<LineRenderer> _dots = new();

	protected override void OnStart()
	{
		_line = CreateLine( "VR Aim Line", 0.15f );
	}

	LineRenderer GetDot( int index )
	{
		while ( _dots.Count <= index )
		{
			var dot = CreateLine( "VR Aim Dot", MinDotWidth );
			dot.StartCap = SceneLineObject.CapStyle.Rounded;
			dot.EndCap = SceneLineObject.CapStyle.Rounded;
			_dots.Add( dot );
		}

		return _dots[index];
	}

	LineRenderer CreateLine( string name, float width )
	{
		var go = new GameObject( true, name );
		go.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;
		go.SetParent( GameObject );

		var line = go.AddComponent<LineRenderer>();
		line.UseVectorPoints = true;
		line.Width = width;
		line.CastShadows = false;
		line.Opaque = false;
		return line;
	}

	protected override void OnUpdate()
	{
		if ( !_line.IsValid() ) return;

		_line.Enabled = false;
		foreach ( var dot in _dots ) dot.Enabled = false;

		var vr = VRPlayer.Local;
		if ( vr is null || !Hud.IsVisible( HudElement.Crosshair ) || vr.PointingAtUI || SpawnMenuHost.IsOpen || vr.WheelOpen ) return;

		var tint = Scene.Get<HudLayer>()?.CurrentTint ?? HudLayer.DefaultTint;
		var weapon = vr.Player.GetComponent<PlayerInventory>()?.ActiveWeapon;

		if ( weapon.IsValid() )
		{
			if ( weapon is CameraWeapon ) return;

			var aim = VRMode.DominantHand.AimTransform;

			_line.Enabled = true;
			_line.Color = tint.WithAlpha( 0.45f );
			_line.VectorPoints = new List<Vector3> { aim.Position, aim.Position + aim.Rotation.Forward * LineLength };

			PlaceDot( 0, aim.ForwardRay, vr.GameObject.Root, tint );
			return;
		}

		var index = 0;
		foreach ( var seated in vr.Player.SeatedWeapons )
		{
			// Targeted aim fires where the camera looks, which is the head here
			var ray = seated.IsTargetedAim ? Input.VR.Head.ForwardRay : seated.AimRay;
			PlaceDot( index++, ray, seated.AimIgnoreRoot, tint );
		}
	}

	void PlaceDot( int index, Ray ray, GameObject ignore, Color tint )
	{
		var tr = Scene.Trace.Ray( ray.Position, ray.Position + ray.Forward * MaxDistance )
			.IgnoreGameObjectHierarchy( ignore )
			.Run();

		if ( !tr.Hit ) return;

		// Lifted off the surface, or it sinks into it
		var point = tr.HitPosition + tr.Normal * 0.5f;

		var dot = GetDot( index );
		dot.Enabled = true;
		dot.Color = tint;
		dot.Width = MathF.Max( MinDotWidth, tr.Distance * DotSize );
		dot.VectorPoints = new List<Vector3> { point, point + tr.Normal * 0.01f };
	}
}
