/// <summary>
/// A gun's magazine. Holds rounds of one caliber; push loose rounds into it to load it. Ported from gunbuilder's
/// WeaponMagazine. Every round in it is the same, so only how many there are is kept, synced by the magazine's owner.
/// </summary>
public sealed class GunMagazine : Component, IAmmoSource, Component.ITriggerListener
{
	[RequireComponent] public GunplayInteractable Interactable { get; set; }

	[RequireComponent] public Attachable Attachable { get; set; }

	[Property] public SkinnedModelRenderer Renderer { get; set; }

	/// <summary>
	/// How many rounds are in it.
	/// </summary>
	[Sync, Property, ReadOnly] public int BulletCount { get; private set; }

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

	[Property] public int AmmoBodygroup { get; set; } = 0;

	/// <summary>
	/// Played when a round is pushed in by hand.
	/// </summary>
	[Property] public SoundEvent OnChamberSound { get; set; }

	/// <summary>
	/// What a round from this magazine is.
	/// </summary>
	[Property, Group( "Bullet Setup" )] public Round BulletPrefab { get; set; }

	public bool HasAmmo => BulletCount > 0 || IsInfiniteAmmo;

	protected override void OnStart()
	{
		if ( !IsProxy )
			BulletCount = Math.Clamp( DefaultBulletCapacity, 0, BulletCapacity );
	}

	int _shownBodygroup = -1;

	protected override void OnUpdate()
	{
		// The top round shows while there's one
		var bodygroup = BulletCount > 0 ? 0 : 1;
		if ( bodygroup == _shownBodygroup || !Renderer.IsValid() ) return;

		Renderer.SetBodyGroup( AmmoBodygroup, bodygroup );
		_shownBodygroup = bodygroup;
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
			if ( BulletCount >= BulletCapacity ) break;
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

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( IsProxy ) return;

		// A round pushed in by a hand: whoever holds it owns it, and has to own the magazine too
		if ( other.GameObject.Root.GetComponent<LooseRound>() is not { Consumed: false } round || round.IsProxy ) return;
		if ( BulletCount >= BulletCapacity || !IsCompatibleBullet( round.Bullet ) ) return;

		if ( Push( round.Pop().ToArray() ) <= 0 ) return;

		round.GetComponent<GunplayInteractable>()?.ClearAllInteractions();

		if ( OnChamberSound is not null )
			GunplaySounds.Play( OnChamberSound, WorldPosition );

		round.Remove();
	}
}
