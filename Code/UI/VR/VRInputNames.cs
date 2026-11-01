/// <summary>
/// What each game action is on the controllers, for telling the player which button to press. Mirrors
/// <see cref="VRPlayer.MapInput"/>: keep the two together when the mapping changes.
/// </summary>
public static class VRInputNames
{
	static readonly Dictionary<string, (bool OffHand, string Button)> Actions = new( StringComparer.OrdinalIgnoreCase )
	{
		["attack1"] = (false, "trigger"),
		["attack2"] = (false, "b"),
		["reload"] = (false, "a"),
		["use"] = (false, "grip"),
		["jump"] = (false, "stick_up"),
		["run"] = (true, "stick_click"),
		["spawnmenu"] = (true, "a"),
		["inspectmenu"] = (true, "b"),
		["undo"] = (true, "trigger"),
	};

	/// <summary>
	/// Sitting in something, see <see cref="VRPlayer.DriveInput"/>.
	/// </summary>
	static readonly Dictionary<string, (bool OffHand, string Button)> SeatedActions = new( StringComparer.OrdinalIgnoreCase )
	{
		["attack1"] = (false, "b"),
		["attack2"] = (false, "a"),
		["forward"] = (false, "trigger"),
		["backward"] = (true, "trigger"),
		["left"] = (true, "stick"),
		["right"] = (true, "stick"),
	};

	static bool Find( string action, out (bool OffHand, string Button) input )
	{
		var seated = VRPlayer.Local?.Player.IsSeated ?? false;
		return (seated && !SpawnMenuHost.IsOpen && SeatedActions.TryGetValue( action, out input )) || Actions.TryGetValue( action, out input );
	}

	/// <summary>
	/// The button and the hand it's on, like "A (left hand)". Null if the action has no button in VR.
	/// </summary>
	public static string Label( string action )
	{
		if ( !Find( action, out var input ) ) return null;

		var left = input.OffHand != VRMode.LeftHanded;
		var hand = Game.Language.GetPhrase( left ? "vr.hand.left" : "vr.hand.right" );

		return $"{ButtonName( input.Button )} ({hand})";
	}

	/// <summary>
	/// Just the button, like "Grip", where there's no room for the hand. Null if the action has no button in VR.
	/// </summary>
	public static string Button( string action ) => Find( action, out var input ) ? ButtonName( input.Button ) : null;

	static string ButtonName( string button ) => Game.Language.GetPhrase( $"vr.button.{button}" );
}
