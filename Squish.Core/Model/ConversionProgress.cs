namespace Squish.Core.Model;

public class ConversionProgress
{
    public double Percentage { get; set; }
    public string Speed { get; set; } = string.Empty;
    public string CurrentFile { get; set; } = string.Empty;
    
    // Overall progress tracking
    public int TotalFiles { get; set; }
    public int CompletedFiles { get; set; }
    public double OverallPercentage => TotalFiles > 0 ? (double)CompletedFiles / TotalFiles * 100 : 0;
}