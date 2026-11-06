/// <summary>
/// A gun's chamber: the round about to be fired. Fed from the magazine when the slide goes forward, or by pushing a
/// loose round in while the slide's back. Ported from gunbuilder's WeaponChamber. The gun's owner keeps it; how many
/// rounds are in it is synced, so whoever takes the gun next carries on from there.
/// </summary>
public sealed class GunChamber : Component, Component.ITriggerListener
{
	/// <summary>
	/// How many rounds are chambered.
	/// </summary>
	[Sync, Property, ReadOnly] public int ChamberCount { get; private set; }

	/// <summary>
	/// How many rounds fit, more than one for something like a shotgun.
	/// </summary>
	[Property] public int ChamberCapacity { get; set; } = 1;

	/// <summary>
	/// The gun's bolt or slide, if it has one.
	/// </summary>
	[Property] public PointInteractable Bolt { get; set; }

	/// <summary>
	/// Played when a round's pushed in by hand.
	/// </summary>
	[Property] public SoundEvent OnChamberSound { get; set; }

	/// <summary>
	/// What the chambered rounds are. All rounds of a caliber are the same, so one will do.
	/// </summary>
	Round _round;

	Round ChamberedRound => _round ?? GetComponentInParent<PhysicalGun>()?.RoundTemplate;

	bool CanInsert => ChamberCount < ChamberCapacity;

	/// <summary>
	/// Takes the chambered round out. Owner only.
	/// </summary>
	public Round Eject()
	{
		if ( IsProxy || ChamberCount < 1 ) return null;

		ChamberCount--;
		return ChamberedRound is { } round ? round with { IsSpent = false } : null;
	}

	/// <summary>
	/// Fills the chamber from <paramref name="src"/>. Owner only.
	/// </summary>
	/// <returns>How many rounds went in.</returns>
	public int Feed( IAmmoSource src )
	{
		if ( IsProxy || src is null ) return 0;

		var count = 0;
		while ( CanInsert && src.Pop().FirstOrDefault() is { } round )
		{
			_round = round;
			ChamberCount++;
			count++;
		}

		return count;
	}

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( IsProxy ) return;

		if ( other.GameObject.Root.GetComponent<LooseRound>() is not { Consumed: false } round || round.IsProxy ) return;

		// Only with the slide all the way back
		if ( Bolt.IsValid() && !Bolt.CompletionValue.AlmostEqual( 1f ) ) return;
		if ( !CanInsert ) return;

		round.GetComponent<GunplayInteractable>()?.ClearAllInteractions();

		if ( Feed( round ) <= 0 ) return;

		if ( OnChamberSound is not null )
			GunplaySounds.Play( OnChamberSound, WorldPosition );

		round.Remove();
	}
}
