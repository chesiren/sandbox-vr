using Sandbox.VR;

public sealed partial class VRPlayer
{
	// Curl per finger (thumb, index, middle, ring, pinky), six bits each, so everyone sees the same hand
	[Sync] int LeftFingers { get; set; }
	[Sync] int RightFingers { get; set; }

	static readonly string[] FingerNames = { "thumb", "index", "middle", "ring", "pinky" };

	// Degrees each joint bends at a full curl, from the knuckle out. The thumb folds less.
	static readonly float[] FingerBend = { 60f, 90f, 70f };
	static readonly float[] ThumbBend = { 15f, 30f, 45f };

	record struct FingerJoint( GameObject Bone, Transform Rest, int Finger, float Bend, float Side );

	List<FingerJoint> _joints;

	void UpdateFingerPose()
	{
		LeftFingers = PackCurls( Input.VR.LeftHand );
		RightFingers = PackCurls( Input.VR.RightHand );
	}

	static int PackCurls( VRController hand )
	{
		var packed = 0;
		for ( var i = 0; i < 5; i++ )
			packed |= (int)(hand.GetFingerCurl( i ).Clamp( 0f, 1f ) * 63f) << (i * 6);

		return packed;
	}

	static float UnpackCurl( int packed, int finger ) => ((packed >> (finger * 6)) & 63) / 63f;

	/// <summary>
	/// Bends the body's finger bones by the controllers' finger curl. The human anim graph has no finger
	/// parameters, so the finger bones are taken over from the animation and posed from their rest pose.
	/// </summary>
	void UpdateFingers( SkinnedModelRenderer renderer )
	{
		if ( _joints is null && !SetupFingers( renderer ) ) return;

		foreach ( var joint in _joints )
		{
			if ( !joint.Bone.IsValid() ) continue;

			var curl = UnpackCurl( joint.Side > 0 ? RightFingers : LeftFingers, joint.Finger );
			joint.Bone.LocalPosition = joint.Rest.Position;
			joint.Bone.LocalRotation = joint.Rest.Rotation * Rotation.FromPitch( joint.Side * curl * joint.Bend );
		}
	}

	bool SetupFingers( SkinnedModelRenderer renderer )
	{
		if ( renderer.Model is null ) return false;

		renderer.CreateBoneObjects = true;
		_joints = new();

		foreach ( var (suffix, side) in new[] { ("R", 1f), ("L", -1f) } )
		{
			for ( var finger = 0; finger < FingerNames.Length; finger++ )
			{
				var bends = finger == 0 ? ThumbBend : FingerBend;

				for ( var segment = 0; segment < 3; segment++ )
				{
					var name = $"finger_{FingerNames[finger]}_{segment}_{suffix}";
					var bone = renderer.Model.Bones.GetBone( name );
					var go = renderer.GetBoneObject( name );
					if ( bone is null || !go.IsValid() ) continue;

					// Bone.LocalTransform is the bind pose in model space - the bone object needs it relative to its parent
					var rest = bone.Parent is null ? bone.LocalTransform : bone.Parent.LocalTransform.ToLocal( bone.LocalTransform );

					go.Flags |= GameObjectFlags.ProceduralBone;
					_joints.Add( new FingerJoint( go, rest, finger, bends[segment], side ) );
				}
			}
		}

		return true;
	}

	/// <summary>
	/// Hands the finger bones back to the animation.
	/// </summary>
	void ReleaseFingers()
	{
		if ( _joints is null ) return;

		foreach ( var joint in _joints )
		{
			if ( joint.Bone.IsValid() )
				joint.Bone.Flags &= ~GameObjectFlags.ProceduralBone;
		}

		_joints = null;
	}
}
