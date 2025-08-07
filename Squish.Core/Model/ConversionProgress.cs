namespace Squish.Core.Model;

public class ConversionProgress
{
    public double Percentage { get; set; }
    public string Speed { get; set; } = string.Empty;
    public string CurrentFile { get; set; } = string.Empty;
}