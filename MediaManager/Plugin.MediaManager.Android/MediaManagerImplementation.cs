using Android.App;
using Android.Content.Res;
using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Abstractions.Implementations;
using Plugin.MediaManager.Audio;
using Plugin.MediaManager.MediaSession;

namespace Plugin.MediaManager
{
    [Android.Runtime.Preserve(AllMembers = true)]
    public class MediaManagerImplementation : MediaManagerBase
    {
        public MediaManagerImplementation()
        {
            MediaSessionManager.OnNotificationActionFired += HandleNotificationActions;
        }

        private IAudioPlayer _audioPlayer;
        private IMediaExtractor _mediaExtraxtor;

        public override IAudioPlayer AudioPlayer
        {
            get {return _audioPlayer ?? (_audioPlayer = new AudioPlayerImplementation(MediaSessionManager));}
            set { _audioPlayer = value; }
        }

        public override IVideoPlayer VideoPlayer { get; set; } = new VideoPlayerImplementation();

        public override IMediaNotificationManager MediaNotificationManager
        {
            get { return MediaSessionManager.NotificationManager; }
            set { MediaSessionManager.NotificationManager = value; }
        }

        public override IMediaExtractor MediaExtractor
        {
            get { return _mediaExtraxtor ?? (_mediaExtraxtor = new MediaExtractorImplementation(Resources.System, RequestHeaders)); }
            set { _mediaExtraxtor = value; }
        }

        public override IVolumeManager VolumeManager { get; set; } = new VolumeManagerImplementation();

        public MediaSessionManager MediaSessionManager { get; set; } = new MediaSessionManager(Application.Context);

        private async void HandleNotificationActions(object sender, string action)
        {
            if (action.Equals(MediaServiceBase.ActionPlay))
            {
                await PlayPause();
            }
            else if (action.Equals(MediaServiceBase.ActionPause))
            {
                await Pause();
            }
            else if (action.Equals(MediaServiceBase.ActionPrevious))
            {
                await PlayPrevious();
            }
            else if (action.Equals(MediaServiceBase.ActionNext))
            {
                await PlayNext();
            }
            else if (action.Equals(MediaServiceBase.ActionStop))
            {
                await Stop();
            }
        }
    }
}