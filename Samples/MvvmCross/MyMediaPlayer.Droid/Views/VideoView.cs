using System.Collections.Generic;
using Android.App;
using Android.OS;
using MvvmCross.Droid.Views;
using Plugin.MediaManager;
using Plugin.MediaManager.ExoPlayer;
using Com.Google.Android.Exoplayer2.UI;
using Android.Views;
using Android.Graphics;
using Android.Runtime;
using System;
using MvvmCross.Binding.BindingContext;
using MyMediaPlayer.Core.ViewModels;

namespace MyMediaPlayer.Droid.Views
{
    [Activity(Label = "View for VideoViewModel")]
    public class VideoView : MvxActivity //, ISurfaceHolderCallback
    {

        //private MvxVideoPlayerEventLogger _eventLogger;
        private AspectRatioFrameLayout _videoFrame;
        private SurfaceView _surfaceView;
        //private SubtitleLayout _subtitleLayout;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            SetContentView(Resource.Layout.VideoView);

            
            var exoVideoPlayer = new ExoPlayerVideoImplementation();
            //exoVideoPlayer.RequestProperties = new Dictionary<string, string> { { "Test", "1234" } };
            CrossMediaManager.Current.VideoPlayer = exoVideoPlayer;



            _videoFrame = FindViewById<AspectRatioFrameLayout>(Resource.Id.video_frame);
            _surfaceView = FindViewById<SurfaceView>(Resource.Id.surface_view);
            _surfaceView.Holder.AddCallback(this);

            //_subtitleLayout = FindViewById<SubtitleLayout>(Resource.Id.subtitles);


            //new AudioImp
        }

        public MvxVideoItem Item
        {
            get { return _item; }
            set
            {
                if (_item == value)
                {
                    return;
                }

                _item = value;
                PreparePlayer(true);
            }
        }

        protected override void OnViewModelSet()
        {

            base.OnViewModelSet();
            CreateBindings();
        }

        private void CreateBindings()
        {
            var set = this.CreateBindingSet<VideoView, VideoViewModel>();
            set.Bind(this).For(t => t.Item).To(vm => vm.VideoItem);


        }

        public void SurfaceChanged(ISurfaceHolder holder, [GeneratedEnum] Format format, int width, int height)
        {
            throw new NotImplementedException();
        }

        public void SurfaceCreated(ISurfaceHolder holder)
        {
            throw new NotImplementedException();
        }

        public void SurfaceDestroyed(ISurfaceHolder holder)
        {
            throw new NotImplementedException();
        }
    }
}
