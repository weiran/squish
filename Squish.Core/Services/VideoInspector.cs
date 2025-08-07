using System.Text.Json;
using Squish.Core.Abstractions;

namespace Squish.Core.Services;

public class VideoInspector : IVideoInspector
{
    private readonly IProcessWrapper _processWrapper;

    public VideoInspector(IProcessWrapper processWrapper)
    {
        _processWrapper = processWrapper ?? throw new ArgumentNullException(nameof(processWrapper));
    }

    public async Task<string> GetVideoCodecAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        var arguments = $"-v quiet -print_format json -show_streams -select_streams v:0 \"{filePath}\"";
        var result = await _processWrapper.RunAsync("ffprobe", arguments);

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"ffprobe failed with exit code {result.ExitCode}: {result.StandardError}");

        return ParseCodecFromJson(result.StandardOutput);
    }

    private static string ParseCodecFromJson(string jsonOutput)
    {
        if (string.IsNullOrWhiteSpace(jsonOutput))
            throw new InvalidOperationException("ffprobe output is empty");

        try
        {
            using var jsonDoc = JsonDocument.Parse(jsonOutput);
            
            if (!jsonDoc.RootElement.TryGetProperty("streams", out var streams))
                return "unknown";
            
            if (streams.GetArrayLength() == 0)
                return "unknown";

            var videoStream = streams[0];
            if (videoStream.TryGetProperty("codec_name", out var codecElement))
            {
                return codecElement.GetString() ?? "unknown";
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse ffprobe output: {ex.Message}", ex);
        }

        return "unknown";
    }
}