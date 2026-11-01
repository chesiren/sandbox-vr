using Sandbox.UI;
using Sandbox.UI.Construct;

namespace Sandbox;

/// <summary>
/// What a tooltip would say about the thing the laser's on, in one place at the bottom of the menus' panel. A tooltip
/// would shake with the laser, and showing it in the list would move the rows under it.
/// </summary>
public class VRHintBar : Panel
{
	readonly Label _name;
	readonly Label _text;
	Panel _shownFor;

	public VRHintBar()
	{
		_name = Add.Label( "", "name" );
		_name.Tokenize = false;

		_text = Add.Label( "", "text" );
		_text.IsRich = true;
		_text.Tokenize = false;
	}

	/// <summary>
	/// Shows what there is to say about this panel or the nearest thing it's in.
	/// </summary>
	public void ShowFor( Panel hovered )
	{
		if ( hovered == _shownFor ) return;
		_shownFor = hovered;

		var (name, text) = FindHint( hovered );

		_name.Text = LocalizedText.Resolve( name ) ?? "";
		_name.SetClass( "hidden", string.IsNullOrWhiteSpace( name ) );
		_text.Text = LocalizedText.Resolve( text ) ?? "";

		SetClass( "visible", !string.IsNullOrWhiteSpace( text ) );
	}

	static (string Name, string Text) FindHint( Panel hovered )
	{
		if ( !hovered.IsValid() ) return default;

		foreach ( var panel in hovered.AncestorsAndSelf )
		{
			if ( panel is UISheetRow row )
				return row.Hint;

			if ( panel is UIHintLabel label && !string.IsNullOrWhiteSpace( label.Hint ) )
				return (label.Text, label.Hint);

			if ( !string.IsNullOrWhiteSpace( panel.Tooltip ) )
				return (null, panel.Tooltip);
		}

		return default;
	}
}
