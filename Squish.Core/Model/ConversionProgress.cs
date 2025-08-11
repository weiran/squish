namespace Squish.Core.Model;

public class ConversionProgress
{
    public double Percentage { get; set; }
    public string Speed { get; set; } = string.Empty;
    public string CurrentFile { get; set; } = string.Empty;
    
    // Overall progress tracking
    public int TotalFiles { get; set; }
    public int CompletedFiles { get; set; }
    public double PartialProgress { get; set; } // Progress of files currently being processed
    
    // Calculate overall percentage including partial progress of active conversions
    public double OverallPercentage => TotalFiles > 0 ? (CompletedFiles + PartialProgress) / TotalFiles * 100 : 0;
}