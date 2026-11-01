using Sandbox.VR;

/// <summary>
/// Shows where damage came from in a headset: the same indicators as the screen's, out at the edge of the view instead of
/// around a crosshair, and a pulse in the hand on that side. The one thing that's stuck to the head, so it stays out of the
/// way and only shows for a moment.
/// </summary>
public sealed class VRDamagePanel : VRHudPanel
{
	/// <summary>
	/// How far in front of the eyes the indicators are.
	/// </summary>
	[Property] public float Distance { get; set; } = 24f;

	/// <summary>
	/// How wide the panel is, in world units. The indicators sit near its edge.
	/// </summary>
	[Property] public float Width { get; set; } = 60f;

	protected override Vector2 PanelPixels => new( 1400, 1400 );

	protected override void Place( Transform head )
	{
		PlaceAt( head.Position + head.Forward * Distance, Rotation.LookAt( -head.Forward, head.Up ), Width );
	}

	/// <summary>
	/// While true, the indicators pulse all around the view, drawn with <paramref name="icon"/>.
	/// </summary>
	public void SetLowHealth( bool low, Texture icon )
	{
		if ( Canvas?.Section<VRDamageSection>() is { } section )
		{
			section.LowHealth = low;
			section.LowHealthIcon = icon;
		}
	}

	/// <summary>
	/// Shows damage from <paramref name="source"/>, drawn with the player's indicator texture.
	/// </summary>
	public void Add( Vector3 source, Texture icon )
	{
		Canvas?.Section<VRDamageSection>().Add( source, icon );

		var head = Input.VR.Head;
		var right = Vector3.Dot( source - head.Position, head.Right ) > 0f;
		VRMode.Pulse( right ? Input.VR.RightHand : Input.VR.LeftHand, 0.7f, 0.4f );
	}
}

/// <summary>
/// Paints <see cref="VRDamagePanel"/>'s indicators, working out their direction like <see cref="PlayerDamageIndicators"/>.
/// </summary>
public sealed class VRDamageSection : HudSection
{
	const float Lifetime = 2f;

	/// <summary>
	/// How many indicators ring the view at low health.
	/// </summary>
	const int LowHealthRing = 6;

	/// <summary>
	/// How far from the centre of the panel the indicators start, as a fraction of its width.
	/// </summary>
	const float EdgeDistance = 0.28f;

	readonly List<(Vector3 Source, Texture Icon, TimeSince Age)> _indicators = new();

	public bool LowHealth { get; set; }
	public Texture LowHealthIcon { get; set; }

	public void Add( Vector3 source, Texture icon ) => _indicators.Add( (source, icon, 0f) );

	public override void Tick() => _indicators.RemoveAll( x => x.Age >= Lifetime );

	public override void GetCompositeRegions( List<Rect> regions )
	{
		if ( _indicators.Count > 0 || (LowHealth && LowHealthIcon is not null) )
			regions.Add( CanvasRect );
	}

	public override void Paint( Painter painter )
	{
		var head = Input.VR.Head;
		var focalPoint = head.Position + head.Forward * 16f;

		if ( LowHealth && LowHealthIcon is not null )
		{
			var alpha = 0.2f + 0.15f * MathF.Sin( RealTime.Now * 3f );
			for ( var i = 0; i < LowHealthRing; i++ )
				DrawIndicator( painter, LowHealthIcon, 360f * i / LowHealthRing, alpha );
		}

		foreach ( var (source, icon, age) in _indicators )
		{
			var direction = (source - focalPoint).Normal;
			var angle = -MathF.Atan2( direction.y, direction.x ) + head.Rotation.Angles().yaw.DegreeToRadian() - (MathF.PI / 2f);

			DrawIndicator( painter, icon, angle.RadianToDegree(), 1f - (age / Lifetime) );
		}
	}

	static void DrawIndicator( Painter painter, Texture icon, float degrees, float alpha )
	{
		var bounds = painter.Bounds;
		var size = new Vector2( 0.11f, 0.22f ) * bounds.Width;

		using var scope = painter.Scope();
		painter.Translate( bounds.Center );
		painter.Rotate( degrees );

		var rect = new Rect( new Vector2( bounds.Width * EdgeDistance, -size.y / 2f ), size );
		icon.MarkUsed( (int)size.x ); // streaming only hears of what the engine draws itself
		painter.Texture( icon, rect, Color.Red.WithAlpha( alpha ) );
	}
}
