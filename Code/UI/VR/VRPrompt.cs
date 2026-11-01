/// <summary>
/// What pressing use would do, on the thing itself, with the button it's on: the screen HUD's <see cref="PressableSection"/>
/// where the eyes land on it instead of under a crosshair, and a tick in the hand when there's something new to use.
/// </summary>
public sealed class VRPrompt : VRHudPanel
{
	/// <summary>
	/// How wide the panel is, in world units.
	/// </summary>
	[Property] public float Width { get; set; } = 14f;

	/// <summary>
	/// How far towards the player the prompt comes off the surface, so it doesn't sink into it.
	/// </summary>
	[Property] public float SurfaceGap { get; set; } = 6f;

	protected override Vector2 PanelPixels => new( 800, 400 );

	PressableSection _section;
	Component _lastHovered;
	Vector3 _target;

	protected override void Place( Transform head )
	{
		var position = _target + (head.Position - _target).Normal * SurfaceGap;
		PlaceAt( position, Rotation.LookAt( head.Position - position, Vector3.Up ), Width );
	}

	protected override void OnUpdate()
	{
		_section ??= Canvas?.Section<PressableSection>();
		if ( _section is null ) return;

		var player = VRPlayer.Local?.Player;
		var controller = player?.Controller;

		if ( !controller.IsValid() || !Hud.IsVisible( HudElement.InteractionHints ) )
		{
			_section.Hint = null;
			_lastHovered = null;
			return;
		}

		var hint = player.InteractionHint;
		var hovered = hint.HasValue ? controller.Hovered : null;

		_section.Hint = hint;
		_section.KeyLabel = VRInputNames.Button( controller.UseButton )?.ToUpperInvariant();

		if ( hovered.IsValid() )
		{
			var eye = controller.EyeTransform;
			var tr = Scene.Trace.Ray( eye.Position, eye.Position + eye.Rotation.Forward * controller.ReachLength )
				.IgnoreGameObjectHierarchy( player.GameObject.Root )
				.Run();

			_target = tr.Hit ? tr.HitPosition : hovered.WorldPosition;

			if ( hovered != _lastHovered )
				VRMode.PulseDominant( 0.12f, 0.2f );
		}

		_lastHovered = hovered;
	}
}
