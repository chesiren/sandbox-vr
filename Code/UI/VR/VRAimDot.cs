/// <summary>
/// Where the held weapon points: a dot on the surface in the world, the HUD's colour, and a short line from the hand. There's
/// no crosshair in a headset, the screen overlay it paints on is stuck in front of the eyes.
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
	LineRenderer _dot;

	protected override void OnStart()
	{
		_line = CreateLine( "VR Aim Line", 0.15f );
		_dot = CreateLine( "VR Aim Dot", MinDotWidth );
		_dot.StartCap = SceneLineObject.CapStyle.Rounded;
		_dot.EndCap = SceneLineObject.CapStyle.Rounded;
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
		if ( !_dot.IsValid() || !_line.IsValid() ) return;

		var vr = VRPlayer.Local;
		var weapon = vr?.Player.GetComponent<PlayerInventory>()?.ActiveWeapon;
		var show = weapon.IsValid() && weapon is not CameraWeapon && Hud.IsVisible( HudElement.Crosshair ) && !vr.PointingAtUI && !SpawnMenuHost.IsOpen && !vr.WheelOpen;

		_line.Enabled = show;
		_dot.Enabled = false;
		if ( !show ) return;

		var aim = VRMode.DominantHand.AimTransform;
		var tint = Scene.Get<HudLayer>()?.CurrentTint ?? HudLayer.DefaultTint;

		_line.Color = tint.WithAlpha( 0.45f );
		_line.VectorPoints = new List<Vector3> { aim.Position, aim.Position + aim.Rotation.Forward * LineLength };

		var tr = Scene.Trace.Ray( aim.Position, aim.Position + aim.Rotation.Forward * MaxDistance )
			.IgnoreGameObjectHierarchy( vr.GameObject.Root )
			.Run();

		if ( !tr.Hit ) return;

		// Lifted off the surface, or it sinks into it
		var point = tr.HitPosition + tr.Normal * 0.5f;

		_dot.Enabled = true;
		_dot.Color = tint;
		_dot.Width = MathF.Max( MinDotWidth, tr.Distance * DotSize );
		_dot.VectorPoints = new List<Vector3> { point, point + tr.Normal * 0.01f };
	}
}
