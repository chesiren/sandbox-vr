/// <summary>
/// A magazine built into a gun, like a shotgun's tube or a bolt-action rifle's box: it doesn't come out, it's loaded a
/// round at a time by pushing loose rounds into its trigger. Kept by the gun's owner.
/// </summary>
public sealed class InternalMagazine : RoundStore
{
	/// <summary>
	/// The part that has to be all the way back to load, like a rifle's bolt. None: it can always be loaded, like a
	/// shotgun's loading port.
	/// </summary>
	[Property] public PointInteractable LoadGate { get; set; }

	protected override bool CanLoadByHand => !LoadGate.IsValid() || LoadGate.CompletionValue.AlmostEqual( 1f );

	protected override void OnLoadedByHand()
	{
		var gun = GetComponentInParent<PhysicalGun>();
		VRMode.Pulse( gun?.GripHand?.Controller, 0.2f, 0.2f );
	}
}
