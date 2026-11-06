/// <summary>
/// A socket on an interactable that an <see cref="Attachable"/> goes into, like a gun's magazine well. Ported from
/// gunbuilder. The owner of the gun decides what goes in and out; everyone else follows <see cref="AttachedObject"/>.
/// </summary>
public sealed class AttachmentPoint : Component, Component.ExecuteInEditor, Component.ITriggerListener, Component.ICollisionListener
{
	[Property] public GunplayInteractable Interactable { get; set; }

	/// <summary>
	/// Editor: a model to show where things go.
	/// </summary>
	[Property, Group( "Editor" )] public Model GuideModel { get; set; }

	/// <summary>
	/// What's accepted: an attachable needs one of these tags. Empty takes anything.
	/// </summary>
	[Property] public TagSet AcceptedTags { get; set; } = new TagSet();

	/// <summary>
	/// What's in here, synced by the owner of the gun.
	/// </summary>
	[Sync] public GameObject AttachedObject { get; private set; }

	/// <summary>
	/// What's in here on this machine.
	/// </summary>
	public Attachable CurrentAttachable { get; private set; }

	TimeSince TimeSinceAttachChanged = 1;

	const float AttachmentDelay = 0.4f;

	protected override void DrawGizmos()
	{
		if ( GuideModel is null ) return;

		Gizmo.Draw.Color = Color.Orange.WithAlpha( 0.4f );
		Gizmo.Draw.Model( GuideModel );
	}

	bool CanAttach( Attachable attachable )
	{
		if ( TimeSinceAttachChanged < AttachmentDelay ) return false;
		if ( attachable.IsAttached ) return false;
		if ( attachable.Interactable == Interactable ) return false;
		if ( !Interactable.CanAccept( attachable ) ) return false;

		if ( AcceptedTags is { } accepted && accepted.Any() && !attachable.Tags.HasAny( accepted ) )
			return false;

		// It has to go in facing the right way, like a magazine
		return attachable.WorldRotation.Forward.Dot( WorldRotation.Forward ) > 0.8f;
	}

	bool CanDetach()
	{
		if ( !CurrentAttachable.IsValid() ) return false;
		return TimeSinceAttachChanged >= AttachmentDelay;
	}

	/// <summary>
	/// Takes out what's in here. Owner of the gun only.
	/// </summary>
	public bool TryDetach()
	{
		if ( IsProxy || !CanDetach() ) return false;

		var attachable = CurrentAttachable;
		CurrentAttachable = null;
		AttachedObject = null;
		TimeSinceAttachChanged = 0;

		Interactable.Detach( attachable, this );
		return true;
	}

	/// <summary>
	/// Puts <paramref name="attachable"/> in here. Owner of the gun only.
	/// </summary>
	public bool TryAttach( Attachable attachable )
	{
		if ( IsProxy || !CanAttach( attachable ) ) return false;

		if ( CurrentAttachable.IsValid() && !TryDetach() ) return false;

		// We move it from now on
		attachable.Interactable?.TakeOwnership();

		CurrentAttachable = attachable;
		AttachedObject = attachable.GameObject;
		TimeSinceAttachChanged = 0;

		Interactable.Attach( attachable, this );
		return true;
	}

	protected override void OnUpdate()
	{
		if ( !Game.IsPlaying ) return;

		if ( IsProxy ) FollowOwner();

		if ( CurrentAttachable.IsValid() )
			CurrentAttachable.WorldTransform = WorldTransform.WithScale( CurrentAttachable.WorldScale );
		else if ( CurrentAttachable is not null && !IsProxy )
		{
			// It was destroyed while in here
			CurrentAttachable = null;
			AttachedObject = null;
		}
	}

	/// <summary>
	/// On everyone but the owner, attach and detach as the owner did.
	/// </summary>
	void FollowOwner()
	{
		var synced = AttachedObject.IsValid() ? AttachedObject.GetComponent<Attachable>() : null;
		if ( synced == CurrentAttachable ) return;

		if ( CurrentAttachable.IsValid() )
			Interactable.Detach( CurrentAttachable, this );

		CurrentAttachable = synced;

		if ( synced.IsValid() )
			Interactable.Attach( synced, this );
	}

	void TryAttachGameObject( GameObject gameObject )
	{
		if ( IsProxy || !gameObject.IsValid() ) return;

		if ( gameObject.Root.GetComponentInChildren<Attachable>() is { } attachable )
			TryAttach( attachable );
	}

	void ITriggerListener.OnTriggerEnter( Collider other ) => TryAttachGameObject( other.GameObject );
	void ICollisionListener.OnCollisionStart( Collision collision ) => TryAttachGameObject( collision.Other.GameObject );
}
