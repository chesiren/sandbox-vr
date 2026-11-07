using System.Text.Json.Serialization;

/// <summary>
/// A part of a gun pulled along one axis by a hand, like a slide or a bolt. Ported from gunbuilder. The gun's owner
/// moves it; everyone else follows <see cref="SyncedCompletion"/>.
/// </summary>
public sealed class PointInteractable : BaseGunplayInteractable
{
	/// <summary>
	/// The completion value changed, from the first value to the second. On the gun's owner only.
	/// </summary>
	[Property, Group( "Events" )] public Action<float, float> OnCompletionValue { get; set; }

	[Property, Group( "Components" )] public SkinnedModelRenderer Renderer { get; set; }

	/// <summary>
	/// Played when it gets back to the start.
	/// </summary>
	[Property, Group( "Sounds" )] public SoundEvent ZeroSound { get; set; }

	/// <summary>
	/// Played when it gets all the way to the end.
	/// </summary>
	[Property, Group( "Sounds" )] public SoundEvent OneSound { get; set; }

	/// <summary>
	/// The bone moved along.
	/// </summary>
	[Property, Group( "Setup" )] public GameObject BoneGameObject { get; set; }

	/// <summary>
	/// Where it starts, and where the hand takes hold.
	/// </summary>
	[Property, Group( "Setup" )] public GameObject Start { get; set; }

	/// <summary>
	/// Where it goes all the way to.
	/// </summary>
	[Property, Group( "Setup" )] public GameObject End { get; set; }

	/// <summary>
	/// Go back to the start when let go of, unless pulled all the way.
	/// </summary>
	[Property, Group( "Configuration" )] public bool ResetOnRelease { get; set; }

	/// <summary>
	/// How the bone turns at the end, like a bolt handle locking back.
	/// </summary>
	[Property] public Angles AnglesAtOne { get; set; }

	/// <summary>
	/// A grab point of the gun itself that moves this part, like a shotgun's pump: the hand on it holds and aims the gun
	/// with the other one, and pulls the part along as it goes. None: the part has its own grab point.
	/// </summary>
	[Property, Group( "Setup" )] public GrabPoint DrivenBy { get; set; }

	IGrabbable HandleGrabPoint => DrivenBy.IsValid() ? DrivenBy : PrimaryGrabPoint;

	public GunplayHand Hand => HandleGrabPoint?.Hand;

	public Transform InitialBoneLocalTransform { get; set; }

	public float DistanceBetweenStartAndEnd { get; set; }

	/// <summary>
	/// How far along it is, synced from the gun's owner.
	/// </summary>
	[Sync] public float SyncedCompletion { get; private set; }

	float completionValue;

	/// <summary>
	/// How far along it is: 0 at the start, 1 all the way. Set on the gun's owner.
	/// </summary>
	[Property, JsonIgnore, Range( 0, 1 ), Group( "Debugging" )]
	public float CompletionValue
	{
		get => completionValue;
		set
		{
			if ( IsProxy ) return;

			var prev = completionValue;
			completionValue = value;
			SyncedCompletion = value;

			if ( !completionValue.Equals( prev ) )
				OnCompletionValueChanged( prev, completionValue );
		}
	}

	void OnCompletionValueChanged( float prev, float value )
	{
		if ( prev != 0 && value == 0 )
			GunplaySounds.Play( ZeroSound, WorldPosition );

		if ( prev != 1 && value == 1 )
			GunplaySounds.Play( OneSound, WorldPosition );

		OnCompletionValue?.Invoke( prev, value );
	}

	protected override void OnStart()
	{
		base.OnStart();

		if ( Renderer.IsValid() && Start.IsValid() && End.IsValid() && BoneGameObject.IsValid() )
		{
			// We move the bone, not the model's animation
			BoneGameObject.Flags |= GameObjectFlags.ProceduralBone;

			DistanceBetweenStartAndEnd = Vector3.DistanceBetween( End.WorldPosition, Start.WorldPosition );
			InitialBoneLocalTransform = BoneGameObject.LocalTransform;
		}

		if ( HandleGrabPoint is GrabPoint grabPoint )
			grabPoint.OnGrabEndEvent += OnGrabEnd;

		if ( IsProxy ) completionValue = SyncedCompletion;
	}

	protected override void OnDestroy()
	{
		if ( HandleGrabPoint is GrabPoint grabPoint && grabPoint.IsValid() )
			grabPoint.OnGrabEndEvent -= OnGrabEnd;
	}

	void OnGrabEnd()
	{
		if ( ResetOnRelease && !CompletionValue.AlmostEqual( 1 ) )
			CompletionValue = 0;
	}

	Vector3 CalcLocalPosition()
	{
		var pos = InitialBoneLocalTransform.Position;
		var direction = (End.LocalPosition - Start.LocalPosition).Normal;
		return pos + direction * (DistanceBetweenStartAndEnd * completionValue);
	}

	protected override void OnPreRender()
	{
		if ( !BoneGameObject.IsValid() || !Start.IsValid() || !End.IsValid() ) return;

		if ( IsProxy )
			completionValue = SyncedCompletion;

		var hand = Hand;
		if ( !IsProxy && hand.IsValid() )
		{
			// How far back along the axis the hand is from the start
			var localHand = Start.WorldTransform.ToLocal( hand.WorldTransform );
			CompletionValue = localHand.Position.x.Remap( 0, -DistanceBetweenStartAndEnd, 0, 1, true );

			// The grab point, and so the hand on it, slides with the part
			HandleGrabPoint.GameObject.WorldPosition = Vector3.Lerp( Start.WorldPosition, End.WorldPosition, completionValue );
		}

		// The bone keeps how the model turns it, like a stock that's mirrored round
		var rotation = InitialBoneLocalTransform.Rotation;
		if ( completionValue.AlmostEqual( 1f ) )
			rotation *= AnglesAtOne.ToRotation();

		BoneGameObject.LocalTransform = new Transform( CalcLocalPosition(), rotation, 1 );
	}
}
