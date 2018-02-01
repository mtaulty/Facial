namespace FacialLibrary
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Windows.Devices.Enumeration;

    public class CameraDeviceFinder
    {
        Func<DeviceInformation, bool> deviceFilter;

        public CameraDeviceFinder(Func<DeviceInformation, bool> deviceFilter = null)
        {
            this.deviceFilter = deviceFilter;
        }
        public async Task<DeviceInformation> FindSingleCameraAsync()
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
            return (devices.SingleOrDefault());
        }
    }
}