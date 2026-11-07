/// <summary>
/// A grenade held in the hand. Pull its pin with the other hand (<see cref="GrenadePin"/>); while it's held the spoon
/// stays down and nothing happens, so it can be cooked in the hand. Let go of it, thrown or dropped, and the spoon flies
/// off: it goes off <see cref="Fuse"/> seconds later, on the host, as the player who threw it.
/// </summary>
public sealed class PhysicalGrenade : GunplayInteractable
{
	[Property] public SkinnedModelRenderer Renderer { get; set; }

	/// <summary>
	/// Seconds from the spoon flying off to the bang.
	/// </summary>
	[Property, Group( "Explosion" )] public float Fuse { get; set; } = 3f;

	[Property, Group( "Explosion" )] public float Radius { get; set; } = 512f;

	[Property, Group( "Explosion" )] public float Damage { get; set; } = 125f;

	[Property, Group( "Explosion" )] public float Force { get; set; } = 1f;

	[Property, Group( "Sounds" )] public SoundEvent PinSound { get; set; }

	[Property, Group( "Sounds" )] public SoundEvent SpoonSound { get; set; }

	/// <summary>
	/// The pin's out.
	/// </summary>
	[Sync] public bool PinPulled { get; private set; }

	/// <summary>
	/// The spoon's off: it's going to go off.
	/// </summary>
	[Sync] public bool Armed { get; private set; }

	/// <summary>
	/// Whoever held it last, who it goes off as.
	/// </summary>
	[Sync] public GameObject Thrower { get; private set; }

	protected override void OnInteract( IGrabbable grabbable, GunplayHand hand )
	{
		base.OnInteract( grabbable, hand );

		if ( hand?.Owner?.Player is { } player )
			Thrower = player.GameObject;
	}

	protected override void OnStopInteract( IGrabbable grabbable, GunplayHand hand )
	{
		base.OnStopInteract( grabbable, hand );

		// Out of the hand with the pin out: the spoon flies off
		if ( !IsProxy && PinPulled && !Armed && !IsHeld )
		{
			Armed = true;
			GunplaySounds.Play( SpoonSound, WorldPosition );
			Arm( Thrower, Fuse );
		}
	}

	/// <summary>
	/// Pulls the pin. Owner only.
	/// </summary>
	public void PullPin()
	{
		if ( IsProxy || PinPulled ) return;

		PinPulled = true;
		GunplaySounds.Play( PinSound, WorldPosition );
		VRMode.Pulse( PrimaryGrabPoint?.Hand?.Controller, 0.4f, 0.2f );
	}

	/// <summary>
	/// The host lights the fuse: the grenade's own explosive, as the thrower.
	/// </summary>
	[Rpc.Host]
	void Arm( GameObject thrower, float fuse )
	{
		if ( !Rpc.Caller.IsHost && VRPlayer.Get( Player.FindForConnection( Rpc.Caller ) ) is null ) return;
		if ( GetComponent<TimedExplosive>().IsValid() ) return;

		var explosive = AddComponent<TimedExplosive>( false );
		explosive.Lifetime = fuse;
		explosive.Radius = Radius;
		explosive.Damage = Damage;
		explosive.Force = Force;
		explosive.Attacker = thrower;
		explosive.Enabled = true;
	}

	int _shown = -1;

	protected override void OnUpdate()
	{
		if ( !Renderer.IsValid() ) return;

		// The pin and the spoon go as they come off
		var state = (PinPulled ? 1 : 0) | (Armed ? 2 : 0);
		if ( state == _shown ) return;

		Renderer.SetBodyGroup( "pin", PinPulled ? 1 : 0 );
		Renderer.SetBodyGroup( "spoon", Armed ? 1 : 0 );
		_shown = state;
	}
}

/// <summary>
/// A grenade's pin: take it with the trigger of the other hand and pull it away to pull it out.
/// </summary>
public sealed class GrenadePin : BaseGunplayInteractable
{
	[Property] public PhysicalGrenade Grenade { get; set; }

	/// <summary>
	/// How far the hand has to pull it.
	/// </summary>
	[Property] public float PullDistance { get; set; } = 3f;

	Vector3 _start;

	protected override bool CanInteract( IGrabbable grabbable, GunplayHand hand )
	{
		if ( !Grenade.IsValid() || Grenade.PinPulled ) return false;
		return base.CanInteract( grabbable, hand );
	}

	protected override void OnInteract( IGrabbable grabbable, GunplayHand hand )
	{
		base.OnInteract( grabbable, hand );
		_start = Grenade.WorldTransform.PointToLocal( hand.WorldPosition );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || !Grenade.IsValid() || PrimaryGrabPoint?.Hand is not { } hand ) return;

		var pulled = Grenade.WorldTransform.PointToLocal( hand.WorldPosition ).Distance( _start );
		if ( pulled < PullDistance ) return;

		Grenade.PullPin();
		foreach ( var point in HeldGrabPoints.ToList() )
		{
			if ( point is GrabPoint grabPoint ) grabPoint.StopGrabbing( hand, true );
			else StopInteract( point, true );
		}
	}
}
