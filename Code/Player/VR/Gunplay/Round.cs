/// <summary>
/// A round's caliber: what magazine and gun it fits.
/// </summary>
public enum RoundCaliber
{
	None,
	[Title( "5.56x45mm" )]
	FiveFiveSix,
	[Title( "9mm" )]
	Nine,
	[Title( "12ga" )]
	TwelveGauge
}

/// <summary>
/// One round of ammunition, as data in a magazine or a chamber. Ported from gunbuilder's Bullet.
/// </summary>
public partial record Round
{
	public class BodyGroupKeyValue
	{
		[KeyProperty] public int Index { get; set; }
		[KeyProperty] public int Value { get; set; }
	}

	/// <summary>
	/// Fired: just a casing now.
	/// </summary>
	[Property, Group( "Data" )] public bool IsSpent { get; set; }

	[Property, Group( "Setup" )] public RoundCaliber Caliber { get; set; } = RoundCaliber.FiveFiveSix;

	/// <summary>
	/// The body group that shows a spent casing on <see cref="Prefab"/>.
	/// </summary>
	[Property, Group( "Setup" )] public BodyGroupKeyValue SpentCasingBodygroup { get; set; }

	/// <summary>
	/// The round as an object in the world, see <see cref="LooseRound"/>.
	/// </summary>
	[Property, Group( "Setup" )] public GameObject Prefab { get; set; }

	/// <summary>
	/// A spent casing flying out of a gun. Only an effect, on each machine: it isn't networked and can't be picked up.
	/// </summary>
	public GameObject CreateCasing( Vector3 position, Rotation rotation )
	{
		if ( Prefab is null ) return null;

		var go = Prefab.Clone( new CloneConfig { Transform = new Transform( position, rotation ), StartEnabled = false } );
		go.NetworkMode = NetworkMode.Never;
		go.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked;

		foreach ( var grabPoint in go.GetComponentsInChildren<GrabPoint>( true ) )
			grabPoint.GameObject.Destroy();

		foreach ( var interactable in go.GetComponentsInChildren<BaseGunplayInteractable>( true ) )
			interactable.Destroy();

		foreach ( var loose in go.GetComponentsInChildren<LooseRound>( true ) )
			loose.Destroy();

		if ( SpentCasingBodygroup is not null && go.GetComponentInChildren<ModelRenderer>( true ) is { } model )
			model.SetBodyGroup( SpentCasingBodygroup.Index, SpentCasingBodygroup.Value );

		go.Enabled = true;

		var effect = go.AddComponent<TemporaryEffect>();
		effect.DestroyAfterSeconds = 10f;

		return go;
	}
}

/// <summary>
/// A round lying in the world. Push it into a magazine to load it, or into an open chamber.
/// </summary>
public sealed class LooseRound : Component, IAmmoSource
{
	[Property] public Round Bullet { get; set; } = new();

	/// <summary>
	/// Used up: it went into a magazine or a chamber and is on its way out.
	/// </summary>
	public bool Consumed { get; private set; }

	public IEnumerable<Round> Pop( int amount = 1 )
	{
		if ( Consumed || amount < 1 ) yield break;

		Consumed = true;
		yield return Bullet with { IsSpent = false };
	}

	/// <summary>
	/// Removes the round once it's gone into something. Clients can't destroy networked objects, the host does it.
	/// </summary>
	public void Remove()
	{
		Consumed = true;
		GameObject.Enabled = false;
		RemoveOnHost( GameObject );
	}

	[Rpc.Host]
	static void RemoveOnHost( GameObject round )
	{
		if ( round.IsValid() && round.GetComponent<LooseRound>().IsValid() )
			round.Destroy();
	}
}

/// <summary>
/// Somewhere rounds can be taken from.
/// </summary>
public interface IAmmoSource
{
	public IEnumerable<Round> Pop( int amount = 1 );
}
