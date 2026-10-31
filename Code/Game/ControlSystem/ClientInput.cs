using System.Text.Json.Serialization;

/// <summary>
/// A configurable input action for contraptions driven from a seat. This contains no state:
/// it reads the active driver's connection while <see cref="IPlayerControllable.OnControl"/>
/// is running. Wired inputs are ordinary <see cref="SignalInputAttribute"/> handlers instead.
/// </summary>
public struct ClientInput
{
	[Property]
	public string Action { get; set; }

	[JsonIgnore, Hide]
	public readonly bool IsEnabled => !string.IsNullOrWhiteSpace( Action );

	/// <summary>
	/// A gamepad's or VR controller's analog input counts too. It's pressed from code in the driver's update, which doesn't
	/// reach the fixed update we're read in on the host, so it comes through <see cref="Player.ActionAmounts"/> instead.
	/// </summary>
	public readonly bool Down() => IsEnabled && (ControlContext.Connection?.Down( Action ) == true || Amount > 0f);
	public readonly bool Pressed() => IsEnabled && ControlContext.Connection?.Pressed( Action ) == true;
	public readonly bool Released() => IsEnabled && ControlContext.Connection?.Released( Action ) == true;

	private readonly float Amount => ControlContext.Player?.ActionAmounts is { } amounts && amounts.TryGetValue( Action, out var amount ) ? amount : 0f;

	/// <summary>
	/// How far the action's pushed, 0 to 1: a key's all the way, a gamepad's trigger or stick anything between (see
	/// <see cref="Player.ActionAmounts"/>).
	/// </summary>
	public readonly float GetAnalog()
	{
		if ( !Down() ) return 0f;

		var amounts = ControlContext.Player?.ActionAmounts;
		return amounts is not null && amounts.TryGetValue( Action, out var amount ) ? amount : 1f;
	}
}
