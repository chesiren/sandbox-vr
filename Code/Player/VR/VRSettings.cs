/// <summary>
/// The player's VR comfort options, saved between sessions. Mirrors Half-Life: Alyx's locomotion choices,
/// plus the arm swinger from the old VR controller demo.
/// </summary>
public sealed class VRSettings
{
	/// <summary>
	/// How the movement hand's stick gets the player around.
	/// </summary>
	public enum MovementType
	{
		/// <summary>
		/// Walks with the stick.
		/// </summary>
		Continuous,

		/// <summary>
		/// Aims an arc and blinks to where it lands.
		/// </summary>
		Teleport,

		/// <summary>
		/// Aims an arc and dashes there quickly.
		/// </summary>
		Shift,

		/// <summary>
		/// Walks while A is held and the arms swing.
		/// </summary>
		ArmSwinger
	}

	/// <summary>
	/// What forward means for continuous movement.
	/// </summary>
	public enum DirectionType
	{
		Head,
		Hand
	}

	/// <summary>
	/// How the turning stick turns the player.
	/// </summary>
	public enum TurnType
	{
		Snap,
		Smooth
	}

	public MovementType Movement { get; set; } = MovementType.Continuous;
	public DirectionType Direction { get; set; } = DirectionType.Head;
	public TurnType Turn { get; set; } = TurnType.Snap;

	/// <summary>
	/// How far one snap turn goes, in degrees.
	/// </summary>
	public float SnapAngle { get; set; } = 45f;

	/// <summary>
	/// How fast smooth turning goes, in degrees a second.
	/// </summary>
	public float SmoothTurnSpeed { get; set; } = 120f;

	/// <summary>
	/// Weapons and tools go in the left hand, and movement on the right.
	/// </summary>
	public bool LeftHanded { get; set; }

	/// <summary>
	/// Crouches automatically when the real head drops below this fraction of the standing eye height.
	/// </summary>
	public float PhysicalCrouchFraction { get; set; } = 0.7f;

	/// <summary>
	/// Seated, the view tips with the seat as it pitches and rolls, rather than only turning with it and keeping the horizon level.
	/// </summary>
	public bool TiltWithSeat { get; set; } = true;

	/// <summary>
	/// Puts the watch on the hand that holds weapons rather than the other one.
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

	/// <summary>
	/// Writes these settings to disk, so they're the ones loaded next session.
	/// </summary>
	public void Save()
	{
		FileSystem.Data.CreateDirectory( "vr" );
		FileSystem.Data.WriteJson( FilePath, this );
	}
}
