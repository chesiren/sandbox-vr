using Sandbox.UI;

namespace Sandbox;

public class InputHint : Panel
{
	Texture _lastGlyph;
	string _lastOrigin;

	[Parameter] public string Action { get; set; }
	[Parameter] public InputGlyphSize GlyphSize { get; set; } = InputGlyphSize.Medium;

	public override void Tick()
	{
		if ( string.IsNullOrEmpty( Action ) ) return;

		if ( VRMode.IsActive && VRInputNames.Button( Action ) is { } button )
		{
			ShowButton( button );
			return;
		}

		var glyph = Input.GetGlyph( Action, GlyphSize, false );

		if ( glyph.IsValid() )
		{
			if ( glyph == _lastGlyph ) return;
			_lastGlyph = glyph;
			_lastOrigin = null;

			Style.SetBackgroundImage( glyph );
			Style.AspectRatio = (float)glyph.Width / glyph.Height;
		}
		else
		{
			var origin = Input.GetButtonOrigin( Action ) ?? Action;
			if ( origin == _lastOrigin ) return;
			_lastOrigin = origin;
			_lastGlyph = null;

			Style.SetBackgroundImage( string.Empty );
		}
	}

	Label _buttonLabel;

	/// <summary>
	/// In VR, the button's name on a key cap. The glyphs are for what's bound to the action, which the controllers aren't.
	/// </summary>
	void ShowButton( string button )
	{
		if ( !_buttonLabel.IsValid() )
		{
			Style.SetBackgroundImage( string.Empty );
			Style.BackgroundColor = Color.White.WithAlpha( 0.92f );
			Style.BorderTopLeftRadius = Style.BorderTopRightRadius = Style.BorderBottomLeftRadius = Style.BorderBottomRightRadius = Length.Pixels( 2 );
			Style.PaddingLeft = Style.PaddingRight = Length.Pixels( 8 );
			Style.AlignItems = Align.Center;
			Style.JustifyContent = Justify.Center;
			Style.FontColor = new Color( 0.07f, 0.07f, 0.07f );

			_buttonLabel = AddChild<Label>();
		}

		_buttonLabel.Text = button;
	}
}
