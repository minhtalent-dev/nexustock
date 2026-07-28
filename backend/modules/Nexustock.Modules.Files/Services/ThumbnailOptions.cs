using System.ComponentModel.DataAnnotations;

namespace Nexustock.Modules.Files.Services;

public class ThumbnailOptions
{
    public bool Enabled { get; set; } = true;
    public bool BackfillEnabled { get; set; } = true;

    [Range(64, 2048)]
    public int MaxEdge { get; set; } = 256;

    [Range(1, 100)]
    public int JpegQuality { get; set; } = 82;

    [Range(100000, 100000000)]
    public long MaxPixels { get; set; } = 40000000; // 40 MP

    [Range(100, 50000)]
    public int MaxDimension { get; set; } = 12000; // 12,000 px

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 10;

    [Range(1, 1000)]
    public int BatchSize { get; set; } = 50;

    [Range(1, 50)]
    public int MaxRetriesPerRun { get; set; } = 3;

    [Range(0, 3600)]
    public int StartupDelaySeconds { get; set; } = 45;
}
