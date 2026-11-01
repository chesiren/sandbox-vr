/// <summary>
/// Low in the view, for the screen HUD's <see cref="Sandbox.UI.Notices"/> to sit on. Follows the head softly, the one thing besides
/// the damage indicators that comes to the player, since notices can't wait to be looked at.
/// </summary>
public sealed class VRNoticeBand : VRHudPanel
{
	/// <summary>
	/// How far in front of the eyes the notices are.
	/// </summary>
	[Property] public float Distance { get; set; } = 26f;

	/// <summary>
	/// How far below the eyes the notices are.
	/// </summary>
	[Property] public float Drop { get; set; } = 9f;

	/// <summary>
	/// How wide the panel is, in world units.
	/// </summary>
	[Property] public float Width { get; set; } = 20f;

	/// <summary>
	/// How quickly the notices catch up with where you look.
	/// </summary>
	[Property] public float FollowSpeed { get; set; } = 6f;

	protected override Vector2 PanelPixels => new( 1100, 400 );

	float _yaw = float.NaN;
	float _pitch;

	protected override void Place( Transform head )
	{
		var look = head.Rotation.Angles();
		var pitch = look.pitch.Clamp( -40f, 40f );

		if ( float.IsNaN( _yaw ) )
		{
			_yaw = look.yaw;
			_pitch = pitch;
		}

		var t = 1f - MathF.Exp( -FollowSpeed * Time.Delta );
		_yaw += Angles.NormalizeAngle( look.yaw - _yaw ) * t;
		_pitch += (pitch - _pitch) * t;

		var rotation = Rotation.From( _pitch, _yaw, 0 );
		PlaceAt( head.Position + rotation.Forward * Distance + rotation.Down * Drop, Rotation.LookAt( -rotation.Forward, rotation.Up ), Width );
	}
}
