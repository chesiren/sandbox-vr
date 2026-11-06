/// <summary>
/// Something a VR hand can take hold of: a handle on a gun, a magazine, a round. A <see cref="GunplayHand"/> close to it
/// (or pointing at it) with the right input down grabs it. Ported from gunbuilder.
/// </summary>
public interface IGrabbable : IValid
{
	/// <summary>
	/// A hand let go of this grabbable.
	/// </summary>
	public bool StopGrabbing( GunplayHand hand );

	/// <summary>
	/// A hand took hold of this grabbable.
	/// </summary>
	public bool StartGrabbing( GunplayHand hand );

	/// <summary>
	/// Can the hand take hold of this?
	/// </summary>
	public bool CanStartGrabbing( BaseGunplayInteractable interactable, GunplayHand hand ) => true;

	/// <summary>
	/// Can the hand let go of this?
	/// </summary>
	public bool CanStopGrabbing( BaseGunplayInteractable interactable, GunplayHand hand ) => true;

	GameObject GameObject { get; }

	ITagSet Tags { get; }

	/// <summary>
	/// The hand holding this, if any.
	/// </summary>
	GunplayHand Hand { get; }

	/// <summary>
	/// Is a hand holding this already?
	/// </summary>
	bool IsHeld { get; }

	/// <summary>
	/// What this is a handle of.
	/// </summary>
	BaseGunplayInteractable Interactable { get; }

	/// <summary>
	/// Which input takes hold of it.
	/// </summary>
	public GrabInputType GrabInput { get; }

	/// <summary>
	/// Hears about every grab in the scene.
	/// </summary>
	public interface IGrabListener
	{
		void OnGrabStart( BaseGunplayInteractable interactable, GunplayHand hand ) { }
		void OnGrabEnd( BaseGunplayInteractable interactable, GunplayHand hand ) { }
	}
}

/// <summary>
/// Which input takes hold of a grabbable.
/// </summary>
public enum GrabInputType
{
	Grip,
	Trigger,
	Hover
}
