namespace FacialLibrary
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Windows.Graphics.Imaging;
    using Windows.Media.Capture;
    using Windows.Media.Capture.Frames;
    using Windows.Media.FaceAnalysis;

    public class FaceCountChangedEventArgs : EventArgs
    {
        internal FaceCountChangedEventArgs(int newCount)
        {
            this.FaceCount = newCount;
        }
        public int FaceCount { get; private set; }
    }
    public interface IFaceWatcher
    {
        event EventHandler<FaceCountChangedEventArgs> InFrameFaceCountChanged;
    }
    public class FaceWatcher : IFaceWatcher
    {
        public event EventHandler<FaceCountChangedEventArgs> InFrameFaceCountChanged;

        public FaceWatcher(CameraDeviceFinder deviceFinder,
            bool ignoreSyncContext = false)
        {
            if (!ignoreSyncContext)
            {
                this.syncContext = SynchronizationContext.Current;
            }
            this.deviceFinder = deviceFinder;
        }
        public async Task CaptureAsync(CancellationToken cancelToken)
        {
            if (this.capturing)
            {
                throw new InvalidOperationException("Already capturing");
            }
            this.capturing = true;

            try
            {
                // Which device are we wanting to pull frames from?
                var device = await this.deviceFinder.FindSingleCameraAsync();

                MediaCaptureInitializationSettings initialisationSettings = new MediaCaptureInitializationSettings()
                {
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                    VideoDeviceId = device.Id,
                    // This turns out to be more important than I thought if I want a SoftwareBitmap
                    // back on each frame
                    MemoryPreference = MediaCaptureMemoryPreference.Cpu
                };
                // Initialise the media capture
                using (var mediaCapture = new MediaCapture())
                {
                    await mediaCapture.InitializeAsync(initialisationSettings);

                    // Get a frame reader.
                    using (var frameReader = await mediaCapture.CreateFrameReaderAsync
                        (
                            mediaCapture.FrameSources.First(
                                fs =>
                                (
                                    (fs.Value.Info.DeviceInformation.Id == device.Id) &&
                                    (fs.Value.Info.MediaStreamType == MediaStreamType.VideoPreview) &&
                                    (fs.Value.Info.SourceKind == MediaFrameSourceKind.Color)
                                )
                            ).Value
                        )
                    )
                    {
                        var faceDetector = await FaceDetector.CreateAsync();
                        var faceDetectorFormat = FaceDetector.GetSupportedBitmapPixelFormats().First();
                        int handlingFrame = 0;
                        TimeSpan? lastFrameTime = null;
                        uint lastFaceCount = 0;

                        frameReader.FrameArrived += async (s, e) =>
                        {
                            if (Interlocked.CompareExchange(ref handlingFrame, 1, 0) == 0)
                            {
                                using (var frame = frameReader.TryAcquireLatestFrame())
                                {
                                    if (frame?.SystemRelativeTime != lastFrameTime)
                                    {
                                        lastFrameTime = frame.SystemRelativeTime;

                                        var originalBitmap = frame.VideoMediaFrame.SoftwareBitmap;
                                        var bitmapForDetection = originalBitmap;

                                        if (originalBitmap != null)
                                        {
                                            // Don't really need to call this every frame but...
                                            if (!FaceDetector.IsBitmapPixelFormatSupported(originalBitmap.BitmapPixelFormat))
                                            {
                                                bitmapForDetection = SoftwareBitmap.Convert(
                                                    originalBitmap, originalBitmap.BitmapPixelFormat);
                                            }
                                            // Run detection on this...
                                            var faceResults = await faceDetector.DetectFacesAsync(
                                                bitmapForDetection);

                                            if (faceResults?.Count() != lastFaceCount)
                                            {
                                                // We have work to do.
                                                this.DispatchFaceCountChanged(faceResults.Count);
                                                lastFaceCount = (uint)faceResults.Count();
                                            }
                                        }
                                        if (bitmapForDetection != originalBitmap)
                                        {
                                            bitmapForDetection.Dispose();
                                        }
                                    }
                                }
                                Interlocked.Exchange(ref handlingFrame, 0);
                            }
                        };
                        await frameReader.StartAsync();

                        await Task.Delay(-1, cancelToken);
                    }
                }
            }
            finally
            {
                this.capturing = false;
            }
        }
        void DispatchFaceCountChanged(int count)
        {
            if (this.syncContext != null)
            {
                this.syncContext.Post(
                    _ =>
                    {
                        this.FireFaceCountChanged(count);
                    },
                    null);
            }
            else
            {
                this.FireFaceCountChanged(count);
            }
        }
        void FireFaceCountChanged(int count)
        {
            this.InFrameFaceCountChanged?.Invoke(
                this, new FaceCountChangedEventArgs(count));
        }
        SynchronizationContext syncContext;
        CameraDeviceFinder deviceFinder;
        volatile bool capturing;
    }
}