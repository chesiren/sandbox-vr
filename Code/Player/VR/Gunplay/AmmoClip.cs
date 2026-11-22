/// <summary>
/// Rounds held together to load a gun in one go, like a revolver's speedloader or a shotgun's speed strip. Pushed into
/// an open cylinder or a tube's loading port, it gives up as many rounds as fit and stays in the hand. Push loose rounds
/// into it to fill it back up. Kept by whoever holds it.
/// </summary>
public sealed class AmmoClip : RoundStore
{
	[RequireComponent] public GunplayInteractable Interactable { get; set; }

	/// <summary>
	/// The rounds shown on the model, the first one out first. Those past <see cref="RoundStore.BulletCount"/> are hidden.
	/// They can be the model's bones.
	/// </summary>
	[Property] public List<GameObject> Rounds { get; set; } = new();

	Transform[] _rest;

	protected override void OnLoadedByHand()
	{
		VRMode.Pulse( Interactable?.PrimaryGrabPoint?.Hand?.Controller, 0.2f, 0.2f );
	}

	protected override void OnPreRender()
	{
		if ( Rounds is null || Rounds.Count == 0 ) return;

		if ( _rest is null || _rest.Length != Rounds.Count )
		{
			_rest = Rounds.Select( r => r.IsValid() ? r.LocalTransform : default ).ToArray();

			// Bones go back to the animation every frame unless we own them
			foreach ( var round in Rounds )
				if ( round.IsValid() ) round.Flags |= GameObjectFlags.ProceduralBone;
		}

		var shown = Rounds.Count - BulletCount;
		for ( var i = 0; i < Rounds.Count; i++ )
		{
			if ( !Rounds[i].IsValid() ) continue;
			Rounds[i].LocalTransform = _rest[i].WithScale( i >= shown ? _rest[i].Scale : 0.001f );
		}
	}
}
