using Sandbox.UI;

/// <summary>
/// One small piece of the VR HUD: a world panel of its own, placed every frame by <see cref="Place"/> rather than sharing
/// the screen's layout. Painted HUD goes through <see cref="Canvas"/>, the same canvas and sections as the screen's HUD, so
/// it keeps the look and the player's HUD colour. Never present outside VR, see <see cref="VRHud"/>.
/// </summary>
public abstract class VRHudPanel : PanelComponent
{
	/// <summary>
	/// World units per panel pixel at a world scale of 1, to size a panel in world units.
	/// </summary>
	const float UnitsPerPixel = 0.1f;

	/// <summary>
	/// The panel's size in panel pixels.
	/// </summary>
	protected virtual Vector2 PanelPixels => new( 1024, 1024 );

	/// <summary>
	/// How visible the panel is right now, on top of the player's HUD opacity.
	/// </summary>
	protected virtual float Visibility => 1f;

	/// <summary>
	/// How far away the laser can click the panel. Nothing by default, see <see cref="Create{T}"/>.
	/// </summary>
	protected float InteractionRange
	{
		set
		{
			if ( GetComponent<Sandbox.WorldPanel>() is { } world )
				world.InteractionRange = value;
		}
	}

	/// <summary>
	/// The canvas sections paint into, the first time it's needed.
	/// </summary>
	protected HudCanvas Canvas
	{
		get
		{
			if ( _canvas.IsValid() || Panel is null ) return _canvas;

			_canvas = new HudCanvas { Layer = Scene.Get<HudLayer>() };
			Panel.AddChild( _canvas );
			return _canvas;
		}
	}

	HudCanvas _canvas;

	/// <summary>
	/// Makes a panel on an object of its own. Only the laser's own panels may catch it, so this one can't be clicked:
	/// anything the laser hovers keeps the trigger from the weapon.
	/// </summary>
	public static T Create<T>( string name ) where T : VRHudPanel, new()
	{
		var go = new GameObject( true, name );
		go.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;

		var world = go.AddComponent<Sandbox.WorldPanel>();
		world.LookAtCamera = false;
		world.InteractionRange = 0f;

		var panel = go.AddComponent<T>();
		world.PanelSize = panel.PanelPixels;

		return panel;
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( Panel is null ) return;

		Panel.Style.PointerEvents = PointerEvents.None;
		Panel.Style.Position = PositionMode.Absolute;
		Panel.Style.Width = Length.Percent( 100 );
		Panel.Style.Height = Length.Percent( 100 );
	}

	/// <summary>
	/// Moves the panel for this frame. Width is the size it should be in the world, in world units.
	/// </summary>
	protected void PlaceAt( Vector3 position, Rotation rotation, float width )
	{
		WorldPosition = position;
		WorldRotation = rotation;
		WorldScale = width / (PanelPixels.x * UnitsPerPixel);
	}

	/// <summary>
	/// Where the panel goes this frame, see <see cref="PlaceAt"/>.
	/// </summary>
	protected abstract void Place( Transform head );

	protected override void OnPreRender()
	{
		if ( !VRMode.IsActive ) return;

		Place( Input.VR.Head );

		// On the root, what's been moved onto this object from the screen HUD sits next to the panel
		if ( GetComponent<Sandbox.WorldPanel>()?.GetPanel() is { } root )
			root.Style.Opacity = VRSettings.Current.HudOpacity * Visibility;
	}
}
