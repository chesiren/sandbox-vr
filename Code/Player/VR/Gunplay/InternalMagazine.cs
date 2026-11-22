/// <summary>
/// A magazine built into a gun, like a shotgun's tube: it doesn't come out, it's loaded by pushing loose rounds into its
/// trigger one at a time, or a whole <see cref="AmmoClip"/> at once. Kept by the gun's owner.
/// </summary>
public sealed class InternalMagazine : RoundStore
{
	/// <summary>
	/// The part that has to be all the way back to load, like a rifle's bolt. None: it can always be loaded, like a
	/// shotgun's loading port.
	/// </summary>
	[Property] public PointInteractable LoadGate { get; set; }

	protected override bool CanLoadByHand => !LoadGate.IsValid() || LoadGate.CompletionValue.AlmostEqual( 1f );

	protected override bool AcceptsClips => true;

	protected override void OnLoadedByHand()
	{
		var gun = GetComponentInParent<PhysicalGun>();
		VRMode.Pulse( gun?.GripHand?.Controller, 0.2f, 0.2f );
	}
}
