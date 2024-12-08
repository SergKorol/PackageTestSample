namespace PackageTestSample.UnitTests.Models;

public class PackageInfo
{
    public required string Project { get; set; }
    public required string NuGetPackage { get; set; }
    public required string Version { get; set; }
}