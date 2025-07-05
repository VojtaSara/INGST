using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Diagnostics;

namespace INGST
{
    public class ThumbnailService
    {
        private static readonly string ThumbnailCacheFolder = Path.Combine(Path.GetTempPath(), "INGST_Thumbnails");
        
        static ThumbnailService()
        {
            // Ensure thumbnail cache folder exists
            if (!Directory.Exists(ThumbnailCacheFolder))
            {
                Directory.CreateDirectory(ThumbnailCacheFolder);
            }
        }

        private static void Log(string message)
        {
            string logPath = Path.Combine(ThumbnailCacheFolder, "thumbnail_debug.log");
            File.AppendAllText(logPath, DateTime.Now + ": " + message + Environment.NewLine);
            System.Diagnostics.Debug.WriteLine(message);
        }

        public static async Task<ImageSource?> ExtractFrameAsync(string videoPath, TimeSpan position)
        {
            try
            {
                Log($"=== Starting thumbnail extraction ===");
                Log($"Video path: {videoPath}");
                Log($"Position: {position}");

                // Create a unique filename based on the video path and position
                string videoFileName = Path.GetFileNameWithoutExtension(videoPath);
                string positionStr = position == TimeSpan.Zero ? "first" : $"{position.TotalSeconds:F0}s";
                string thumbnailFileName = $"{videoFileName}_{positionStr}.jpg";
                string thumbnailPath = Path.Combine(ThumbnailCacheFolder, thumbnailFileName);
                
                Log($"Video filename: {videoFileName}");
                Log($"Position string: {positionStr}");
                Log($"Thumbnail filename: {thumbnailFileName}");
                Log($"Full thumbnail path: {thumbnailPath}");

                // Check if thumbnail already exists
                if (File.Exists(thumbnailPath))
                {
                    Log($"Using cached thumbnail: {thumbnailPath}");
                    return LoadImageFromPath(thumbnailPath);
                }
                else
                {
                    Log($"Thumbnail does not exist, will create: {thumbnailPath}");
                }

                // Use first frame (0 seconds) instead of middle frame
                TimeSpan extractPosition = position;
                if (position == TimeSpan.Zero)
                {
                    extractPosition = TimeSpan.Zero; // Use first frame
                    Log($"Using first frame position: {extractPosition}");
                }

                // Build the FFmpeg command
                string ffmpegArgs = $"-ss {extractPosition:hh\\:mm\\:ss} -i \"{videoPath}\" -frames:v 1 -q:v 2 -y \"{thumbnailPath}\"";
                Log($"FFmpeg command: ffmpeg {ffmpegArgs}");

                // Run FFmpeg as a subprocess
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        Arguments = ffmpegArgs,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                Log("Starting FFmpeg process...");
                try
                {
                    process.Start();
                }
                catch (Exception ex)
                {
                    Log($"ERROR: Could not start FFmpeg process: {ex.Message}");
                    return null;
                }

                // Read output and error in parallel
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                // Wait for process exit or timeout
                bool exited = await Task.Run(() => process.WaitForExit(10000)); // 10s timeout
                if (!exited)
                {
                    try { process.Kill(); } catch { }
                    Log($"ERROR: FFmpeg process timed out after 10 seconds");
                    return null;
                }

                string output = await outputTask;
                string error = await errorTask;

                Log($"FFmpeg process completed with exit code: {process.ExitCode}");
                if (!string.IsNullOrEmpty(output))
                    Log($"[FFmpeg OUT] {output}");
                if (!string.IsNullOrEmpty(error))
                    Log($"[FFmpeg ERR] {error}");

                if (process.ExitCode == 0 && File.Exists(thumbnailPath))
                {
                    Log($"Frame extracted successfully: {thumbnailPath}");
                    Log($"File size: {new FileInfo(thumbnailPath).Length} bytes");
                    return LoadImageFromPath(thumbnailPath);
                }
                else
                {
                    Log($"ERROR: FFmpeg failed with exit code {process.ExitCode}");
                    Log($"Thumbnail file exists: {File.Exists(thumbnailPath)}");
                    if (File.Exists(thumbnailPath))
                    {
                        Log($"File size: {new FileInfo(thumbnailPath).Length} bytes");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"ERROR: Frame extraction failed: {ex.Message}");
                Log($"Stack trace: {ex.StackTrace}");
            }
            Log("Returning null due to extraction failure");
            return null;
        }

        private static ImageSource? LoadImageFromPath(string imagePath)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(imagePath);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load image from {imagePath}: {ex.Message}");
                return null;
            }
        }

