/// <summary>
/// Wires and the wire tool's card in the world, for the screen HUD's <see cref="SignalsOverlay"/>, which keeps finding what to
/// show. It projects them onto the screen otherwise, which a headset doesn't have: here the wires are lines between the objects
/// and the card sits on this panel, above the object it's about.
/// </summary>
public sealed class VRSignals : VRHudPanel
{
	/// <summary>
	/// How wide the panel is close up, in world units.
	/// </summary>
	[Property] public float Width { get; set; } = 18f;

	/// <summary>
	/// How big the card looks from further away, as the panel's width per unit of distance. It stays readable from any distance.
	/// </summary>
	[Property] public float Size { get; set; } = 0.4f;

	/// <summary>
	/// How far above the object the card floats.
	/// </summary>
	[Property] public float Gap { get; set; } = 4f;

	/// <summary>
	/// How thick the wires are.
	/// </summary>
	[Property] public float WireWidth { get; set; } = 0.3f;

	protected override Vector2 PanelPixels => new( 800, 800 );

	readonly List<LineRenderer> _wires = new();
	SignalsOverlay _overlay;

	protected override void Place( Transform head )
	{
		var target = _overlay?.CardTarget;
		if ( !target.IsValid() ) return;

		var bounds = target.GetBounds();
		var width = MathF.Max( Width, head.Position.Distance( bounds.Center ) * Size );

		// The card hangs from the bottom of the panel, so that's what goes on the object
		var position = bounds.Center + Vector3.Up * (bounds.Size.z * 0.5f + Gap + width * 0.5f);

		PlaceAt( position, Rotation.LookAt( head.Position - position, Vector3.Up ), width );
	}

	protected override void OnUpdate()
	{
		_overlay ??= GameObject.GetComponentInChildren<SignalsOverlay>( true );

		var tint = Scene.Get<HudLayer>()?.CurrentTint ?? HudLayer.DefaultTint;
		var used = 0;

		if ( _overlay.IsValid() )
		{
			foreach ( var wire in _overlay.Wires )
			{
				if ( !wire.Source.IsValid() || !wire.Target.IsValid() ) continue;

				var line = GetWire( used++ );
				line.Enabled = true;
				line.Color = tint.WithAlpha( 0.8f );
				line.VectorPoints = new List<Vector3> { _overlay.WorldCenter( wire.Source ), _overlay.WorldCenter( wire.Target ) };
			}
		}

		for ( var i = used; i < _wires.Count; i++ )
			_wires[i].Enabled = false;
	}

	LineRenderer GetWire( int index )
	{
		while ( _wires.Count <= index )
		{
			var go = new GameObject( true, "VR Wire" );
			go.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;
			go.SetParent( GameObject );

			var line = go.AddComponent<LineRenderer>();
			line.UseVectorPoints = true;
			line.Width = WireWidth;
			line.CastShadows = false;
			line.Opaque = false;
			_wires.Add( line );
		}

		return _wires[index];
	}
}
