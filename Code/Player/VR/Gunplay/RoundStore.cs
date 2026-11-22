/// <summary>
/// Rounds of one caliber held together: a magazine that goes in a gun, one built into it like a shotgun's tube, or a
/// speedloader or speed strip (see <see cref="AmmoClip"/>). Every round in it is the same, so only how many there are
/// is kept, synced by the owner.
/// </summary>
public abstract class RoundStore : Component, IAmmoSource, Component.ITriggerListener
{
	/// <summary>
	/// How many rounds are in it.
	/// </summary>
	[Sync, Property, ReadOnly] public int BulletCount { get; protected set; }

	/// <summary>
	/// How many rounds fit in it.
	/// </summary>
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
	/// Fills it to <see cref="BulletCapacity"/>. Owner only.
	/// </summary>
	public void Refill()
	{
		if ( !IsProxy ) BulletCount = BulletCapacity;
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

	/// <summary>
	/// Takes every round of an <see cref="AmmoClip"/> pushed into it, like a shotgun's tube from a speed strip.
	/// </summary>
	protected virtual bool AcceptsClips => false;

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( CanLoadByHand )
			LoadByHand( other );
	}

	/// <summary>
	/// Loads what a hand pushed into it: a loose round, or the rounds of a clip if it takes them. Owner only.
	/// </summary>
	/// <returns>How many rounds went in.</returns>
	public int LoadByHand( Collider other )
	{
		if ( IsProxy || IsFull || !other.IsValid() ) return 0;

		var root = other.GameObject.Root;
		var pushed = 0;

		// Ours or another player's, like the owner of a gun takes a magazine handed into it
		if ( root.GetComponent<LooseRound>() is { Consumed: false } round )
		{
			if ( !IsCompatibleBullet( round.Bullet ) ) return 0;

			pushed = Push( round.Pop().ToArray() );
			if ( pushed <= 0 ) return 0;

			round.GetComponent<GunplayInteractable>()?.ClearAllInteractions();
			round.Remove();
		}
		// The clip's rounds can only be taken by whoever holds it, it stays in their hand
		else if ( AcceptsClips && root.GetComponent<AmmoClip>() is { } clip && clip != this && !clip.IsProxy )
		{
			if ( !clip.HasAmmo || clip.Caliber != Caliber ) return 0;

			pushed = Push( clip.Pop( BulletCapacity - BulletCount ).ToArray() );
			if ( pushed <= 0 ) return 0;
		}
		else
		{
			return 0;
		}

		if ( OnChamberSound is not null )
			GunplaySounds.Play( OnChamberSound, WorldPosition );

		OnLoadedByHand();
		return pushed;
	}

	/// <summary>
	/// A loose round was pushed in by hand.
	/// </summary>
	protected virtual void OnLoadedByHand() { }
}
