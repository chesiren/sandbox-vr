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
	/// The button and the hand it's on, like "A (left hand)". Null if the action has no button in VR.
	/// </summary>
	public static string Label( string action )
	{
		if ( !Actions.TryGetValue( action, out var input ) ) return null;

		var left = input.OffHand != VRMode.LeftHanded;
		var button = Game.Language.GetPhrase( $"vr.button.{input.Button}" );
		var hand = Game.Language.GetPhrase( left ? "vr.hand.left" : "vr.hand.right" );

		return $"{button} ({hand})";
	}
}
