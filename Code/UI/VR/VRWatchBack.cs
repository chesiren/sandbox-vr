using Sandbox.UI;

/// <summary>
/// The other side of the watch, seen with the palm turned up: the buttons for what has no button on the controllers, and the
/// screen HUD's kill feed, voices and chat, moved here by <see cref="VRHud"/> so they're there when wanted and out of the way
/// otherwise. The laser clicks the buttons, so it only takes the laser while it's facing the player.
/// </summary>
public sealed class VRWatchBack : VRHudPanel
{
	/// <summary>
	/// How wide the panel is, in world units.
	/// </summary>
	[Property] public float Width { get; set; } = 14f;

	protected override Vector2 PanelPixels => new( 1000, 800 );

	Panel _chat;
	float _visibility;

	protected override float Visibility => _visibility;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( Panel is null ) return;

		if ( Panel.ChildrenCount > 0 ) return;

		AddButton( "vr.watch.scores", () => VRHud.ScoreboardOpen = !VRHud.ScoreboardOpen );
		_chat = AddButton( "vr.watch.chat", ToggleChat );
	}

	Panel AddButton( string phrase, Action action )
	{
		var button = Panel.Add.Panel( "button" );
		button.AddChild( new Label { Text = Game.Language.GetPhrase( phrase ) } );
		button.AddEventListener( "onclick", action );
		return button;
	}

	void ToggleChat()
	{
		if ( Scene.Get<ChatBox>() is not { } chat ) return;

		if ( !ChatBox.IsOpen ) chat.Open();
		else if ( VRKeyboard.IsOpen ) Scene.Get<VRKeyboard>().Cancel();
		else chat.Close();
	}

	protected override void OnUpdate()
	{
		if ( _chat is not null )
			_chat.Style.Display = Sandbox.Platform.Chat.Enabled && Hud.IsVisible( HudElement.Chat ) ? DisplayMode.Flex : DisplayMode.None;
	}

	protected override void Place( Transform head )
	{
		var hand = VRWatch.Hand.AimTransform;
		var face = -VRWatch.FaceDirection( hand );
		var watch = Scene.Get<VRWatch>();
		var position = hand.Position - hand.Rotation.Forward * (watch?.WristDistance ?? 5f) + face * (watch?.FaceHeight ?? 3f);

		PlaceAt( position, Rotation.LookAt( face, hand.Rotation.Up ), Width );

		var toHead = (head.Position - position).Normal;
		var lookedAt = Vector3.Dot( face, toHead ) > 0.4f;
		_visibility = _visibility.Approach( lookedAt ? 1f : 0f, Time.Delta * 8f );

		InteractionRange = lookedAt ? 2000f : 0f;
	}
}
