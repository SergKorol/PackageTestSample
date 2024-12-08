using System.Xml.Linq;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using PackageTestSample.UnitTests.Models;

namespace PackageTestSample.UnitTests;

public class NuGetPackageTests
{
    private static string[] _projectFiles;
    private static string _solutionDirectory;
    
    public NuGetPackageTests()
    {
        _solutionDirectory = Directory.GetCurrentDirectory()
            .Split("PackageTestSample.UnitTests")
            .First();
        _projectFiles = ListSolutionProjectPaths();
    }
    
    
    [Fact]
    public async Task CheckPackageLicenses()
    {
        //Arrange
        var restrictedNuGetPackages = new List<PackageLicenseInfo>();
        var allowedNugetPackageLicenses = new List<string>
        {
            "MIT",
            "Apache-2.0",
            "Microsoft"
        };

        var resource = await GetNugetResourceAsync();

        //Act
        foreach (var projectFile in _projectFiles)
        {
            foreach (var packageReference in ListNuGetPackages(projectFile))
            {
                var metadata = await GetNuGetMetadataAsync(packageReference, resource);
                var licenseMetadata = metadata.LicenseMetadata?.License ?? metadata.Authors;

                if (allowedNugetPackageLicenses.Contains(licenseMetadata)) continue;
                var nugetPackage = new PackageLicenseInfo
                {
                    NuGetPackage = packageReference.NuGetPackage,
                    Version = packageReference.Version,
                    Project = packageReference.Project,
                    License = licenseMetadata
                };

                restrictedNuGetPackages.Add(nugetPackage);
            }
        }

        //Assert
        Assert.Empty(restrictedNuGetPackages);
    }
    
    [Fact]
    public async Task CheckPackageVulnerabilities()
    {
        // Arrange
        var vulnerableNuGetPackages = new List<PackageVulnerabilityInfo>();
        var resource = await GetNugetResourceAsync();
    
        // Act
        foreach (var projectFile in _projectFiles)
        {
            foreach (var packageReference in ListNuGetPackages(projectFile))
            {
                var metadata = await GetNuGetMetadataAsync(packageReference, resource);
                var vulnerabilities = metadata.Vulnerabilities ?? new List<PackageVulnerabilityMetadata>();
    
                if (!vulnerabilities.Any()) continue;
                var nugetPackage = new PackageVulnerabilityInfo
                {
                    NuGetPackage = packageReference.NuGetPackage,
                    Version = packageReference.Version,
                    Project = packageReference.Project,
                    Vulnerabilities = vulnerabilities
                };
    
                vulnerableNuGetPackages.Add(nugetPackage);
            }
        }
    
        // Assert
        Assert.Empty(vulnerableNuGetPackages);
    }
    
    [Fact]
    public async Task CheckDeprecatedPackage()
    {
        // Arrange
        var deprecatedPackages = new List<PackageDeprecationInfo>();
        var resource = await GetNugetResourceAsync();
    
        // Act
        foreach (var projectFile in _projectFiles)
        {
            foreach (var packageReference in ListNuGetPackages(projectFile))
            {
                var metadata = await GetNuGetMetadataAsync(packageReference, resource);
                var tags = metadata.Tags ?? string.Empty;
    
                if (!tags.Contains("Deprecated")) continue;
                var nugetPackage = new PackageDeprecationInfo
                {
                    NuGetPackage = packageReference.NuGetPackage,
                    Version = packageReference.Version,
                    Project = packageReference.Project,
                    IsDeprecated = (await metadata.GetDeprecationMetadataAsync()).Reasons.Any()
                };
    
                deprecatedPackages.Add(nugetPackage);
            }
        }
    
        // Assert
        Assert.Empty(deprecatedPackages);
    }
    
    
    
    [Fact]
    public void CheckPackageVersionMismatches()
    {
        // Arrange
        var installedNuGetPackages = new List<PackageInfo>();
    
        foreach (var projectFile in _projectFiles)
        {
            installedNuGetPackages.AddRange(ListNuGetPackages(projectFile));
        }
    
        // Act
        var packagesToConsolidate = installedNuGetPackages
            .GroupBy(package => package.NuGetPackage)
            .Where(packageGroup => packageGroup.Select(package => package.Version).Distinct().Count() > 1)
            .Select(packageToConsolidate => new
            {
                PackageName = packageToConsolidate.Key,
                Versions = packageToConsolidate.Select(package => $"{package.Project}: {package.Version}")
            }).ToList();
    
        // Assert
        Assert.Empty(packagesToConsolidate);
    }
    
    [Fact]
    public void CheckNoInstalledNuGetPackages()
    {
        //Arrange & Act
        var projectFiles = Directory.GetFiles(_solutionDirectory, "*Domain.csproj", SearchOption.AllDirectories);
        var packages = new List<PackageInfo>();
    
        foreach (var projectFile in projectFiles)
        {
            packages.AddRange(ListNuGetPackages(projectFile).ToList());
        }
    
        //Assert
        Assert.Empty(packages);
    }
    
    [Fact]
    public void CheckAllowedVersionsNuGetPackages()
    {
        //Arrange & Act
        var projectFiles = Directory.GetFiles(_solutionDirectory, "*AppSample.csproj", SearchOption.AllDirectories);
        var packages = new List<PackageInfo>();
    
        foreach (var projectFile in projectFiles)
        {
            packages.AddRange(ListNuGetPackages(projectFile).ToList());
        }
    
        //Assert
        Assert.Equal("6.6.2", packages.FirstOrDefault(p => p.NuGetPackage == "Swashbuckle.AspNetCore")?.Version);
    }



    
    private static string[] ListSolutionProjectPaths()
    {
        return Directory.GetFiles(
            path: _solutionDirectory,
            searchPattern: "*.csproj",
            searchOption: SearchOption.AllDirectories
        );
    }
    
    private async Task<PackageMetadataResource> GetNugetResourceAsync()
    {
        var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");

        return await repository.GetResourceAsync<PackageMetadataResource>();
    }
    
    private static PackageInfo[] ListNuGetPackages(string projectFilePath)
    {
        return XDocument
            .Load(projectFilePath)
            .Descendants("PackageReference")
            .Select(packageReference=> new PackageInfo
            {
                Project = projectFilePath.Split('\\').Last().Split(".csproj").First(),
                NuGetPackage = packageReference.Attribute("Include")?.Value ?? string.Empty,
                Version = packageReference.Attribute("Version")?.Value ?? string.Empty
            }).ToArray();
    }

    private async Task<IPackageSearchMetadata> GetNuGetMetadataAsync(PackageInfo packageReference, PackageMetadataResource resource)
    {
        var packageIdentity = new PackageIdentity(
            id: packageReference.NuGetPackage,
            version: NuGetVersion.Parse(packageReference.Version)
        );

        return await resource.GetMetadataAsync(
            package: packageIdentity,
            sourceCacheContext: new SourceCacheContext(),
            log: NullLogger.Instance,
            token: default
        );
    }

    
}