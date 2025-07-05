using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Media = System.Windows.Media;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;
using System.ComponentModel;

namespace INGST
{
    public class CameraSection
    {
        public string Name { get; set; } = string.Empty;
        public List<IngestFile> Files { get; set; } = new();
        public string PathLabel { get; set; } = "No files selected";
    }

    public partial class MainWindow : Window
    {
        private List<CameraSection> cameras = new();
        private List<IngestFile> audioFiles = new();
        private string destRootPath = "";
        
        // Copy progress tracking
        private CancellationTokenSource? copyCancellationTokenSource;
        private bool isCopyPaused = false;
        private long totalBytesToCopy = 0;
        private long totalBytesCopied = 0;
        private int totalFilesToCopy = 0;
        private int totalFilesCopied = 0;
        private DateTime copyStartTime;
        private List<string> copiedFiles = new();
        private List<string> unfinishedFiles = new();

        public MainWindow()
        {
            InitializeComponent();
            InitializeCameras();
            AudioListView.ItemsSource = audioFiles;
            CameraItemsControl.ItemsSource = cameras;
            
            // Add selection changed events for large preview
            AudioListView.SelectionChanged += ListView_SelectionChanged;

            // Try to find ffmpeg in PATH and set FFmpeg executables path
            TrySetFFmpegFromPath();
            
            // Handle window closing to force quit everything
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Cancel any ongoing copy operations
            copyCancellationTokenSource?.Cancel();
            
            // Force quit all background processes
            ForceQuitAllProcesses();
            
            // Kill any remaining INGST processes
            KillIngestProcesses();
        }

        private void ForceQuitAllProcesses()
        {
            try
            {
                // Use ThumbnailService to kill FFmpeg processes
                ThumbnailService.ForceQuitAllFFmpegProcesses();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ForceQuitAllProcesses: {ex.Message}");
            }
        }

