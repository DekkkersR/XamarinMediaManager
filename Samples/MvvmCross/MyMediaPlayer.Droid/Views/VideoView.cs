using System.Collections.Generic;
using Android.App;
using Android.OS;
using MvvmCross.Droid.Views;
using Plugin.MediaManager;
using Plugin.MediaManager.ExoPlayer;

namespace MyMediaPlayer.Droid.Views
{
    [Activity(Label = "View for VideoViewModel")]
    public class VideoView : MvxActivity
    {
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            SetContentView(Resource.Layout.VideoView);

            
            var exoVideoPlayer = new ExoPlayerVideoImplementation();
            //exoVideoPlayer.RequestProperties = new Dictionary<string, string> { { "Test", "1234" } };
            CrossMediaManager.Current.VideoPlayer = exoVideoPlayer;

            //new AudioImp
        }
    }
}
