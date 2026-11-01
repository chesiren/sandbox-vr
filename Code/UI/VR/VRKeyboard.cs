using Sandbox.UI;

/// <summary>
/// Types into whichever text entry has focus, there's no keyboard in a headset and the engine doesn't offer one. Opens on its
/// own in front of the eyes, stays where it opened, and the laser clicks its keys: the chat, the menus' searches, anything.
/// </summary>
public sealed class VRKeyboard : VRHudPanel
{
	/// <summary>
	/// How wide the keyboard is, in world units.
	/// </summary>
	[Property] public float Width { get; set; } = 30f;

	/// <summary>
	/// How far in front of the eyes it opens.
	/// </summary>
	[Property] public float Distance { get; set; } = 24f;

	/// <summary>
	/// How far below the eyes it opens, within reach of the laser without covering the view.
	/// </summary>
	[Property] public float Drop { get; set; } = 8f;

	const string Digits = "1234567890";

	static readonly string[] Azerty = { "azertyuiop", "qsdfghjklm", "wxcvbn,.'", "éèêàçùô-?!" };
	static readonly string[] Qwerty = { "qwertyuiop", "asdfghjkl'", "zxcvbnm,.", "-_/:;()@?!" };

	/// <summary>
	/// The keyboard's up, so the trigger's for its keys rather than the weapon.
	/// </summary>
	public static bool IsOpen { get; private set; }

	protected override Vector2 PanelPixels => new( 1400, 680 );

	protected override float Visibility => IsOpen ? 1f : 0f;

	readonly List<(Label Label, char Character)> _letters = new();
	TextEntry _target;
	Label _preview;
	Vector3 _anchorLocal;
	bool _shift;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( Panel is null || Panel.ChildrenCount > 0 ) return;

		_preview = Panel.Add.Panel( "preview" ).AddChild<Label>();
		_preview.Tokenize = false;

		var rows = Game.Language.SelectedCode == "fr" ? Azerty : Qwerty;

		AddCharacters( Panel.Add.Panel( "row" ), Digits );
		AddCharacters( Panel.Add.Panel( "row" ), rows[0] );
		AddCharacters( Panel.Add.Panel( "row" ), rows[1] );

		var bottom = Panel.Add.Panel( "row" );
		AddKey( bottom, "arrow_upward", ToggleShift, "icon" );
		AddCharacters( bottom, rows[2] );
		AddKey( bottom, "backspace", Backspace, "icon" );

		AddCharacters( Panel.Add.Panel( "row" ), rows[3] );

		var last = Panel.Add.Panel( "row" );
		AddKey( last, "close", Cancel, "icon" );
		AddKey( last, "space_bar", () => Type( ' ' ), "icon space" );
		AddKey( last, "keyboard_return", Submit, "icon" );
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		IsOpen = false;
	}

	void AddCharacters( Panel row, string characters )
	{
		foreach ( var character in characters )
		{
			var label = AddKey( row, character.ToString(), () => Type( _shift ? char.ToUpperInvariant( character ) : character ) );
			_letters.Add( (label, character) );
		}
	}

	Label AddKey( Panel row, string text, Action action, string classes = null )
	{
		var key = row.Add.Panel( "key" );
		if ( classes is not null ) key.AddClass( classes );

		var label = key.AddChild<Label>();
		label.Tokenize = false;
		label.Text = text;

		key.AddEventListener( "onclick", action );

		return label;
	}

	protected override void Place( Transform head )
	{
		if ( !IsOpen ) return;

		var position = Input.VR.Anchor.PointToWorld( _anchorLocal );
		PlaceAt( position, Rotation.LookAt( head.Position - position, Vector3.Up ), Width );
	}

	protected override void OnUpdate()
	{
		if ( Panel is null ) return;

		var focused = (InputFocus.Current ?? InputFocus.Next) as TextEntry;
		if ( focused.IsValid() && focused != _target )
			Open( focused );

		// Clicking a key, or anything else, can take the focus away. Only its own keys and the menu's buttons close it
		if ( IsOpen && (!_target.IsValid() || Input.Pressed( "spawnmenu" ) || Input.Pressed( "inspectmenu" )) )
			Close();

		InteractionRange = IsOpen ? 2000f : 0f;
		if ( !IsOpen ) return;

		if ( !focused.IsValid() )
			_target.Focus();

		var text = _target.Text;
		var empty = string.IsNullOrEmpty( text );
		_preview.Text = empty ? _target.Placeholder : text;
		_preview.SetClass( "placeholder", empty );
	}

	void Open( TextEntry target )
	{
		_target = target;
		IsOpen = true;
		SetShift( false );

		var head = Input.VR.Head;
		var look = head.Rotation.Angles().WithRoll( 0 ).ToRotation();
		_anchorLocal = Input.VR.Anchor.PointToLocal( head.Position + look.Forward * Distance + look.Down * Drop );
	}

	void Close()
	{
		if ( _target.IsValid() ) _target.Blur();

		_target = null;
		IsOpen = false;
	}

	void Type( char character )
	{
		if ( !_target.IsValid() ) return;

		_target.OnKeyTyped( character );
		SetShift( false );
	}

	void Backspace()
	{
		if ( !_target.IsValid() ) return;

		var text = _target.Text ?? "";
		var caret = Math.Min( _target.CaretPosition, text.Length );
		if ( caret <= 0 ) return;

		_target.Text = text.Remove( caret - 1, 1 );
		_target.CaretPosition = caret - 1;
		_target.OnValueChanged();
	}

	void ToggleShift() => SetShift( !_shift );

	void SetShift( bool shift )
	{
		_shift = shift;

		foreach ( var (label, character) in _letters )
			label.Text = (shift ? char.ToUpperInvariant( character ) : character).ToString();
	}

	void Submit()
	{
		var target = _target;
		if ( !target.IsValid() ) return;

		Close();
		target.CreateEvent( "onsubmit", target.Text );
	}

	/// <summary>
	/// Closes the keyboard and whatever it was typing into, as if nothing had been typed.
	/// </summary>
	public void Cancel()
	{
		var target = _target;
		if ( !target.IsValid() ) return;

		Close();
		target.CreateEvent( "oncancel" );
	}
}
