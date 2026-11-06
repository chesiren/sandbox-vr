/// <summary>
/// A place a hand can hold an interactable by, like the grip of a gun. Needs a trigger collider for the hand to find
/// it. Ported from gunbuilder.
/// </summary>
public sealed class GrabPoint : Component, IGrabbable
{
	[Property] public Action OnGrabStartEvent { get; set; }
	[Property] public Action OnGrabEndEvent { get; set; }

	/// <summary>
	/// The trigger the hand finds this by.
	/// </summary>
	[Property] public Collider Collider { get; set; }

	/// <summary>
	/// What this is a handle of.
	/// </summary>
	[Property] public BaseGunplayInteractable Interactable { get; set; }

	/// <summary>
	/// For a secondary grab point, the primary one it goes with.
	/// </summary>
	[Property] public GrabPoint PrimaryGrabPoint { get; set; }

	/// <summary>
	/// How the fingers sit while holding this.
	/// </summary>
	[Property, Title( "Hand Preset" )] public HandPreset Preset { get; set; } = HandPreset.GripWithoutIndexFinger;

	/// <summary>
	/// Which input takes hold of it.
	/// </summary>
	[Property] public GrabInputType GrabInput { get; set; } = GrabInputType.Grip;

	public bool IsSecondaryGrabPoint => PrimaryGrabPoint.IsValid();
	public bool IsPrimaryGrabPoint => !IsSecondaryGrabPoint;

	/// <summary>
	/// The hand holding this, on the holder's machine.
	/// </summary>
	public GunplayHand HeldHand { get; set; }

	GameObject IGrabbable.GameObject => GameObject;
	ITagSet IGrabbable.Tags => Tags;
	GunplayHand IGrabbable.Hand => HeldHand;
	bool IGrabbable.IsHeld => HeldHand.IsValid();

	bool IGrabbable.StartGrabbing( GunplayHand hand )
	{
		if ( !(Interactable?.Interact( hand, this ) ?? false) ) return false;

		HeldHand = hand;
		OnGrabStartEvent?.Invoke();
		NotifyGrabListeners( hand, true );
		return true;
	}

	bool IGrabbable.StopGrabbing( GunplayHand hand ) => StopGrabbing( hand, false );

	/// <summary>
	/// Lets go. <paramref name="force"/> skips the delay between grabbing and letting go, for a hand that's going away.
	/// </summary>
	public bool StopGrabbing( GunplayHand hand, bool force )
	{
		if ( !(Interactable?.StopInteract( this, force ) ?? false) ) return false;

		HeldHand = null;
		OnGrabEndEvent?.Invoke();
		NotifyGrabListeners( hand, false );
		return true;
	}

	void NotifyGrabListeners( GunplayHand hand, bool start )
	{
		foreach ( var listener in Scene.GetAllComponents<IGrabbable.IGrabListener>() )
		{
			if ( start ) listener.OnGrabStart( Interactable, hand );
			else listener.OnGrabEnd( Interactable, hand );
		}
	}

	protected override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected ) return;

		Gizmo.Draw.Color = Color.Cyan.WithAlpha( 0.2f );
		Gizmo.Draw.Model( "models/first_person/hand_human_right.vmdl" );
	}
}
