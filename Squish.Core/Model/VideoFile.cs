namespace Squish.Core.Model;

public class VideoFile
{
    public required string FilePath { get; set; }
    public long FileSize { get; set; }
    public string? Codec { get; set; }
}