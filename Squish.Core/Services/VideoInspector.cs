using System.Diagnostics;
using System.Text.Json;
using Squish.Core.Abstractions;

namespace Squish.Core.Services;

public class VideoInspector : IVideoInspector
{
    public async Task<string> GetVideoCodecAsync(string filePath)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "ffprobe",
            Arguments = $"-v quiet -print_format json -show_streams -select_streams v:0 \"{filePath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo);
        if (process == null)
            throw new InvalidOperationException("Failed to start ffprobe process");

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"ffprobe failed with exit code {process.ExitCode}: {error}");

        try
        {
            using var jsonDoc = JsonDocument.Parse(output);
            var streams = jsonDoc.RootElement.GetProperty("streams");
            
            if (streams.GetArrayLength() > 0)
            {
                var videoStream = streams[0];
                if (videoStream.TryGetProperty("codec_name", out var codecElement))
                {
                    return codecElement.GetString() ?? "unknown";
                }
            }
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Failed to parse ffprobe output");
        }

        return "unknown";
    }
}