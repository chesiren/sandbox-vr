/// <summary>
/// Keeps entities marked <see cref="ScriptedEntity.VROnly"/> for VR players: the host turns down anyone else spawning
/// one, from the menu or the console, and the spawn menu greys them out for desktop players.
/// </summary>
public sealed class VROnlySpawnGate : GameObjectSystem<VROnlySpawnGate>, Global.ISpawnEvents
{
	public VROnlySpawnGate( Scene scene ) : base( scene ) { }

	void Global.ISpawnEvents.OnSpawn( Global.ISpawnEvents.SpawnData e )
	{
		if ( e.Player is null || e.Spawner is not EntitySpawner { Entity.VROnly: true } ) return;

		if ( VRPlayer.Get( Player.FindForConnection( e.Player ) ) is null )
			e.Cancelled = true;
	}

	/// <summary>
	/// Is the thing behind a spawn menu ident only for VR players?
	/// </summary>
	public static bool IsVROnly( string ident )
	{
		if ( string.IsNullOrEmpty( ident ) ) return false;

		var (type, path, _) = SpawnlistItem.ParseIdent( ident );
		if ( type != "entity" || string.IsNullOrEmpty( path ) ) return false;

		return ResourceLibrary.Get<ScriptedEntity>( path )?.VROnly ?? false;
	}

	/// <summary>
	/// Can we spawn this from our spawn menu?
	/// </summary>
	public static bool CanSpawnLocally( string ident ) => VRMode.IsActive || !IsVROnly( ident );
}
