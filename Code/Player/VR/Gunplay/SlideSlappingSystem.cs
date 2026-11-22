/// <summary>
/// Slapping a locked-back slide or bolt forward with the other hand. Ported from gunbuilder. On the gun's owner.
/// </summary>
public sealed class SlideSlappingSystem : Component, Component.ITriggerListener
{
	[Property] public PointInteractable PointInteractable { get; set; }

	/// <summary>
	/// How fast the hand has to be going along the slide, relative to the gun, in units a second.
	/// </summary>
	[Property] public float MinSpeed { get; set; } = 80f;

	/// <summary>
	/// The hand near the slide, if any.
	/// </summary>
	public GunplayHand Hand { get; set; }

	public bool IsPulled => PointInteractable.CompletionValue.AlmostEqual( 1f );

	Vector3 _previousPosition;

	protected override void OnEnabled()
	{
		_previousPosition = WorldPosition;
	}

	protected override void OnUpdate()
	{
		// The gun moves with the hand holding it: only the slapping hand's movement against it counts
		var movement = WorldPosition - _previousPosition;
		_previousPosition = WorldPosition;

		if ( IsProxy || !Hand.IsValid() ) return;

		if ( IsPulled && IsSlap( Hand.Velocity - movement ) )
		{
			PointInteractable.CompletionValue = 0f;
			Hand = null;
			return;
		}

		if ( Hand.WorldPosition.Distance( WorldPosition ) > 64f )
			Hand = null;
	}

	/// <summary>
	/// Hard enough, and mostly along the slide towards the muzzle. Takes the hand's movement this frame.
	/// </summary>
	bool IsSlap( Vector3 movement )
	{
		if ( movement.Length < MinSpeed * Time.Delta ) return false;

		// Against the slide's own axes, so it doesn't matter where the gun's pointing
		var local = (WorldRotation.Inverse * movement).Normal;
		return local.x > 0.5f;
	}

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( other.GameObject.GetComponent<GunplayHand>() is { } hand )
			Hand = hand;
	}
}
