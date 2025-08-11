namespace Squish.Core.Model;

public class ConversionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string? OutputPath { get; set; }
    public TimeSpan Duration { get; set; }
    public long OriginalSize { get; set; }
    public long NewSize { get; set; }
}