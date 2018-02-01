namespace FacialTestApp
{
    using FacialLibrary;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Windows.UI.Xaml.Controls;

    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public MainPage()
        {
            this.InitializeComponent();
            this.CanStart = true;

            this.faceWatcher = new FaceWatcher(
                new CameraDeviceFinder(
                    deviceInformation =>
                    {
                        return (deviceInformation.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front);
                    }
                )
            );
        }
        public int FaceCount
        {
            get => faceCount;
            set
            {
                if (value != this.faceCount)
                {
                    this.faceCount = value;
                    this.FirePropertyChanged();
                }
            }
        }
        public bool CanStart
        {
            get => canStart;
            set
            {
                if (value != this.canStart)
                {
                    this.canStart = value;
                    this.FirePropertyChanged();
                    this.FirePropertyChanged("CanStop");
                }
            }
        }
        public bool CanStop
        {
            get
            {
                return (!this.CanStart);
            }
        }

        async void OnStart()
        {
            this.CanStart = false;

            this.cancelTokenSource = new CancellationTokenSource();

            this.faceWatcher.InFrameFaceCountChanged += OnFaceCountChanged;

            try
            {
                await this.faceWatcher.CaptureAsync(this.cancelTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                this.faceWatcher.InFrameFaceCountChanged -= this.OnFaceCountChanged;
                this.CanStart = true;
            }
        }
        void OnFaceCountChanged(object sender, FaceCountChangedEventArgs e)
        {
            this.FaceCount = e.FaceCount;
        }
        void OnStop()
        {
            this.cancelTokenSource.Cancel();
        }
        void FirePropertyChanged([CallerMemberName] string callerName = null)
        {
            this.PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(callerName));
        }
        CancellationTokenSource cancelTokenSource;
        FaceWatcher faceWatcher;
        bool canStart;
        int faceCount;
    }
}