/// <summary>
/// A whole object VR hands hold and move about: a gun, a magazine, a round. The holder's machine pulls its body to the
/// hand every physics step; a second hand on a grab point tagged secondary aims it along both hands. Ported from
/// gunbuilder.
/// </summary>
public class GunplayInteractable : BaseGunplayInteractable
{
	/// <summary>
	/// The second hand's grab point, while both hands hold this.
	/// </summary>
	public IGrabbable SecondaryGrabPoint => heldGrabbables.FirstOrDefault( x => x.Tags.Has( "secondary" ) );

	/// <summary>
	/// Higher is heavier: it takes longer to follow the hand.
	/// </summary>
	[Property] public float MassScale { get; set; } = 1.0f;

	/// <summary>
	/// Can <paramref name="attachable"/> go into one of our attachment points?
	/// </summary>
	public virtual bool CanAccept( Attachable attachable ) => true;

	protected override bool CanInteract( IGrabbable grabbable, GunplayHand hand )
	{
		// What's in a gun comes out with its button, not by pulling at it
		if ( GetComponent<Attachable>() is { IsAttached: true } ) return false;

		return base.CanInteract( grabbable, hand );
	}

	public void Attach( Attachable attachable, AttachmentPoint attachmentPoint )
	{
		attachable.OnAttach( attachmentPoint );
		OnAttachableAdded( attachable, attachmentPoint );
	}

	public void Detach( Attachable attachable, AttachmentPoint attachmentPoint )
	{
		attachable.OnDetach( attachmentPoint );
		OnAttachableRemoved( attachable, attachmentPoint );
	}

	protected virtual void OnAttachableRemoved( Attachable attachable, AttachmentPoint attachmentPoint ) { }

	protected virtual void OnAttachableAdded( Attachable attachable, AttachmentPoint attachmentPoint ) { }

	protected override void OnInteract( IGrabbable grabbable, GunplayHand hand )
	{
		base.OnInteract( grabbable, hand );

		// What's attached to us is moved by us now
		foreach ( var point in GetComponentsInChildren<AttachmentPoint>() )
			point.CurrentAttachable?.Interactable?.TakeOwnership();

		// Keyframed while held: the hand moves it by its velocity
		FreezeMotion();
	}

	protected override void OnStopInteract( IGrabbable grabbable, GunplayHand hand )
	{
		base.OnStopInteract( grabbable, hand );

		if ( heldGrabbables.Count > 0 || !Rigidbody.IsValid() ) return;

		// Something attached to a gun stays put; otherwise it falls, with the hand's swing as a throw
		if ( GetComponent<Attachable>() is { IsAttached: true } ) return;

		FreezeMotion( false );

		if ( hand.IsValid() )
			Rigidbody.Velocity = hand.ThrowVelocity;
	}

	/// <summary>
	/// The rotation to hold this at.
	/// </summary>
	protected virtual Rotation GetHoldRotation()
	{
		var primary = PrimaryGrabPoint;
		var targetRotation = primary.Hand.HoldRotation;

		// Held by both hands: swing it, from how the first hand holds it, so the line between its grab points runs between
		// the hands. The roll stays the first hand's: the second one only supports, palm under the barrel or round a
		// front grip alike. Taking its roll from the second hand's turn (gunbuilder's way) rolled the gun over unless that
		// hand held it like a vertical grip.
		if ( SecondaryGrabPoint is { } secondary && secondary.Hand.IsValid() )
		{
			var between = WorldRotation.Inverse * (secondary.GameObject.WorldPosition - primary.GameObject.WorldPosition);
			var from = (targetRotation * between).Normal;
			var to = (secondary.Hand.WorldPosition - primary.Hand.WorldPosition).Normal;

			if ( !from.IsNearZeroLength && !to.IsNearZeroLength )
				targetRotation = Rotation.FromToRotation( from, to ) * targetRotation;
		}

		return targetRotation;
	}

	protected void PositionInteractable()
	{
		var primary = PrimaryGrabPoint;
		if ( primary?.Hand is not { } hand || !Rigidbody.IsValid() ) return;

		// Bring the grab point, not the middle of the body, onto the hand
		var holdPos = hand.HoldPosition;
		var target = holdPos - (primary.GameObject.WorldPosition - Rigidbody.WorldPosition);

		var velocity = Rigidbody.Velocity;
		Vector3.SmoothDamp( Rigidbody.WorldPosition, target, ref velocity, 0.2f * MassScale, Time.Delta );
		Rigidbody.Velocity = velocity;

		var angularVelocity = Rigidbody.AngularVelocity;
		Rotation.SmoothDamp( Rigidbody.WorldRotation, GetHoldRotation(), ref angularVelocity, 0.15f * MassScale, Time.Delta );
		Rigidbody.AngularVelocity = angularVelocity;
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;

		// The primary grab point is what holds it; a hand on the secondary alone doesn't
		if ( IsHeld && PrimaryGrabPoint is { IsHeld: true } )
			PositionInteractable();
	}

	/// <summary>
	/// Makes every hand holding this let go.
	/// </summary>
	public void ClearAllInteractions()
	{
		foreach ( var point in heldGrabbables.ToList() )
		{
			if ( point is GrabPoint grabPoint ) grabPoint.StopGrabbing( point.Hand, true );
			else StopInteract( point, true );
		}
	}
}
