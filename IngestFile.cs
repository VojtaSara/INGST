using System;
using System.Windows.Media;
using System.ComponentModel;

namespace INGST
{
    public class IngestFile : INotifyPropertyChanged
    {
        public string Path { get; set; } = "";
        public string Type { get; set; } = ""; // "video" or "audio"
        public TimeSpan Duration { get; set; }
        public double SizeGB { get; set; }
        private ImageSource? _thumbnail;
        public ImageSource? Thumbnail 
        { 
            get => _thumbnail;
            set 
            { 
                _thumbnail = value; 
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail))); 
            }
        }
        
        private ImageSource? _largePreview;
        public ImageSource? LargePreview 
        { 
            get => _largePreview;
            set 
            { 
                _largePreview = value; 
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LargePreview))); 
            }
        } // For the big frame viewer
        public string? CustomDisplayName { get; set; } // Custom display name for renamed files
        public string DisplayName => CustomDisplayName ?? System.IO.Path.GetFileName(Path);
        public string DestinationFileName => CustomDisplayName ?? System.IO.Path.GetFileName(Path); // Filename to use when copying
        public string DurationFormatted => Duration.ToString(@"hh\:mm\:ss");

        private int _copyProgress;
        public int CopyProgress
        {
            get => _copyProgress;
            set { _copyProgress = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CopyProgress))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