        private void KillIngestProcesses()
        {
            try
            {
                var ingestProcesses = System.Diagnostics.Process.GetProcessesByName("INGST");
                foreach (var process in ingestProcesses)
                {
                    try
                    {
                        if (process.Id != System.Diagnostics.Process.GetCurrentProcess().Id && !process.HasExited)
                        {
                            process.Kill();
                            process.WaitForExit(1000);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to kill INGST process {process.Id}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in KillIngestProcesses: {ex.Message}");
            }
        }

        private void TrySetFFmpegFromPath()
        {
            string? ffmpegDir = FindExecutableInPath("ffmpeg.exe");
            if (!string.IsNullOrEmpty(ffmpegDir))
            {
                System.Diagnostics.Debug.WriteLine($"FFmpeg found at: {ffmpegDir}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("FFmpeg not found in PATH!");
                System.Windows.MessageBox.Show("FFmpeg not found in PATH! Please install FFmpeg and ensure ffmpeg.exe is available.");
            }
        }

        private string? FindExecutableInPath(string exeName)
        {
            var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
            foreach (var path in paths)
            {
                try
                {
                    var exePath = Path.Combine(path.Trim(), exeName);
                    if (File.Exists(exePath))
                    {
                        return path.Trim();
                    }
                }
                catch { }
            }
            return null;
        }

        private void ListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var listView = sender as System.Windows.Controls.ListView;
            var selectedItem = listView?.SelectedItem as IngestFile;
            
            if (selectedItem != null)
            {
                // Show loading text immediately
                LargePreviewImage.Source = ThumbnailService.CreateLoadingText();
                PreviewInfoText.Text = $"Loading frame from: {selectedItem.DisplayName}";
                
                // Load large preview in background
                _ = LoadLargePreviewAsync(selectedItem);
            }
        }

        private void CameraListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var listView = sender as System.Windows.Controls.ListView;
            var selectedItem = listView?.SelectedItem as IngestFile;
            
            if (selectedItem != null)
            {
                // Show loading text immediately
                LargePreviewImage.Source = ThumbnailService.CreateLoadingText();
                PreviewInfoText.Text = $"Loading frame from: {selectedItem.DisplayName}";
                
                // Load large preview in background
                _ = LoadLargePreviewAsync(selectedItem);
            }
        }

        private async Task LoadLargePreviewAsync(IngestFile file)
        {
            try
            {
                if (file.Type == "video")
                {
                    var largePreview = await ThumbnailService.ExtractFirstFrameAsync(file.Path);
                    if (largePreview != null)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            file.LargePreview = largePreview;
                            LargePreviewImage.Source = largePreview;
                            PreviewInfoText.Text = $"Frame from: {file.DisplayName}";
                        });
                    }
                }
                else
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        file.LargePreview = ThumbnailService.CreateLoadingText();
                        LargePreviewImage.Source = file.LargePreview;
                        PreviewInfoText.Text = $"Audio: {file.DisplayName}";
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Large preview loading failed: {ex.Message}");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    LargePreviewImage.Source = ThumbnailService.CreateLoadingText();
                    PreviewInfoText.Text = $"Error loading frame from: {file.DisplayName}";
                });
            }
        }

        private void InitializeCameras()
        {
            cameras.Clear();
            cameras.Add(new CameraSection { Name = "Camera 1" });
            cameras.Add(new CameraSection { Name = "Camera 2" });
            CameraItemsControl.ItemsSource = null;
            CameraItemsControl.ItemsSource = cameras;
        }

        private void AddCamera(object sender, RoutedEventArgs e)
        {
            cameras.Add(new CameraSection { Name = $"Camera {cameras.Count + 1}" });
            CameraItemsControl.ItemsSource = null;
            CameraItemsControl.ItemsSource = cameras;
        }

        private void RemoveCamera(object sender, RoutedEventArgs e)
        {
            if (cameras.Count > 1)
            {
                cameras.RemoveAt(cameras.Count - 1);
                CameraItemsControl.ItemsSource = null;
                CameraItemsControl.ItemsSource = cameras;
            }
        }

        // Called from IMPORT button in each camera section
        public void ImportCameraFiles(int cameraIndex)
        {
            if (cameraIndex < 0 || cameraIndex >= cameras.Count) return;
            var files = PickFiles("video");
            cameras[cameraIndex].Files = files;
            cameras[cameraIndex].PathLabel = $"{files.Count} file(s) selected - Total Duration: {GetTotalDuration(files)}";
            CameraItemsControl.ItemsSource = null;
            CameraItemsControl.ItemsSource = cameras;
        }

        // This method is used by the XAML binding for the IMPORT button
        public void ImportCameraFilesFromButton(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button?.DataContext is CameraSection cam)
            {
                int idx = cameras.IndexOf(cam);
                ImportCameraFiles(idx);
            }
        }

        private void SelectAudioFiles(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog
            {
                Description = "Select folder containing audio subfolders (each with MIC1.wav, MIC2.wav, etc.)"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                audioFiles = LoadAudioFilesFromFolders(new[] { dialog.SelectedPath });
                AudioListView.ItemsSource = audioFiles;
                AudioPathLabel.Text = $"{audioFiles.Count} audio file(s) imported - Total Duration: {GetTotalDuration(audioFiles)}";
            }
        }

        private List<IngestFile> LoadAudioFilesFromFolders(string[] rootPaths)
        {
            var results = new List<IngestFile>();
            
            try
            {
                foreach (var rootPath in rootPaths)
                {
                    // Get all subfolders in the selected folder
                    var subfolders = Directory.GetDirectories(rootPath);
                    
                    foreach (var subfolder in subfolders)
                    {
                        string parentFolderName = Path.GetFileName(subfolder);
                        
                        // Look for MIC1.wav through MIC4.wav and SOUND_PAD.wav
                        string[] targetFiles = { "MIC1.wav", "MIC2.wav", "MIC3.wav", "MIC4.wav", "SOUND_PAD.wav" };
                        
                        foreach (var targetFile in targetFiles)
                        {
                            string filePath = Path.Combine(subfolder, targetFile);
                            
                            if (File.Exists(filePath))
                            {
                                // Check if the file is not silent
                                if (!WavSilenceDetector.IsWavSilent(filePath))
                                {
                                    // Create new filename with parent folder name
                                    string micNumber = Path.GetFileNameWithoutExtension(targetFile);
                                    string newFileName = $"{parentFolderName}_{micNumber}.wav";
                                    
                                    var ingestFile = new IngestFile
                                    {
                                        Path = filePath,
                                        Type = "audio",
                                        SizeGB = new FileInfo(filePath).Length / 1_073_741_824.0,
                                        Duration = GetMediaDuration(filePath),
                                        CustomDisplayName = newFileName, // Use the new filename for display
                                        Thumbnail = ThumbnailService.CreateAudioText()
                                    };
                                    
                                    results.Add(ingestFile);
                                    
                                    // Load thumbnail in the background
                                    _ = LoadThumbnailAsync(ingestFile);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading audio files: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            
            return results;
        }

        private void SelectDestination(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();

            var result = dialog.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                destRootPath = dialog.SelectedPath;
                DestPathLabel.Text = destRootPath;
            }
        }

        private async void SubmitProject(object sender, RoutedEventArgs e)
        {
            string projectCode = ProjectCodeBox.Text.Trim();
            string projectName = ProjectNameBox.Text.Trim();
            DateTime? recordingDate = RecordingDatePicker.SelectedDate;

            if (string.IsNullOrEmpty(projectCode) || string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(destRootPath))
            {
                System.Windows.MessageBox.Show("Fill in Project Code, Name, and Destination folder.");
                return;
            }

            // Initialize copy progress tracking
            copyCancellationTokenSource = new CancellationTokenSource();
            isCopyPaused = false;
            copiedFiles.Clear();
            unfinishedFiles.Clear();
            
            // Calculate totals
            totalFilesToCopy = 0;
            totalBytesToCopy = 0;
            foreach (var cam in cameras)
            {
                totalFilesToCopy += cam.Files.Count;
                foreach (var file in cam.Files)
                {
                    totalBytesToCopy += new FileInfo(file.Path).Length;
                }
            }
            totalFilesToCopy += audioFiles.Count;
            foreach (var file in audioFiles)
            {
                totalBytesToCopy += new FileInfo(file.Path).Length;
            }

            if (totalFilesToCopy == 0)
            {
                System.Windows.MessageBox.Show("No files to copy!");
                return;
            }

            // Show progress overlay
            CopyProgressOverlay.Visibility = Visibility.Visible;
            BuildProjectButton.IsEnabled = false;
            
            // Reset progress
            totalBytesCopied = 0;
            totalFilesCopied = 0;
            copyStartTime = DateTime.Now;
            
            UpdateProgressUI();

            try
            {
                string fullProjectPath = Path.Combine(destRootPath, $"{projectCode} {projectName}");

                // Create folders for all cameras
                foreach (var cam in cameras)
                {
                    Directory.CreateDirectory(Path.Combine(fullProjectPath, $"NM\\{cam.Name.ToLower().Replace(" ", "_")}"));
                }
                Directory.CreateDirectory(Path.Combine(fullProjectPath, "RAW zvuk"));

                // Copy camera files with progress
                foreach (var cam in cameras)
                {
                    string camFolder = Path.Combine(fullProjectPath, $"NM\\{cam.Name.ToLower().Replace(" ", "_")}");
                    foreach (var file in cam.Files)
                    {
                        if (copyCancellationTokenSource.Token.IsCancellationRequested)
                            break;

                        // Create new filename with project code and camera name prefix
                        string originalFileName = Path.GetFileName(file.Path);
                        string fileExtension = Path.GetExtension(file.Path);
                        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(file.Path);
                        string cameraName = cam.Name.ToLower().Replace(" ", "_");
                        string newFileName = $"{projectCode}_{cameraName}_{fileNameWithoutExt}{fileExtension}";
                        
                        string destPath = Path.Combine(camFolder, newFileName);
                        await CopyFileWithProgressAsync(file, destPath, copyCancellationTokenSource.Token);
                        
                        if (!copyCancellationTokenSource.Token.IsCancellationRequested)
                        {
                            copiedFiles.Add(destPath);
                            totalFilesCopied++;
                        }
                    }
                }

                // Copy audio files with progress
                foreach (var file in audioFiles)
                {
                    if (copyCancellationTokenSource.Token.IsCancellationRequested)
                        break;

                    // Create new filename with project code prefix for audio files too
                    string originalFileName = file.DestinationFileName;
                    string fileExtension = Path.GetExtension(originalFileName);
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);
                    string newFileName = $"{projectCode}_{fileNameWithoutExt}{fileExtension}";
                    
                    string destPath = Path.Combine(fullProjectPath, "RAW zvuk", newFileName);
                    await CopyFileWithProgressAsync(file, destPath, copyCancellationTokenSource.Token);
                    
                    if (!copyCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        copiedFiles.Add(destPath);
                        totalFilesCopied++;
                    }
                }

                if (!copyCancellationTokenSource.Token.IsCancellationRequested)
                {
                    string[] folders = {
                        "grafika",
                        "render",
                        "vystrizky",
                        "podklady",
                        "fixing",
                        "foto",
                        "projekt"
                    };

                    foreach (string folder in folders)
                    {
                        Directory.CreateDirectory(Path.Combine(fullProjectPath, folder));
                    }

                    System.Windows.MessageBox.Show($"Project setup complete!\n\nFiles copied: {totalFilesCopied}\nTotal video: {GetTotalSizeGB(cameras.SelectMany(c => c.Files)):F2} GB\nTotal audio: {GetTotalSizeGB(audioFiles):F2} GB", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (OperationCanceledException)
            {
                // Clean up unfinished files
                CleanupUnfinishedFiles();
                System.Windows.MessageBox.Show("Copy operation was cancelled. Unfinished files have been cleaned up.", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                // Clean up unfinished files
                CleanupUnfinishedFiles();
                System.Windows.MessageBox.Show($"Error during copy operation: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Hide progress overlay
                CopyProgressOverlay.Visibility = Visibility.Collapsed;
                BuildProjectButton.IsEnabled = true;
                copyCancellationTokenSource?.Dispose();
                copyCancellationTokenSource = null;
            }
        }

        private void CleanupUnfinishedFiles()
        {
            foreach (var file in unfinishedFiles)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to delete unfinished file {file}: {ex.Message}");
                }
            }
            unfinishedFiles.Clear();
        }

        private void PauseCopy(object sender, RoutedEventArgs e)
        {
            isCopyPaused = !isCopyPaused;
            PauseButton.Content = isCopyPaused ? "RESUME" : "PAUSE";
            PauseButton.Background = isCopyPaused ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2d, 0x5a, 0x2d)) : new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x5a, 0x4a, 0x2d));
            StatusText.Text = isCopyPaused ? "Copy paused..." : "Copying...";
        }

        private void StopCopy(object sender, RoutedEventArgs e)
        {
            copyCancellationTokenSource?.Cancel();
            StatusText.Text = "Stopping copy operation...";
        }

        private void UpdateProgressUI()
        {
            if (totalBytesToCopy > 0)
            {
                double overallProgress = (double)totalBytesCopied / totalBytesToCopy * 100;
                OverallProgressBar.Value = overallProgress;
                OverallProgressText.Text = $"{overallProgress:F1}%";
                
                // Update stats
                double totalGB = totalBytesToCopy / 1_073_741_824.0;
                double copiedGB = totalBytesCopied / 1_073_741_824.0;
                StatsText.Text = $"Files: {totalFilesCopied}/{totalFilesToCopy} | Size: {copiedGB:F2} GB / {totalGB:F2} GB";
                
                // Calculate speed and time remaining
                var elapsed = DateTime.Now - copyStartTime;
                if (elapsed.TotalSeconds > 0)
                {
                    double bytesPerSecond = totalBytesCopied / elapsed.TotalSeconds;
                    string speedText = bytesPerSecond > 1_073_741_824 ? 
                        $"{bytesPerSecond / 1_073_741_824:F1} GB/s" : 
                        $"{bytesPerSecond / 1_048_576:F1} MB/s";
                    SpeedText.Text = $"Speed: {speedText}";
                    
                    if (bytesPerSecond > 0)
                    {
                        long remainingBytes = totalBytesToCopy - totalBytesCopied;
                        double remainingSeconds = remainingBytes / bytesPerSecond;
                        var remainingTime = TimeSpan.FromSeconds(remainingSeconds);
                        TimeRemainingText.Text = $"Time remaining: {remainingTime:hh\\:mm\\:ss}";
                    }
                }
            }
        }

        private async Task CopyFileWithProgressAsync(IngestFile file, string destPath, CancellationToken cancellationToken)
        {
            try
            {
                // Add to unfinished files list
                unfinishedFiles.Add(destPath);
                
                // Update UI for current file
                await Dispatcher.InvokeAsync(() =>
                {
                    string displayName = file.DisplayName;
                    if (file.Type == "video")
                    {
                        // For video files, show the new filename that will be created
                        string projectCode = ProjectCodeBox.Text.Trim();
                        string cameraName = GetCameraNameForFile(file);
                        if (!string.IsNullOrEmpty(cameraName))
                        {
                            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(file.Path);
                            string fileExtension = Path.GetExtension(file.Path);
                            displayName = $"{projectCode}_{cameraName}_{fileNameWithoutExt}{fileExtension}";
                        }
                    }
                    else if (file.Type == "audio")
                    {
                        // For audio files, show the new filename that will be created
                        string projectCode = ProjectCodeBox.Text.Trim();
                        string originalFileName = file.DestinationFileName;
                        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);
                        string fileExtension = Path.GetExtension(originalFileName);
                        displayName = $"{projectCode}_{fileNameWithoutExt}{fileExtension}";
                    }
                    
                    CurrentFileText.Text = $"Copying: {displayName}";
                    FileProgressBar.Value = 0;
                    FileProgressText.Text = "0%";
                    StatusText.Text = "Copying...";
                });

                const int bufferSize = 1024 * 1024; // 1MB buffer for fast copying
                using (var sourceStream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize))
                using (var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize))
                {
                    long totalBytes = sourceStream.Length;
                    long totalRead = 0;
                    byte[] buffer = new byte[bufferSize];
                    int read;
                    
                    while ((read = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        // Check for pause
                        while (isCopyPaused && !cancellationToken.IsCancellationRequested)
                        {
                            await Task.Delay(100, cancellationToken);
                        }
                        
                        if (cancellationToken.IsCancellationRequested)
                            break;
                        
                        await destStream.WriteAsync(buffer, 0, read, cancellationToken);
                        totalRead += read;
                        totalBytesCopied += read;
                        
                        // Update file progress
                        int fileProgress = (int)(100.0 * totalRead / totalBytes);
                        await Dispatcher.InvokeAsync(() =>
                        {
                            file.CopyProgress = fileProgress;
                            FileProgressBar.Value = fileProgress;
                            FileProgressText.Text = $"{fileProgress}%";
                            UpdateProgressUI();
                        });
                    }
                }
                
                // Remove from unfinished files if successful
                unfinishedFiles.Remove(destPath);
                
                await Dispatcher.InvokeAsync(() =>
                {
                    file.CopyProgress = 100;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to copy {file.Path}: {ex.Message}");
                throw;
            }
        }

        private List<IngestFile> PickFiles(string type)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Media Files|*.mp4;*.mov;*.wav;*.mp3|All Files|*.*",
                Multiselect = true
            };

            var results = new List<IngestFile>();

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    var ingestFile = new IngestFile
                    {
                        Path = file,
                        Type = GetMediaType(file),
                        SizeGB = new FileInfo(file).Length / 1_073_741_824.0,
                        Duration = GetMediaDuration(file),
                        Thumbnail = ThumbnailService.CreateLoadingText() // Show loading text initially
                    };
                    results.Add(ingestFile);

                    // Load thumbnail in the background
                    _ = LoadThumbnailAsync(ingestFile);
                }
            }

            return results;
        }

        private string GetMediaType(string file)
        {
            string ext = System.IO.Path.GetExtension(file).ToLower();
            if (ext == ".wav" || ext == ".mp3") return "audio";
            return "video";
        }

        private TimeSpan GetMediaDuration(string file)
        {
            try
            {
                string ext = Path.GetExtension(file).ToLower();
                
                // For WAV files, use FFmpeg to get duration more reliably
                if (ext == ".wav")
                {
                    return GetWavDurationWithFFmpeg(file);
                }
                
                // For other files, use MediaPlayer
                var player = new MediaPlayer();
                player.Open(new Uri(file));
                
                // Wait for media to open with timeout
                var startTime = DateTime.Now;
                while (!player.NaturalDuration.HasTimeSpan && (DateTime.Now - startTime).TotalSeconds < 5)
                {
                    System.Threading.Thread.Sleep(50);
                }
                
                return player.NaturalDuration.HasTimeSpan ? player.NaturalDuration.TimeSpan : TimeSpan.Zero;
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        private TimeSpan GetWavDurationWithFFmpeg(string filePath)
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "ffprobe",
                        Arguments = $"-v quiet -show_entries format=duration -of csv=p=0 \"{filePath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                
                // Wait for process with timeout to prevent hanging
                if (!process.WaitForExit(5000)) // 5 second timeout
                {
                    try { process.Kill(); } catch { }
                    System.Diagnostics.Debug.WriteLine($"FFprobe timed out for {filePath}");
                    return TimeSpan.Zero;
                }
                
                string output = process.StandardOutput.ReadToEnd();

                if (process.ExitCode == 0 && double.TryParse(output.Trim(), out double seconds))
                {
                    return TimeSpan.FromSeconds(seconds);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FFprobe failed for {filePath}: {ex.Message}");
            }
            
            return TimeSpan.Zero;
        }

        private async Task LoadThumbnailAsync(IngestFile ingestFile)
        {
            var thumb = await GetMediaThumbnailAsync(ingestFile.Path);
            System.Windows.Application.Current.Dispatcher.Invoke(() => {
                ingestFile.Thumbnail = thumb;
                // Property change notification will automatically update the UI
            });
        }

        public async Task<ImageSource?> GetMediaThumbnailAsync(string file)
        {
            string ext = System.IO.Path.GetExtension(file).ToLower();
            if (ext == ".wav" || ext == ".mp3")
            {
                return ThumbnailService.CreateAudioText();
            }
            
            // Use the new ThumbnailService for video files
            return await ThumbnailService.ExtractFirstFrameAsync(file);
        }

        private string GetTotalDuration(List<IngestFile> files)
        {
            var totalDuration = TimeSpan.Zero;
            foreach (var file in files)
            {
                totalDuration += file.Duration;
            }
            return totalDuration.ToString(@"hh\:mm\:ss");
        }

        private double GetTotalSizeGB(IEnumerable<IngestFile> files)
        {
            return files.Sum(f => f.SizeGB);
        }

        private string GetCameraNameForFile(IngestFile file)
        {
            // Find which camera this file belongs to
            foreach (var cam in cameras)
            {
                if (cam.Files.Contains(file))
                {
                    return cam.Name.ToLower().Replace(" ", "_");
                }
            }
            return string.Empty;
        }
    }
}
