/// <summary>
/// A gun's slide: pulled all the way back it throws out the chambered round, going forward it feeds one from the
/// magazine, and the slide release (A) sends it forward when it's locked back. Ported from gunbuilder. On the gun's
/// owner.
/// </summary>
public sealed class SlideReleaseSystem : Component
{
	[RequireComponent] PointInteractable PointInteractable { get; set; }

	[Property] public PhysicalGun Weapon { get; set; }

	/// <summary>
	/// The grab point whose hand's A button releases the slide.
	/// </summary>
	[Property] public GrabPoint InputGrabPoint { get; set; }

	/// <summary>
	/// Is the slide all the way back?
	/// </summary>
	public bool IsPulled => PointInteractable.CompletionValue.AlmostEqual( 1f ) || (IsProxy && PointInteractable.SyncedCompletion.AlmostEqual( 1f ));

	/// <summary>
	/// Is the slide or bolt all the way home?
	/// </summary>
	public bool IsClosed => IsProxy ? PointInteractable.SyncedCompletion.AlmostEqual( 0f, 0.05f ) : PointInteractable.CompletionValue.AlmostEqual( 0f, 0.05f );

	public GunplayHand Hand => InputGrabPoint?.HeldHand;

	/// <summary>
	/// Runs back and home on its own with each shot, like a pistol's slide. Not a rifle's charging handle, which stays put.
	/// </summary>
	[Property] public bool CyclesOnShot { get; set; }

	/// <summary>
	/// A shot's movement, on every machine.
	/// </summary>
	public void OnShot()
	{
		if ( CyclesOnShot )
			PointInteractable.PlayCycle();
	}

	protected override void OnStart()
	{
		PointInteractable.OnCompletionValue += OnCompletionValue;
	}

	protected override void OnDestroy()
	{
		if ( PointInteractable.IsValid() )
			PointInteractable.OnCompletionValue -= OnCompletionValue;
	}

	void OnCompletionValue( float before, float after )
	{
		if ( !Weapon.IsValid() ) return;

		if ( after == 0 )
			Weapon.TryFeedFromMagazine();

		if ( after == 1 )
			Weapon.TryEjectFromChamber();
	}

	/// <summary>
	/// Locks the slide back, like after the last shot.
	/// </summary>
	public void PullManually()
	{
		PointInteractable.CompletionValue = 1f;
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || Hand?.Controller is not { } controller ) return;

		if ( controller.ButtonA.IsPressed && IsPulled )
			PointInteractable.CompletionValue = 0;
	}
}
