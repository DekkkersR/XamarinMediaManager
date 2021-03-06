﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AVFoundation;
using CoreFoundation;
using CoreMedia;
using Foundation;
using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Abstractions.Enums;
using Plugin.MediaManager.Abstractions.EventArguments;
using Plugin.MediaManager.Abstractions.Implementations;

namespace Plugin.MediaManager
{
    public class VideoPlayerImplementation : NSObject, IVideoPlayer
    {
        public static readonly NSString StatusObservationContext =
            new NSString("AVCustomEditPlayerViewControllerStatusObservationContext");

        public static NSString RateObservationContext =
            new NSString("AVCustomEditPlayerViewControllerRateObservationContext");

        private AVPlayer _player;
        private MediaPlayerStatus _status;
        private AVPlayerLayer _videoLayer;

        public Dictionary<string, string> RequestHeaders { get; set; }

        public VideoPlayerImplementation(IVolumeManager volumeManager)
        {
            _volumeManager = volumeManager;
            _status = MediaPlayerStatus.Stopped;

            // Watch the buffering status. If it changes, we may have to resume because the playing stopped because of bad network-conditions.
            BufferingChanged += (sender, e) =>
            {
                // If the player is ready to play, it's paused and the status is still on PLAYING, go on!
                if ((Player.Status == AVPlayerStatus.ReadyToPlay) && (Rate == 0.0f) &&
                    (Status == MediaPlayerStatus.Playing))
                    Player.Play();
            };
            _volumeManager.Mute = Player.Muted;
            _volumeManager.CurrentVolume = Player.Volume;
            _volumeManager.MaxVolume = 1;
            _volumeManager.VolumeChanged += VolumeManagerOnVolumeChanged;
        }

        private void VolumeManagerOnVolumeChanged(object sender, VolumeChangedEventArgs e)
        {
            _player.Volume = (float) e.Volume;
            _player.Muted = e.Mute;
        }

        private AVPlayer Player
        {
            get
            {
                if (_player == null)
                    InitializePlayer();

                return _player;
            }
        }

        private NSUrl nsUrl { get; set; }

        public float Rate
        {
            get
            {
                if (Player != null)
                    return Player.Rate;
                return 0.0f;
            }
            set
            {
                if (Player != null)
                    Player.Rate = value;
            }
        }

        public TimeSpan Position
        {
            get
            {
                if (Player.CurrentItem == null)
                    return TimeSpan.Zero;
                return TimeSpan.FromSeconds(Player.CurrentItem.CurrentTime.Seconds);
            }
        }

        public TimeSpan Duration
        {
            get
            {
                if (Player.CurrentItem == null)
                    return TimeSpan.Zero;
				if (double.IsNaN(Player.CurrentItem.Duration.Seconds))
					return TimeSpan.Zero;
                return TimeSpan.FromSeconds(Player.CurrentItem.Duration.Seconds);
            }
        }

        public TimeSpan Buffered
        {
            get
            {
                var buffered = TimeSpan.Zero;
                if (Player.CurrentItem != null)
                    buffered =
                        TimeSpan.FromSeconds(
                            Player.CurrentItem.LoadedTimeRanges.Select(
                                tr => tr.CMTimeRangeValue.Start.Seconds + tr.CMTimeRangeValue.Duration.Seconds).Max());

                Console.WriteLine("Buffered size: " + buffered);

                return buffered;
            }
        }

        public async Task Stop()
        {
            await Task.Run(() =>
            {
                if (Player.CurrentItem == null)
                    return;

                if (Player.Rate != 0.0)
                    Player.Pause();

                Player.CurrentItem.Seek(CMTime.FromSeconds(0d, 1));

                Status = MediaPlayerStatus.Stopped;
            });
        }

        public async Task Pause()
        {
            await Task.Run(() =>
            {
                Status = MediaPlayerStatus.Paused;

                if (Player.CurrentItem == null)
                    return;

                if (Player.Rate != 0.0)
                    Player.Pause();
            });
        }

        public MediaPlayerStatus Status
        {
            get { return _status; }
            private set
            {
                _status = value;
                StatusChanged?.Invoke(this, new StatusChangedEventArgs(_status));
            }
        }

        public event StatusChangedEventHandler StatusChanged;
        public event PlayingChangedEventHandler PlayingChanged;
        public event BufferingChangedEventHandler BufferingChanged;
        public event MediaFinishedEventHandler MediaFinished;
        public event MediaFailedEventHandler MediaFailed;

        public async Task Seek(TimeSpan position)
        {
            await Task.Run(() => { Player.CurrentItem?.Seek(CMTime.FromSeconds(position.TotalSeconds, 1)); });
        }

        private void InitializePlayer()
        {
            _player = new AVPlayer();
            _videoLayer = AVPlayerLayer.FromPlayer(_player);

            #if __IOS__ || __TVOS__
            var avSession = AVAudioSession.SharedInstance();

            // By setting the Audio Session category to AVAudioSessionCategorPlayback, audio will continue to play when the silent switch is enabled, or when the screen is locked.
            avSession.SetCategory(AVAudioSessionCategory.Playback);

            NSError activationError = null;
            avSession.SetActive(true, out activationError);
            if (activationError != null)
                Console.WriteLine("Could not activate audio session {0}", activationError.LocalizedDescription);
            #endif

            Player.AddPeriodicTimeObserver(new CMTime(1, 4), DispatchQueue.MainQueue, delegate
            {
				double totalProgress = 0;
				if (!double.IsNaN(_player.CurrentItem.Duration.Seconds))
				{
					var totalDuration = TimeSpan.FromSeconds(_player.CurrentItem.Duration.Seconds);
					totalProgress = Position.TotalMilliseconds /
										totalDuration.TotalMilliseconds;
				}
                PlayingChanged?.Invoke(this, new PlayingChangedEventArgs(totalProgress, Position, Duration));
            });
        }

