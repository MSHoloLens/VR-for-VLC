using SharpDX.Direct3D11;
using SharpDX.MediaFoundation;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;

namespace VRPlayer
{
    public class MediaPlayer
    {
        private DXGIDeviceManager dxgiDeviceManager;
        private MediaEngine mediaEngine;
        private MediaEngineEx mediaEngineEx;
        private SharpDX.Direct3D11.Device3 d3dDevice;

        public static bool IsEndOfStream;
        private bool isVideoStopped;
        private readonly object lockObject = new object();

        /// <summary>
        /// Constructor
        /// </summary>
        public MediaPlayer()
        {
            isVideoStopped = true;
        }

        /// <summary>
        /// Gets whether this media player is playing a video or audio.
        /// </summary>
        public bool IsPlaying { get; private set; }

        /// <summary>
        /// Gets or sets if this video should loop when ready
        /// </summary>
        public bool Looping { get; set; }

        /// <summary>
        /// Gets or sets the url used to play the stream.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Output Video texture (must be <see cref="SharpDX.DXGI.Format.B8G8R8A8_UNorm"/>)
        /// </summary>
        public SharpDX.Direct3D11.Texture2D OutputVideoTexture;

        /// <summary>
        /// Movie url
        /// </summary>
        public static string FileName;

        /// <summary>
        /// start movie time (in seconds)
        /// </summary>
        public static double StartPosition = 0;

        public ShaderResourceView textureView;

        private ByteStream byteStream;

        public virtual void Initialize(SharpDX.Direct3D11.Device3 device)
        {
            lock (lockObject)
            {
                // Startup MediaManager
                MediaManager.Startup(useLightVersion: true);

                d3dDevice = device;

                // Setup multithread on the Direct3D11 device
                var multithread = d3dDevice.QueryInterface<SharpDX.Direct3D.DeviceMultithread>();
                multithread.SetMultithreadProtected(true);

                // Create a DXGI Device Manager
                dxgiDeviceManager = new DXGIDeviceManager();
                dxgiDeviceManager.ResetDevice(d3dDevice);

                // Setup Media Engine attributes
                var attributes = new MediaEngineAttributes
                {
                    DxgiManager = dxgiDeviceManager,
                    VideoOutputFormat = (int)SharpDX.DXGI.Format.B8G8R8A8_UNorm
                };

                using (var factory = new MediaEngineClassFactory())
                    mediaEngine = new MediaEngine(factory, attributes, MediaEngineCreateFlags.None, OnMediaEngineEvent);
                mediaEngineEx = mediaEngine.QueryInterface<MediaEngineEx>();
            }
        }

        public async void Load(string fileName = null)
        {
            if (fileName == null && FileName != null)
            {
                fileName = FileName;
            }
            else
            {
                throw new Exception("No file load: " + fileName);
            }


            if (fileName.StartsWith("http")
                || fileName.StartsWith("rstp"))
            {
                mediaEngineEx.Source = fileName;
            }
            else
            {
                Windows.Storage.StorageFile file = await Windows.Storage.StorageFile.GetFileFromPathAsync(fileName);
                var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
                ByteStream byteStream = new ByteStream(stream);
                mediaEngineEx.SetSourceFromByteStream(byteStream, fileName);
            }

        }

        public virtual void OnRender()
        {
            lock (lockObject)
            {
                if (isVideoStopped)
                    return;

                if (mediaEngineEx != null)
                {
                    long pts;
                    if (mediaEngineEx.OnVideoStreamTick(out pts))
                    {
                        if (OutputVideoTexture != null)
                        {
                            var desc = OutputVideoTexture.Description;
                            var dxgiSurface = OutputVideoTexture.QueryInterface<SharpDX.DXGI.Surface>();
                            var region = new SharpDX.Mathematics.Interop.RawRectangle(0, 0, desc.Width, desc.Height);

                            try
                            {
                                // Blit the frame to the supplied rendertarget
                                mediaEngineEx.TransferVideoFrame(dxgiSurface, null, region, null);
                            }
                            catch (Exception)
                            {
                                // This exception can be worked around by using DirectX 9 only (see configuration)
                                Debug.WriteLine("Exception during TransferVideoFrame");
                            }
                        }
                    }
                }
            }
        }

        public void Shutdown()
        {
            lock (lockObject)
            {
                StopVideo();

                if (mediaEngineEx != null)
                {
                    mediaEngineEx.Shutdown();
                }
            }
        }

        private void SetBytestream(Stream stream)
        {
            byteStream = new ByteStream(stream);
            mediaEngineEx.SetSourceFromByteStream(byteStream, Url);
        }

