/// <summary>
/// Slapping a locked-back slide or bolt forward with the other hand. Ported from gunbuilder. On the gun's owner.
/// </summary>
public sealed class SlideSlappingSystem : Component, Component.ITriggerListener
{
	[Property] public PointInteractable PointInteractable { get; set; }

	/// <summary>
	/// The trigger the hand slaps.
	/// </summary>
	[Property] public Collider Slapper { get; set; }

	/// <summary>
	/// The hand near the slide, if any.
	/// </summary>
	public GunplayHand Hand { get; set; }

	public bool IsPulled => PointInteractable.CompletionValue.AlmostEqual( 1f );

	protected override void OnUpdate()
	{
		if ( IsProxy || !Hand.IsValid() ) return;

		if ( IsPulled && IsCorrectDirectionAndSpeed() )
		{
			PointInteractable.CompletionValue = 0f;
			Hand = null;
			return;
		}

		if ( Hand.WorldPosition.Distance( WorldPosition ) > 64f )
			Hand = null;
	}

	/// <summary>
	/// Hard enough, and along the slide.
	/// </summary>
	bool IsCorrectDirectionAndSpeed()
	{
		var velocity = Hand.Velocity;
		var relativeDir = (WorldRotation.Inverse * velocity).Normal;
		var howForward = relativeDir.Dot( WorldRotation.Forward );

		if ( velocity.Length < 1.2f ) return false;
		return howForward > 0.25f && howForward < 0.75f;
	}

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( other.GameObject.GetComponent<GunplayHand>() is { } hand )
			Hand = hand;
	}
}
