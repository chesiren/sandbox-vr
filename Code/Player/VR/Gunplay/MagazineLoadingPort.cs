/// <summary>
/// An opening that loose rounds are pushed through into the magazine in the gun, like a bolt-action rifle's action with
/// the bolt back. Nothing goes in without a magazine. Kept by the gun's owner.
/// </summary>
public sealed class MagazineLoadingPort : Component, Component.ITriggerListener
{
	[Property] public PhysicalGun Gun { get; set; }

	/// <summary>
	/// The part that has to be all the way back to load, like the bolt. None: it can always be loaded.
	/// </summary>
	[Property] public PointInteractable LoadGate { get; set; }

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( IsProxy || Gun?.Magazine is not { } magazine ) return;
		if ( LoadGate.IsValid() && !LoadGate.CompletionValue.AlmostEqual( 1f ) ) return;

		if ( magazine.LoadByHand( other ) > 0 )
			VRMode.Pulse( Gun.GripHand?.Controller, 0.2f, 0.2f );
	}
}
