/// <summary>
/// Rounds of one caliber held together: a magazine that goes in a gun, or one built into it like a shotgun's tube. Every
/// round in it is the same, so only how many there are is kept, synced by the owner.
/// </summary>
public abstract class RoundStore : Component, IAmmoSource, Component.ITriggerListener
{
	/// <summary>
	/// How many rounds are in it.
	/// </summary>
	[Sync, Property, ReadOnly] public int BulletCount { get; protected set; }

	[Property] public int BulletCapacity { get; set; } = 30;

	/// <summary>
	/// How many rounds it comes with.
	/// </summary>
	[Property] public int DefaultBulletCapacity { get; set; } = 0;

	[Property] public RoundCaliber Caliber { get; set; }

	/// <summary>
	/// Never runs out, for testing.
	/// </summary>
	[Property] public bool IsInfiniteAmmo { get; set; } = false;

	/// <summary>
	/// Played when a round is pushed in by hand.
	/// </summary>
	[Property] public SoundEvent OnChamberSound { get; set; }

	/// <summary>
	/// What a round from here is.
	/// </summary>
	[Property, Group( "Bullet Setup" )] public Round BulletPrefab { get; set; }

	public bool HasAmmo => BulletCount > 0 || IsInfiniteAmmo;

	public bool IsFull => BulletCount >= BulletCapacity;

	protected override void OnStart()
	{
		if ( !IsProxy )
			BulletCount = Math.Clamp( DefaultBulletCapacity, 0, BulletCapacity );
	}

	/// <summary>
	/// Puts rounds in, as many as fit and match. Owner only.
	/// </summary>
	public int Push( params Round[] bullets )
	{
		if ( IsProxy ) return 0;

		var pushed = 0;
		foreach ( var bullet in bullets )
		{
			if ( IsFull ) break;
			if ( bullet is null || bullet.IsSpent || bullet.Caliber != Caliber ) continue;

			BulletCount++;
			pushed++;
		}

		return pushed;
	}

	/// <summary>
	/// Takes rounds out from the top. Owner only.
	/// </summary>
	public IEnumerable<Round> Pop( int amount = 1 )
	{
		var popped = new List<Round>();
		if ( IsProxy || BulletPrefab is null ) return popped;

		for ( var i = 0; i < amount; i++ )
		{
			if ( !HasAmmo ) break;

			if ( !IsInfiniteAmmo ) BulletCount--;
			popped.Add( BulletPrefab with { IsSpent = false } );
		}

		return popped;
	}

	public bool IsCompatibleBullet( Round bullet ) => Caliber == bullet.Caliber;

	/// <summary>
	/// Can a round be pushed in by hand right now?
	/// </summary>
	protected virtual bool CanLoadByHand => true;

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( IsProxy || !CanLoadByHand ) return;

		// A round pushed in by a hand: whoever holds it owns it, and has to own this too
		if ( other.GameObject.Root.GetComponent<LooseRound>() is not { Consumed: false } round || round.IsProxy ) return;
		if ( IsFull || !IsCompatibleBullet( round.Bullet ) ) return;

		if ( Push( round.Pop().ToArray() ) <= 0 ) return;

		round.GetComponent<GunplayInteractable>()?.ClearAllInteractions();

		if ( OnChamberSound is not null )
			GunplaySounds.Play( OnChamberSound, WorldPosition );

		OnLoadedByHand();
		round.Remove();
	}

	protected virtual void OnLoadedByHand() { }
}
