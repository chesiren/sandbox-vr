/// <summary>
/// A laser sight. The hand on <see cref="GrabPoint"/> turns it on and off by clicking its stick. Ported from gunbuilder.
/// </summary>
public sealed class LaserPointerComponent : Component, Component.ExecuteInEditor
{
	[Property] public Color LaserColor { get; set; }

	[Property] public float LineWidth { get; set; } = 0.05f;

	[Property] public float DotSize { get; set; } = 0.6f;

	LineRenderer LineRenderer { get; set; }
	LineRenderer DotRenderer { get; set; }

	/// <summary>
	/// On or off, synced from the gun's owner.
	/// </summary>
	[Property, Sync, Change( nameof( OnIsEnabledChanged ) )] public bool IsEnabled { get; set; } = true;

	[Property] public GrabPoint GrabPoint { get; set; }

	TimeSince TimeSinceToggled { get; set; } = 1;

	const float Dist = 100000;

	void OnIsEnabledChanged( bool oldValue, bool newValue )
	{
		if ( Active )
			SetEnabled( newValue );
	}

	Vector3 GetTraceEnd()
	{
		var tr = Scene.Trace.Ray( WorldPosition, WorldPosition + WorldRotation.Forward * Dist )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.WithoutTags( "trigger" )
			.Run();

		return tr.EndPosition;
	}

	LineRenderer CreateRenderer( float width, bool roundCaps )
	{
		var renderer = Components.Create<LineRenderer>();
		renderer.Flags = ComponentFlags.NotSaved | ComponentFlags.Hidden;
		renderer.UseVectorPoints = true;
		renderer.Color = LaserColor;
		renderer.Width = width;
		renderer.Additive = true;
		renderer.Opaque = false;
		renderer.Lighting = false;
		renderer.CastShadows = false;

		if ( roundCaps )
		{
			renderer.StartCap = SceneLineObject.CapStyle.Rounded;
			renderer.EndCap = SceneLineObject.CapStyle.Rounded;
		}

		return renderer;
	}

	void UpdateRenderers()
	{
		if ( !LineRenderer.IsValid() && !DotRenderer.IsValid() ) return;

		var end = GetTraceEnd();

		if ( LineRenderer.IsValid() )
			LineRenderer.VectorPoints = new() { WorldPosition, end };

		if ( DotRenderer.IsValid() )
		{
			// A tiny segment with round caps reads as a dot
			var back = (WorldPosition - end).Normal * 0.01f;
			DotRenderer.VectorPoints = new() { end + back, end + back * 2f };
		}
	}

	public void SetEnabled( bool enabled )
	{
		LineRenderer?.Destroy();
		DotRenderer?.Destroy();
		LineRenderer = null;
		DotRenderer = null;

		if ( !enabled ) return;

		LineRenderer = CreateRenderer( LineWidth, false );
		DotRenderer = CreateRenderer( DotSize, true );
		UpdateRenderers();
	}

	protected override void OnEnabled() => SetEnabled( IsEnabled );

	protected override void OnDisabled() => SetEnabled( false );

	protected override void OnUpdate()
	{
		UpdateRenderers();

		if ( IsProxy || !GrabPoint.IsValid() || GrabPoint.HeldHand?.Controller is not { } controller ) return;

		if ( controller.JoystickPress.IsPressed && TimeSinceToggled > 0.5f )
		{
			TimeSinceToggled = 0;
			IsEnabled = !IsEnabled;
		}
	}
}
