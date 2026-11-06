using System.Collections.Immutable;

/// <summary>
/// Something VR hands hold by its <see cref="GrabPoint"/>s: a gun, a magazine, a round, or a moving part of a gun like
/// its slide. Ported from gunbuilder.
/// <para>Over the network the hand that takes hold of it takes ownership of the object, then moves it and keeps its
/// state, and everyone else follows. Only VR hands hold these: the carry (use) never picks them up, so a desktop player
/// can't take or equip one.</para>
/// </summary>
public class BaseGunplayInteractable : Component, ICarryEvent
{
	[Property] public Rigidbody Rigidbody { get; set; }

	/// <summary>
	/// Since we last started or stopped interacting with this.
	/// </summary>
	protected TimeSince TimeSinceInteract { get; set; } = 1;

	/// <summary>
	/// The grab points of this interactable being held right now, on the holder's machine.
	/// </summary>
	protected HashSet<IGrabbable> heldGrabbables = new();

	public virtual IEnumerable<IGrabbable> GrabbableDirectory => GetComponentsInChildren<GrabPoint>( false, true );

	public virtual ImmutableHashSet<IGrabbable> HeldGrabPoints => ImmutableHashSet.CreateRange( heldGrabbables );

	/// <summary>
	/// Is a hand holding this, on this machine?
	/// </summary>
	public virtual bool IsHeld => heldGrabbables.Any( x => x.IsHeld );

	/// <summary>
	/// The grab point that isn't tagged secondary.
	/// </summary>
	public virtual IGrabbable PrimaryGrabPoint => GrabbableDirectory.FirstOrDefault( x => !x.Tags.Has( "secondary" ) );

	/// <summary>
	/// A delay between starting and stopping an interaction.
	/// </summary>
	protected const float InteractDelay = 0.4f;

	protected override void OnStart()
	{
		// Whoever's hand takes hold of it takes it over, and it stays in the game when they leave
		if ( Networking.IsHost && GameObject.Network.Active && GameObject.Network.RootGameObject == GameObject )
		{
			GameObject.Network.SetOwnerTransfer( OwnerTransfer.Takeover );
			GameObject.Network.SetOrphanedMode( NetworkOrphaned.Host );
		}
	}

	public void SetGrabPointsEnabled( bool enabled )
	{
		foreach ( var grabPoint in GrabbableDirectory )
			grabPoint.GameObject.Enabled = enabled;
	}

	protected virtual bool CanInteract( IGrabbable grabbable, GunplayHand hand )
	{
		if ( grabbable is null ) return false;
		if ( TimeSinceInteract < InteractDelay ) return false;
		if ( grabbable.IsHeld ) return false;
		if ( hand.IsHolding() ) return false;

		return grabbable.CanStartGrabbing( this, hand );
	}

	protected virtual bool CanStopInteract( IGrabbable grabbable, GunplayHand hand )
	{
		if ( TimeSinceInteract < InteractDelay ) return false;
		return grabbable.CanStopGrabbing( this, hand );
	}

	/// <summary>
	/// A hand takes hold of one of our grab points.
	/// </summary>
	public bool Interact( GunplayHand hand, IGrabbable grabbable = null )
	{
		grabbable ??= GrabbableDirectory.FirstOrDefault();

		if ( !CanInteract( grabbable, hand ) ) return false;

		TakeOwnership();

		if ( hand.IsValid() )
			hand.CurrentGrabbable = grabbable;

		heldGrabbables.Add( grabbable );
		OnInteract( grabbable, hand );

		return true;
	}

	/// <summary>
	/// A hand lets go of one of our grab points. <paramref name="force"/> skips the delay.
	/// </summary>
	public bool StopInteract( IGrabbable grabbable, bool force = false )
	{
		var hand = grabbable.Hand;

		if ( !force && !CanStopInteract( grabbable, hand ) ) return false;

		if ( hand.IsValid() )
			hand.CurrentGrabbable = null;

		heldGrabbables.Remove( grabbable );
		OnStopInteract( grabbable, hand );

		return true;
	}

	protected virtual void OnStopInteract( IGrabbable grabbable, GunplayHand hand )
	{
		TimeSinceInteract = 0;
	}

	protected virtual void OnInteract( IGrabbable grabbable, GunplayHand hand )
	{
		TimeSinceInteract = 0;
	}

	/// <summary>
	/// Makes this machine the owner of the object, so it can move it and change its state.
	/// </summary>
	public void TakeOwnership()
	{
		var root = GameObject.Network.RootGameObject;
		if ( root.IsValid() && root.Network.Active && root.IsProxy )
			root.Network.TakeOwnership();
	}

	public void FreezeMotion( bool frozen = true )
	{
		if ( Rigidbody.IsValid() )
			Rigidbody.MotionEnabled = !frozen;
	}

	// The hands hold these, never the carry: a desktop player can't pick one up, and a VR player's grip goes to the hand
	void ICarryEvent.OnCarryGrab( PlayerCarryEvent e ) => e.Cancelled = true;
}
