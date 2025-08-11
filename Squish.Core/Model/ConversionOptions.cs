namespace Squish.Core.Model;

public class ConversionOptions
{
    public bool UseGpu { get; set; } = true;
    public int ParallelJobs { get; set; } = Environment.ProcessorCount;
    public int? Limit { get; set; }
    public bool ListOnly { get; set; }
    public string? OutputFolder { get; set; }
    public bool PreserveTimestamps { get; set; } = true;
}