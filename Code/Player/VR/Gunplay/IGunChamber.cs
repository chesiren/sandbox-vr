/// <summary>
/// Where a gun's next round is fired from: its chamber, or a revolver's cylinder. Kept by the gun's owner.
/// </summary>
public interface IGunChamber
{
	/// <summary>
	/// A live round is ready to fire.
	/// </summary>
	bool HasRound { get; }

	/// <summary>
	/// A fired casing is still in it.
	/// </summary>
	bool HasSpentCasing { get; }

	/// <summary>
	/// Takes the round ready to fire out, live. Owner only.
	/// </summary>
	Round Eject();

	/// <summary>
	/// Fills it from <paramref name="src"/>. Owner only.
	/// </summary>
	/// <returns>How many rounds went in.</returns>
	int Feed( IAmmoSource src );

	/// <summary>
	/// The round just fired leaves its casing behind. Owner only.
	/// </summary>
	void LeaveSpentCasing();

	/// <summary>
	/// Takes the fired casing out. Owner only.
	/// </summary>
	/// <returns>There was one.</returns>
	bool EjectSpentCasing();
}
