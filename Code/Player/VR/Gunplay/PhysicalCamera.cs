using Sandbox.UI;

/// <summary>
/// A camera held in a VR hand. Its screen on the back shows what the lens sees; the trigger of the hand holding it
/// takes a photo from the lens, saved with the game's data, and the stick zooms.
/// </summary>
public sealed class PhysicalCamera : GunplayInteractable
{
	[Property] public CameraFeed Feed { get; set; }

	/// <summary>
	/// The screen on the back: a world panel the lens' picture is shown in.
	/// </summary>
	[Property] public PhysicalCameraScreen Screen { get; set; }

	[Property] public SoundEvent ShutterSound { get; set; }

	[Property, Range( 10, 100 )] public float FieldOfView { get; set; } = 50f;

	[Property] public RangedFloat Zoom { get; set; } = new( 15, 90 );

	/// <summary>
	/// How sharp a photo is, in pixels across.
	/// </summary>
	[Property] public int PhotoWidth { get; set; } = 1920;

	bool _triggerWasDown;
	TimeSince _sincePhoto = 1;

	protected override void OnUpdate()
	{
		if ( Feed.IsValid() )
			Feed.UseLensView( FieldOfView );

		if ( IsProxy || PrimaryGrabPoint?.Hand is not { } hand || hand.Controller is not { } controller )
		{
			_triggerWasDown = false;
			return;
		}

		// The stick zooms
		var stick = controller.Joystick.Value.y;
		if ( MathF.Abs( stick ) > 0.2f )
			FieldOfView = (FieldOfView - stick * 40f * Time.Delta).Clamp( Zoom.Min, Zoom.Max );

		var down = hand.IsTriggerDown();
		if ( down && !_triggerWasDown && _sincePhoto > 0.5f && !(hand.Owner?.PointingAtUI ?? false) )
			TakePhoto( hand );

		_triggerWasDown = down;
	}

	void TakePhoto( GunplayHand hand )
	{
		_sincePhoto = 0;

		if ( Feed?.Camera is { } lens )
		{
			var bitmap = new Bitmap( PhotoWidth, PhotoWidth * 9 / 16 );
			lens.RenderToBitmap( bitmap );

			FileSystem.Data.CreateDirectory( "photos" );
			FileSystem.Data.WriteAllBytes( $"photos/photo_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png", bitmap.ToPng() );
		}

		VRMode.Pulse( hand.Controller, 0.6f, 0.15f );
		Shutter();
	}

	[Rpc.Broadcast]
	void Shutter()
	{
		if ( ShutterSound is not null )
			Sound.Play( ShutterSound, WorldPosition );
	}
}

/// <summary>
/// The screen on the back of a <see cref="PhysicalCamera"/>.
/// </summary>
public sealed class PhysicalCameraScreen : PanelComponent
{
	[Property] public CameraFeed Feed { get; set; }

	Panel _picture;

	protected override void OnTreeFirstBuilt()
	{
		base.OnTreeFirstBuilt();

		Panel.Style.Width = Length.Percent( 100 );
		Panel.Style.Height = Length.Percent( 100 );
		Panel.Style.BackgroundColor = Color.Black;

		_picture = Panel.Add.Panel();
		_picture.Style.Width = Length.Percent( 100 );
		_picture.Style.Height = Length.Percent( 100 );
		_picture.Style.BackgroundSizeX = Length.Percent( 100 );
		_picture.Style.BackgroundSizeY = Length.Percent( 100 );
	}

	protected override void OnUpdate()
	{
		if ( _picture is null || !Feed.IsValid() ) return;

		_picture.Style.SetBackgroundImage( Feed.RequestFrame() );
	}
}
