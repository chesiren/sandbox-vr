/// <summary>
/// Sounds from what the holder of a gun does on their machine (racking the slide, loading a round), played for everyone.
/// </summary>
public static class GunplaySounds
{
	public static void Play( SoundEvent sound, Vector3 position )
	{
		if ( sound is null ) return;

		Broadcast( sound.ResourcePath, position );
	}

	[Rpc.Broadcast( NetFlags.Unreliable )]
	static void Broadcast( string sound, Vector3 position )
	{
		Sound.Play( sound, position );
	}
}
