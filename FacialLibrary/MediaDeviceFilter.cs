namespace FacialLibrary
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Windows.Devices.Enumeration;
    using Windows.Graphics.Imaging;
    using Windows.Media.Capture;
    using Windows.Media.Capture.Frames;

    public class MediaDeviceFilter
    {
        public MediaDeviceFilter(
            Func<DeviceInformation, bool> deviceFilter = null,
            Func<MediaFrameFormat, bool> formatFilter = null)
        {
            this.deviceFilter = deviceFilter;
            this.formatFilter = formatFilter;
        }
        public async Task<string> GetDeviceIdForFilterAsync()
        {
            var devices = await DeviceInformation.FindAllAsync(
                DeviceClass.VideoCapture) as IEnumerable<DeviceInformation>;

            if (deviceFilter != null)
            {
                devices = devices.Where(this.deviceFilter);
            }
            if (devices.Count() != 1)
            {
                throw new InvalidOperationException(
                    "Expected to find one camera or a non-null device filter");
            }
            return (devices.SingleOrDefault().Id);
        }
        public async Task<MediaFrameSource> GetMediaFrameSourceForFilterAsync(
            MediaCapture mediaCapture,
            IEnumerable<BitmapPixelFormat> pixelFormatHints)
        {
            MediaFrameSource frameSource = null;
            bool ignorePixelFormat = false;

            var colourVideoCandidates =
                mediaCapture.FrameSources.Values.Where(
                    fs => (fs.Info.MediaStreamType == MediaStreamType.VideoPreview) &&
                          (fs.Info.SourceKind == MediaFrameSourceKind.Color));

            var candidates = colourVideoCandidates.Where(
                fs => this.MatchesFormatFilterAndBitmapFormats(fs, pixelFormatHints));

            if (candidates?.Count() == 0)
            {
                ignorePixelFormat = true;

                candidates = colourVideoCandidates.Where(
                    fs => this.MatchesFormatFilter(fs));
            }

            if (candidates?.Count() > 0)
            {
                frameSource = candidates.First();

                var format = frameSource.SupportedFormats.First(
                    f =>
                        ((this.formatFilter == null) || (this.formatFilter(f))) &&
                        (ignorePixelFormat || this.MatchesPixelFormat(f, pixelFormatHints)));

                await frameSource.SetFormatAsync(format);
            }
            return (frameSource);
        }
        bool MatchesFormatFilterAndBitmapFormats(MediaFrameSource frameSource,
            IEnumerable<BitmapPixelFormat> formats)
        {
            return (
                this.MatchesFormatFilter(frameSource) &&
                frameSource.SupportedFormats.Any(
                    format => this.MatchesPixelFormat(format, formats))
            );
        }
        bool MatchesPixelFormat(MediaFrameFormat frameFormat, IEnumerable<BitmapPixelFormat> pixelFormatHints)
        {
            bool match = false;
            BitmapPixelFormat bmpFormat;

            if (Enum.TryParse<BitmapPixelFormat>(frameFormat.Subtype, true, out bmpFormat))
            {
                match = pixelFormatHints.Contains(bmpFormat);
            }
            return (match);
        }
        bool MatchesFormatFilter(MediaFrameSource frameSource)
        {
            return (
                ((this.formatFilter == null) ||
                 (frameSource.SupportedFormats.Any(f => this.formatFilter(f)))));      
        }
        Func<DeviceInformation, bool> deviceFilter;
        Func<MediaFrameFormat, bool> formatFilter;
    }
}