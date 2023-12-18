using Cake.Common.Tools.GitVersion;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace OpenMedStack.NEventStore.Build;

[TaskName("Version")]
[TaskDescription("Retrieves the current version from the git repository")]
public sealed class VersionTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        GitVersion versionInfo = context.GitVersion(new GitVersionSettings
        {
            UpdateAssemblyInfo = false,
            OutputType = GitVersionOutput.Json
        });

        if (versionInfo.BranchName == "master" || versionInfo.BranchName.StartsWith("tags/"))
        {
            context.BuildVersion =
                versionInfo.CommitsSinceVersionSource is > 0
                    ? $"{versionInfo.MajorMinorPatch}-beta.{versionInfo.CommitsSinceVersionSource.Value}"
                    : versionInfo.MajorMinorPatch;
        }
        else
        {
            context.BuildVersion =
                $"{versionInfo.MajorMinorPatch}-{versionInfo.BranchName.Replace("features/", "").Replace("_", "")}.{versionInfo.CommitsSinceVersionSource}";
        }

        if (versionInfo.BranchName == "master")
        {
            context.BuildConfiguration = "Release";
        }

        context.InformationalVersion = $"{versionInfo.MajorMinorPatch}.{(versionInfo.CommitsSinceVersionSource ?? 0)}";
        context.Log.Information($"Build configuration: {context.BuildConfiguration}");
        context.Log.Information($"Branch: {versionInfo.BranchName}");
        context.Log.Information($"Version: {versionInfo.FullSemVer}");
        context.Log.Information($"Version: {versionInfo.MajorMinorPatch}");
        context.Log.Information($"Build version: {context.BuildVersion}");
        context.Log.Information($"CommitsSinceVersionSource: {versionInfo.CommitsSinceVersionSource}");
    }
}
