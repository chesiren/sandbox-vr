using Sandbox.UI;

/// <summary>
/// The other side of the watch, seen with the palm turned up: the buttons for what has no button on the controllers. The laser
/// clicks them, so it only takes the laser while it's facing the player.
/// </summary>
public sealed class VRWatchBack : VRHudPanel
{
	/// <summary>
	/// How wide the panel is, in world units.
	/// </summary>
	[Property] public float Width { get; set; } = 9f;

	protected override Vector2 PanelPixels => new( 640, 240 );

	float _visibility;

	protected override float Visibility => _visibility;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( Panel is null ) return;

		Panel.Style.PointerEvents = PointerEvents.All;

		if ( Panel.ChildrenCount > 0 ) return;

		var scores = Panel.Add.Panel( "button" );
		scores.AddChild( new Label { Text = Game.Language.GetPhrase( "vr.watch.scores" ) } );
		scores.AddEventListener( "onclick", () => VRHud.ScoreboardOpen = !VRHud.ScoreboardOpen );
	}

	protected override void Place( Transform head )
	{
		var hand = VRWatch.Hand.AimTransform;
		var face = -VRWatch.FaceDirection( hand );
		var watch = Scene.Get<VRWatch>();
		var position = hand.Position - hand.Rotation.Forward * (watch?.WristDistance ?? 5f) + face * (watch?.FaceHeight ?? 4.5f);

		PlaceAt( position, Rotation.LookAt( face, hand.Rotation.Up ), Width );

		var toHead = (head.Position - position).Normal;
		var lookedAt = Vector3.Dot( face, toHead ) > 0.4f;
		_visibility = _visibility.Approach( lookedAt ? 1f : 0f, Time.Delta * 8f );

		InteractionRange = lookedAt ? 2000f : 0f;
	}
}