        /// <summary>
        /// Plays the audio/video.
        /// </summary>
        public void Play()
        {
            if (mediaEngineEx != null)
            {
                if (mediaEngineEx.HasVideo() && isVideoStopped)
                    isVideoStopped = false;

                if (IsEndOfStream)
                {
                    PlaybackPosition = 0;
                    IsPlaying = true;
                }
                else
                {
                    if (textureView == null)
                    {
                        int width = 0;
                        int height = 0;

                        mediaEngineEx.GetNativeVideoSize(out width, out height);

                        OutputVideoTexture = new SharpDX.Direct3D11.Texture2D(
                        d3dDevice,
                        new SharpDX.Direct3D11.Texture2DDescription()
                        {
                            ArraySize = 1,
                            Width = width,
                            Height = height,
                            Usage = SharpDX.Direct3D11.ResourceUsage.Default,
                            Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                            CpuAccessFlags = SharpDX.Direct3D11.CpuAccessFlags.None,
                            BindFlags = SharpDX.Direct3D11.BindFlags.RenderTarget | SharpDX.Direct3D11.BindFlags.ShaderResource,
                            OptionFlags = SharpDX.Direct3D11.ResourceOptionFlags.None,
                            SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                            MipLevels = 1,
                        });

                        textureView = new ShaderResourceView(d3dDevice, OutputVideoTexture);
                    }
                    PlaybackPosition = StartPosition;
                    mediaEngineEx.Play();
                }

                IsEndOfStream = false;
            }
        }

        /// <summary>
        /// Pauses the audio/video.
        /// </summary>
        public void Pause()
        {
            if (mediaEngineEx != null)
                mediaEngineEx.Pause();
        }

        /// <summary>
        /// Gets or sets the volume.
        /// </summary>
        public double Volume
        {
            get
            {
                if (mediaEngineEx != null)
                    return mediaEngineEx.Volume;
                return 0.0;
            }
            set
            {
                if (mediaEngineEx != null)
                    mediaEngineEx.Volume = value;
            }
        }

        /// <summary>
        /// Gets or sets the balance.
        /// </summary>
        public double Balance
        {
            get
            {
                if (mediaEngineEx != null)
                    return mediaEngineEx.Balance;
                return 0.0;
            }
            set
            {
                if (mediaEngineEx != null)
                    mediaEngineEx.Balance = value;
            }
        }

        /// <summary>
        /// Gets or sets muted mode.
        /// </summary>
        public bool Mute
        {
            get
            {
                if (mediaEngineEx != null)
                    return mediaEngineEx.Muted;
                return false;
            }
            set
            {
                if (mediaEngineEx != null)
                    mediaEngineEx.Muted = value;
            }
        }

        /// <summary>
        /// Steps forward or backward one frame.
        /// </summary>
        public void FrameStep(bool forward)
        {
            if (mediaEngineEx != null)
                mediaEngineEx.FrameStep(forward);
        }

        /// <summary>
        /// Gets the duration of the audio/video.
        /// </summary>
        public double Duration
        {
            get
            {
                double duration = 0.0;
                if (mediaEngineEx != null)
                {
                    duration = mediaEngineEx.Duration;
                    if (double.IsNaN(duration))
                        duration = 0.0;
                }
                return duration;
            }
        }

        public void FastForward(double skipTime)
        {
            if (mediaEngineEx != null)
            {
                if (Duration - PlaybackPosition >= skipTime)
                    PlaybackPosition = Duration;
                else
                    PlaybackPosition = mediaEngineEx.CurrentTime + skipTime;
            }
                

            Debug.WriteLine("CurrentTime:" + mediaEngineEx.CurrentTime);
            Debug.WriteLine("Duration:" + mediaEngineEx.Duration);
        }

        /// <summary>
        /// Gets a boolean indicating whether the audio/video is seekable.
        /// </summary>
        public bool CanSeek
        {
            get
            {
                if (mediaEngineEx != null)
                    return (mediaEngineEx.ResourceCharacteristics & ResourceCharacteristics.CanSeek) != 0 && Duration != 0.0;
                return false;
            }
        }

        /// <summary>
        /// Gets or sets the playback position.
        /// </summary>
        public double PlaybackPosition
        {
            get
            {
                if (mediaEngineEx != null)
                    return mediaEngineEx.CurrentTime;
                return 0.0;
            }
            set
            {
                if (mediaEngineEx != null)
                    mediaEngineEx.CurrentTime = value;
            }
        }

        /// <summary>
        /// Gets a boolean indicating whether the audio/video is seeking.
        /// </summary>
        public bool IsSeeking
        {
            get
            {
                if (mediaEngineEx != null)
                    return mediaEngineEx.IsSeeking;
                return false;
            }
        }

        public void StopVideo()
        {
            isVideoStopped = true;
            IsPlaying = false;
        }

        protected virtual void OnMediaEngineEvent(MediaEngineEvent mediaEvent, long param1, int param2)
        {
            switch (mediaEvent)
            {
                case MediaEngineEvent.NotifyStableState:
                    SetEvent(new IntPtr(param1));
                    break;
                case MediaEngineEvent.LoadedMetadata:
                    IsEndOfStream = false;
                    break;
                case MediaEngineEvent.CanPlay:
                    // Start the Playback
                    Play();
                    break;
                case MediaEngineEvent.Play:
                    IsPlaying = true;
                    break;
                case MediaEngineEvent.Pause:
                    IsPlaying = false;
                    break;
                case MediaEngineEvent.Ended:
                    IsEndOfStream = true;
                    if (Looping)
                    {
                        Play();
                    }
                    else
                    {
                        if (mediaEngineEx.HasVideo())
                        {
                            StopVideo();
                        }
                    }
                    break;
                case MediaEngineEvent.TimeUpdate:
                    break;
                case MediaEngineEvent.Error:
                    break;
            }
        }

        [DllImport("kernel32.dll", EntryPoint = "SetEvent")]
        private static extern bool SetEvent(IntPtr hEvent);
        
    }
}