        public async Task Play(IMediaFile mediaFile = null)
        {
            if (mediaFile != null)
                nsUrl = new NSUrl(mediaFile.Url);

            if (Status == MediaPlayerStatus.Paused)
            {
                Status = MediaPlayerStatus.Playing;
                //We are simply paused so just start again
                Player.Play();
                return;
            }

            try
            {
                // Start off with the status LOADING.
                Status = MediaPlayerStatus.Buffering;

                var nsAsset = AVAsset.FromUrl(nsUrl);
                var streamingItem = AVPlayerItem.FromAsset(nsAsset);

                Player.CurrentItem?.RemoveObserver(this, new NSString("status"));

                Player.ReplaceCurrentItemWithPlayerItem(streamingItem);
                streamingItem.AddObserver(this, new NSString("status"), NSKeyValueObservingOptions.New, Player.Handle);
                streamingItem.AddObserver(this, new NSString("loadedTimeRanges"),
                    NSKeyValueObservingOptions.Initial | NSKeyValueObservingOptions.New, Player.Handle);

                Player.CurrentItem.SeekingWaitsForVideoCompositionRendering = true;
                Player.CurrentItem.AddObserver(this, (NSString)"status", NSKeyValueObservingOptions.New |
                                                                          NSKeyValueObservingOptions.Initial,
                    StatusObservationContext.Handle);

                NSNotificationCenter.DefaultCenter.AddObserver(AVPlayerItem.DidPlayToEndTimeNotification,
                                                               notification => MediaFinished?.Invoke(this, new MediaFinishedEventArgs(mediaFile)), Player.CurrentItem);

                Player.Play();
            }
            catch (Exception ex)
            {
                OnMediaFailed();
                Status = MediaPlayerStatus.Stopped;

                //unable to start playback log error
                Console.WriteLine("Unable to start playback: " + ex);
            }

            await Task.CompletedTask;
        }

        public override void ObserveValue(NSString keyPath, NSObject ofObject, NSDictionary change, IntPtr context)
        {
            Console.WriteLine("Observer triggered for {0}", keyPath);

            switch ((string)keyPath)
            {
                case "status":
                    ObserveStatus();
                    return;

                case "loadedTimeRanges":
                    ObserveLoadedTimeRanges();
                    return;

                default:
                    Console.WriteLine("Observer triggered for {0} not resolved ...", keyPath);
                    return;
            }
        }

        private void ObserveStatus()
        {
            Console.WriteLine("Status Observed Method {0}", Player.Status);
            if ((Player.Status == AVPlayerStatus.ReadyToPlay) && (Status == MediaPlayerStatus.Buffering))
            {
                Status = MediaPlayerStatus.Playing;
                Player.Play();
            }
            else if (Player.Status == AVPlayerStatus.Failed)
            {
                OnMediaFailed();
                Status = MediaPlayerStatus.Stopped;
            }
        }

        private void OnMediaFailed()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Description: {Player.Error.LocalizedDescription}");
            builder.AppendLine($"Reason: {Player.Error.LocalizedFailureReason}");
            builder.AppendLine($"Recovery Options: {Player.Error.LocalizedRecoveryOptions}");
            builder.AppendLine($"Recovery Suggestion: {Player.Error.LocalizedRecoverySuggestion}");
            MediaFailed?.Invoke(this, new MediaFailedEventArgs(builder.ToString(), new NSErrorException(Player.Error)));
        }

        private void ObserveLoadedTimeRanges()
        {
            var loadedTimeRanges = _player.CurrentItem.LoadedTimeRanges;
            if (loadedTimeRanges.Length > 0)
            {
                var range = loadedTimeRanges[0].CMTimeRangeValue;
                var duration = TimeSpan.FromSeconds(range.Duration.Seconds);
                var totalDuration = _player.CurrentItem.Duration;
                var bufferProgress = duration.TotalSeconds / totalDuration.Seconds;
                BufferingChanged?.Invoke(this, new BufferingChangedEventArgs(bufferProgress, duration));
            }
            BufferingChanged?.Invoke(this, new BufferingChangedEventArgs(0, TimeSpan.Zero));
        }

        private IVideoSurface _renderSurface;
        public IVideoSurface RenderSurface
        {
            get
            {
                return _renderSurface;
            }
            set
            {
                var view = (VideoSurface)value;
                if (view == null)
                    throw new ArgumentException("VideoSurface must be a UIView");

                _renderSurface = value;
                _videoLayer = AVPlayerLayer.FromPlayer(Player);
                _videoLayer.Frame = view.Frame;
                _videoLayer.VideoGravity = AVLayerVideoGravity.ResizeAspect;
                view.Layer.AddSublayer(_videoLayer);
            }
        }

        private VideoAspectMode _aspectMode;
        private IVolumeManager _volumeManager;

        public VideoAspectMode AspectMode { 
            get {
                return _aspectMode;
            } set {
                switch (value)
                {
                    case VideoAspectMode.None:
                        _videoLayer.VideoGravity = AVLayerVideoGravity.Resize;
                        break;
                    case VideoAspectMode.AspectFit:
                        _videoLayer.VideoGravity = AVLayerVideoGravity.ResizeAspect;
                        break;
                    case VideoAspectMode.AspectFill:
                        _videoLayer.VideoGravity = AVLayerVideoGravity.ResizeAspectFill;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                _aspectMode = value;
            }
        }
    }
}