/// <summary>
/// Records how long the local player plays, once a minute.
/// </summary>
internal sealed class PlaytimeStats : GameObjectSystem<PlaytimeStats>
{
	RealTimeSince timeSinceMinute = 0;

	/// <summary>
	/// Minutes on this server. Scene-scoped, so joining another server starts the count over.
	/// </summary>
	int sessionMinutes;

	public PlaytimeStats( Scene scene ) : base( scene )
	{
		Listen( Stage.StartUpdate, 0, Tick, "UpdatePlaytime" );
	}

	void Tick()
	{
		if ( !Game.IsPlaying ) return;

		// A dedicated server has no local player to credit
		if ( Application.IsDedicatedServer ) return;

		if ( timeSinceMinute < 60f ) return;
		timeSinceMinute = 0;

		sessionMinutes++;

		Sandbox.Services.Stats.Increment( "playtime.minutes", 1 );

		// Aggregated as Highest, so the backend keeps the longest session we ever reported
		Sandbox.Services.Stats.SetValue( "session.minutes.max", sessionMinutes );
	}
}
