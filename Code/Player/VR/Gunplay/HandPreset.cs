/// <summary>
/// How curled each finger is while holding a <see cref="GrabPoint"/>, from 0 open to 1 closed. -1 leaves that finger to
/// the controller, like the index finger on a trigger. Ported from gunbuilder.
/// </summary>
public sealed class HandPreset
{
	public float Thumb { get; set; }
	public float Index { get; set; }
	public float Middle { get; set; }
	public float Ring { get; set; }
	public float Pinky { get; set; }

	public HandPreset() { }

	public HandPreset( float thumb, float index, float middle, float ring, float pinky )
	{
		Thumb = thumb;
		Index = index;
		Middle = middle;
		Ring = ring;
		Pinky = pinky;
	}

	public static HandPreset Grip => new( 1f, 1f, 1f, 1f, 1f );
	public static HandPreset GripWithoutIndexFinger => new( -1f, 1f, 1f, 1f, 1f );

	/// <summary>
	/// The curl of a finger, thumb first, or <paramref name="controller"/>'s when the preset leaves it free.
	/// </summary>
	public float Curl( int finger, float controller )
	{
		var value = finger switch
		{
			0 => Thumb,
			1 => Index,
			2 => Middle,
			3 => Ring,
			_ => Pinky
		};

		return value < 0f ? controller : value;
	}

	public override string ToString() => $"{Thumb}, {Index}, {Middle}, {Ring}, {Pinky}";
}