        public static async Task<ImageSource?> ExtractFirstFrameAsync(string videoPath)
        {
            return await ExtractFrameAsync(videoPath, TimeSpan.Zero); // Zero means extract first frame
        }

        public static ImageSource CreateLoadingText()
        {
            // Create a simple "LOADING" text image
            var drawingVisual = new System.Windows.Media.DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                // Background
                drawingContext.DrawRectangle(System.Windows.Media.Brushes.DarkGray, null, new System.Windows.Rect(0, 0, 400, 300));
                
                // Create formatted text for "LOADING"
                var formattedText = new FormattedText(
                    "LOADING",
                    System.Globalization.CultureInfo.CurrentCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    48,
                    System.Windows.Media.Brushes.White,
                    VisualTreeHelper.GetDpi(drawingVisual).PixelsPerDip);
                
                // Center the text
                double x = (400 - formattedText.Width) / 2;
                double y = (300 - formattedText.Height) / 2;
                
                drawingContext.DrawText(formattedText, new System.Windows.Point(x, y));
            }
            
            var bmp = new RenderTargetBitmap(400, 300, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(drawingVisual);
            return bmp;
        }

        public static ImageSource CreateAudioText()
        {
            // Create a simple "AUDIO" text image
            var drawingVisual = new System.Windows.Media.DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                // Background
                drawingContext.DrawRectangle(System.Windows.Media.Brushes.DarkGray, null, new System.Windows.Rect(0, 0, 400, 300));
                
                // Create formatted text for "AUDIO"
                var formattedText = new FormattedText(
                    "AUDIO",
                    System.Globalization.CultureInfo.CurrentCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    48,
                    System.Windows.Media.Brushes.White,
                    VisualTreeHelper.GetDpi(drawingVisual).PixelsPerDip);
                
                // Center the text
                double x = (400 - formattedText.Width) / 2;
                double y = (300 - formattedText.Height) / 2;
                
                drawingContext.DrawText(formattedText, new System.Windows.Point(x, y));
            }
            
            var bmp = new RenderTargetBitmap(400, 300, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(drawingVisual);
            return bmp;
        }

        // Method to clear thumbnail cache
        public static void ClearThumbnailCache()
        {
            try
            {
                if (Directory.Exists(ThumbnailCacheFolder))
                {
                    Directory.Delete(ThumbnailCacheFolder, true);
                    Directory.CreateDirectory(ThumbnailCacheFolder);
                    System.Diagnostics.Debug.WriteLine("Thumbnail cache cleared");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clear thumbnail cache: {ex.Message}");
            }
        }

        // Method to force quit all FFmpeg processes
        public static void ForceQuitAllFFmpegProcesses()
        {
            try
            {
                var ffmpegProcesses = System.Diagnostics.Process.GetProcessesByName("ffmpeg");
                var ffprobeProcesses = System.Diagnostics.Process.GetProcessesByName("ffprobe");
                
                foreach (var process in ffmpegProcesses.Concat(ffprobeProcesses))
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            process.WaitForExit(1000); // Wait up to 1 second
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to kill FFmpeg process {process.ProcessName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ForceQuitAllFFmpegProcesses: {ex.Message}");
            }
        }
    }
} 