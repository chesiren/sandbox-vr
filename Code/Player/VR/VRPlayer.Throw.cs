using Sandbox.VR;

public sealed partial class VRPlayer
{
	/// <summary>
	/// Multiplies the hand's speed when throwing. Nothing pulls on the arm in VR, so people throw softer than they mean to.
	/// </summary>
	[Property, Group( "VR Throw" )] public float ThrowScale { get; set; } = 1.3f;

	readonly HandMotion _leftMotion = new();
	readonly HandMotion _rightMotion = new();

	void UpdateHandMotion()
	{
		var vr = Input.VR;

		// In the play area's space, so turning and moving the rig don't count as swinging the hand
		_leftMotion.Add( vr.Anchor.ToLocal( vr.LeftHand.Transform ).Position );
		_rightMotion.Add( vr.Anchor.ToLocal( vr.RightHand.Transform ).Position );
	}

	/// <summary>
	/// How fast something let go of right now should fly, in world space: the faster hand's recent swing plus how
	/// fast the body's moving. Local VR player only.
	/// </summary>
	public Vector3 ThrowVelocity
	{
		get
		{
			var left = _leftMotion.Peak();
			var right = _rightMotion.Peak();
			var hand = left.LengthSquared > right.LengthSquared ? left : right;

			return Input.VR.Anchor.Rotation * hand * ThrowScale + Controller.Velocity;
		}
	}

	/// <summary>
	/// A hand's recent positions. The hand is already slowing down by the time the grip opens, so a throw takes the
	/// fastest moment of the last few frames rather than the speed at release.
	/// </summary>
	sealed class HandMotion
	{
		const float Window = 0.12f;

		/// <summary>
		/// Speed is measured over at least this long, a single frame is mostly tracking noise.
		/// </summary>
		const float Span = 0.03f;

		readonly List<(float Time, Vector3 Position)> _samples = new();

		public void Add( Vector3 position )
		{
			var now = Time.Now;
			_samples.Add( (now, position) );
			_samples.RemoveAll( x => now - x.Time > Window );
		}

		public Vector3 Peak()
		{
			var peak = Vector3.Zero;
			var start = 0;

			for ( var end = 1; end < _samples.Count; end++ )
			{
				while ( _samples[end].Time - _samples[start + 1].Time >= Span )
					start++;

				var time = _samples[end].Time - _samples[start].Time;
				if ( time < Span ) continue;

				var velocity = (_samples[end].Position - _samples[start].Position) / time;
				if ( velocity.LengthSquared > peak.LengthSquared )
					peak = velocity;
			}

			return peak;
		}
	}
}
