/// <summary>
/// A gun's magazine. Holds rounds of one caliber; push loose rounds into it to load it. Ported from gunbuilder's
/// WeaponMagazine.
/// </summary>
public sealed class GunMagazine : RoundStore
{
	[RequireComponent] public GunplayInteractable Interactable { get; set; }

	[RequireComponent] public Attachable Attachable { get; set; }

	[Property] public SkinnedModelRenderer Renderer { get; set; }

	[Property] public int AmmoBodygroup { get; set; } = 0;

	int _shownBodygroup = -1;

	protected override void OnUpdate()
	{
		// The top round shows while there's one
		var bodygroup = BulletCount > 0 ? 0 : 1;
		if ( bodygroup == _shownBodygroup || !Renderer.IsValid() ) return;

		Renderer.SetBodyGroup( AmmoBodygroup, bodygroup );
		_shownBodygroup = bodygroup;
	}
}
