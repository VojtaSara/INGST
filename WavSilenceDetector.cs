using System;
using System.IO;

namespace INGST
{
    public static class WavSilenceDetector
    {
        public static bool IsWavSilent(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"File not found: {filePath}");
                }

                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var reader = new BinaryReader(stream))
                {
                    // Read WAV header
                    var riffHeader = new string(reader.ReadChars(4));
                    if (riffHeader != "RIFF")
                    {
                        throw new InvalidDataException("Not a valid WAV file (missing RIFF header)");
                    }

                    // Skip file size
                    reader.ReadUInt32();

                    var waveHeader = new string(reader.ReadChars(4));
                    if (waveHeader != "WAVE")
                    {
                        throw new InvalidDataException("Not a valid WAV file (missing WAVE header)");
                    }

                    // Find the data chunk
                    while (stream.Position < stream.Length - 8)
                    {
                        var chunkId = new string(reader.ReadChars(4));
                        var chunkSize = reader.ReadUInt32();

                        if (chunkId == "data")
                        {
                            // Found the data chunk, now analyze the audio samples
                            return AnalyzeAudioData(reader, chunkSize);
                        }
                        else
                        {
                            // Skip to next chunk
                            stream.Position += chunkSize;
                        }
                    }

                    throw new InvalidDataException("No data chunk found in WAV file");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error analyzing WAV file: {ex.Message}");
                return false;
            }
        }

        private static bool AnalyzeAudioData(BinaryReader reader, uint dataSize)
        {
            const double silenceThreshold = 0.001; // Very small threshold for "silence"
            var sampleCount = dataSize / 2; // Assuming 16-bit samples

            // Check first 1000 samples and last 1000 samples
            var samplesToCheck = Math.Min(1000, sampleCount);
            
            // Check beginning of file
            for (int i = 0; i < samplesToCheck; i++)
            {
                if (reader.BaseStream.Position >= reader.BaseStream.Length - 2)
                    break;

                var sample = reader.ReadInt16();
                var normalizedSample = Math.Abs(sample) / 32768.0; // Normalize to 0-1 range
                
                if (normalizedSample > silenceThreshold)
                {
                    return false; // Found non-silent audio
                }
            }

            // Check end of file
            var endPosition = reader.BaseStream.Position;
            var startPosition = reader.BaseStream.Length - (samplesToCheck * 2);
            if (startPosition > endPosition)
            {
                reader.BaseStream.Position = startPosition;
                
                for (int i = 0; i < samplesToCheck; i++)
                {
                    if (reader.BaseStream.Position >= reader.BaseStream.Length - 2)
                        break;

                    var sample = reader.ReadInt16();
                    var normalizedSample = Math.Abs(sample) / 32768.0; // Normalize to 0-1 range
                    
                    if (normalizedSample > silenceThreshold)
                    {
                        return false; // Found non-silent audio
                    }
                }
            }

            // If we get here, the samples we checked were all silent
            return true;
        }
    }
} 