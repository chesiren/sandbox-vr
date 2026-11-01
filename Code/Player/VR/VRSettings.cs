/// <summary>
/// The player's VR comfort options, saved between sessions. Mirrors Half-Life: Alyx's locomotion choices,
/// plus the arm swinger from the old VR controller demo.
/// </summary>
public sealed class VRSettings
{
	public enum MovementType
	{
		/// <summary>Walk with the stick.</summary>
		Continuous,
		/// <summary>Aim an arc and blink there.</summary>
		Teleport,
		/// <summary>Aim an arc and dash there quickly.</summary>
		Shift,
		/// <summary>Hold A and swing your arms to walk.</summary>
		ArmSwinger
	}

	public enum DirectionType
	{
		Head,
		Hand
	}

	public enum TurnType
	{
		Snap,
		Smooth
	}

	public MovementType Movement { get; set; } = MovementType.Continuous;
	public DirectionType Direction { get; set; } = DirectionType.Head;
	public TurnType Turn { get; set; } = TurnType.Snap;
	public float SnapAngle { get; set; } = 45f;
	public float SmoothTurnSpeed { get; set; } = 120f;

	/// <summary>
	/// Hold weapons and tools in the left hand, move with the right.
	/// </summary>
	public bool LeftHanded { get; set; }

	/// <summary>
	/// Crouch automatically when the real head drops below this fraction of the standing eye height.
	/// </summary>
	public float PhysicalCrouchFraction { get; set; } = 0.7f;

	/// <summary>
	/// Wear the watch on the hand that holds weapons rather than the other one.
	/// </summary>
	public bool WatchOnDominantHand { get; set; }

	/// <summary>
	/// Multiplies the size of the watch.
	/// </summary>
	public float WatchSize { get; set; } = 1f;

	/// <summary>
	/// How opaque the VR HUD's panels are.
	/// </summary>
	public float HudOpacity { get; set; } = 1f;

	const string FilePath = "vr/settings.json";

	static VRSettings _current;

	/// <summary>
	/// The loaded settings, read from disk the first time they're needed.
	/// </summary>
	public static VRSettings Current => _current ??= Load();

	static VRSettings Load()
	{
		try
		{
			if ( FileSystem.Data.FileExists( FilePath ) )
				return FileSystem.Data.ReadJson<VRSettings>( FilePath ) ?? new VRSettings();
		}
		catch ( Exception e )
		{
			Log.Warning( $"Couldn't read VR settings: {e.Message}" );
		}

		return new VRSettings();
	}

	public void Save()
	{
		FileSystem.Data.CreateDirectory( "vr" );
		FileSystem.Data.WriteJson( FilePath, this );
	}
}
