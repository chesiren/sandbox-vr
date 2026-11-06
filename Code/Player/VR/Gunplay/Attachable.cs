/// <summary>
/// Something that goes into an <see cref="AttachmentPoint"/>, like a magazine into a gun. Ported from gunbuilder.
/// </summary>
public sealed class Attachable : Component
{
	[Property] public GunplayInteractable Interactable { get; set; }

	[Property] public Rigidbody Rigidbody { get; set; }

	[Property] public Collider Collider { get; set; }

	[Property] public SoundEvent AttachSound { get; set; }

	[Property] public SoundEvent DetachSound { get; set; }

	/// <summary>
	/// What this is attached to, on every machine.
	/// </summary>
	public AttachmentPoint CurrentAttachmentPoint { get; private set; }

	public bool IsAttached => CurrentAttachmentPoint.IsValid();

	internal void OnAttach( AttachmentPoint attachmentPoint, bool playSound = true )
	{
		CurrentAttachmentPoint = attachmentPoint;

		// The hand lets go of it: it's in the gun now
		Interactable?.ClearAllInteractions();

		if ( Rigidbody.IsValid() )
			Rigidbody.MotionEnabled = false;

		Tags.Set( "attached", true );

		if ( playSound && AttachSound is not null )
			Sound.Play( AttachSound, WorldPosition );
	}

	/// <summary>
	/// Takes this out of what it's attached to.
	/// </summary>
	internal void Detach()
	{
		CurrentAttachmentPoint?.TryDetach();
	}

	internal void OnDetach( AttachmentPoint attachmentPoint, bool playSound = true )
	{
		Tags.Set( "attached", false );

		// Back to physics, unless a hand's holding it (that moves it itself)
		if ( Rigidbody.IsValid() && !(Interactable?.IsHeld ?? false) && !IsProxy )
		{
			Rigidbody.MotionEnabled = true;

			// Carry on with what it was attached to, rather than stopping dead in the air
			var parentBody = attachmentPoint?.Interactable?.Rigidbody;
			Rigidbody.Velocity = parentBody.IsValid() ? parentBody.Velocity : Vector3.Zero;
			Rigidbody.AngularVelocity = parentBody.IsValid() ? parentBody.AngularVelocity : Vector3.Zero;
		}

		if ( playSound && DetachSound is not null )
			Sound.Play( DetachSound, WorldPosition );

		CurrentAttachmentPoint = null;
	}
}
