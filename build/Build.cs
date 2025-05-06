using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using Serilog;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.PathConstruction;

[AzurePipelines("CI",
    AzurePipelinesImage.WindowsLatest,
    InvokedTargets = new[] { nameof(CI) },
    NonEntryTargets = new[] { nameof(Restore), nameof(Compile), nameof(Pack) },
    LargeFileStorage = true)]
class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Solution(GenerateProjects = true)]
    private static Solution Solution;
    private static AbsolutePath NugetDirectory => RootDirectory / "bin" / "nuget";

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    private readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    /// <summary>
    /// Shorthand for the targets we want the CI pipeline to do.
    /// </summary>
    Target CI => _ => _
        .DependsOn(Compile, Pack);

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            DotNetTasks.DotNetClean(_ => _
                .SetProject(Solution)
                .SetConfiguration(Configuration)
            );

            var binOutput = RootDirectory / "bin";
            Log.Information($"Deleting {binOutput}");
            binOutput.DeleteDirectory();
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetTasks.DotNetRestore(_ => _
                .SetProjectFile(Solution)
                .SetConfigFile(RootDirectory / "NuGet.Config")
            );

            Log.Information("Contents of bin directory after restore:");
            foreach (var file in (RootDirectory / "bin").GlobFiles("**/*"))
                Log.Information(file.ToString());
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetTasks.DotNetBuild(_ => [_
            .SetProjectFile(Solution)
            .SetConfiguration(Configuration)
            .SetNoRestore(true) // Force use of already restored packages.
            .When(_ => IsServerBuild, _ => _
                .EnableDeterministic())],
            degreeOfParallelism: 8);

            Log.Information("Contents of bin directory after compile:");
            foreach (var file in (RootDirectory / "bin").GlobFiles("**/*"))
                Log.Information(file.ToString());
        });

    Target Pack => _ => _
        .DependsOn(Compile)
        .Produces(NugetDirectory / "*.nupkg")
        .Executes(() =>
        {
            Log.Information("Contents of bin directory before pack:");
            foreach (var file in (RootDirectory / "bin").GlobFiles("**/*"))
                Log.Information(file.ToString());

            string BuildId = AzurePipelines.Instance?.BuildId.ToString() ?? "0";

            DotNetTasks.DotNetPack(_ => _
                .SetProject(Solution)
                .SetConfiguration(Configuration)
                .SetOutputDirectory(NugetDirectory)
                .SetIncludeSymbols(true)
                .SetIncludeSource(true)
                .SetVersion(DateTime.Now.ToString($"yy.M.0.") + BuildId)
                .SetNoBuild(true) // Force use of already built binaries.
            );
        });
}
