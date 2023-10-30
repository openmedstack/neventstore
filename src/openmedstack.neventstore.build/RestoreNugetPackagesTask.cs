using Cake.Common.Tools.DotNet;
using Cake.Frosting;

namespace OpenMedStack.NEventStore.Build;

[TaskName("Restore-Nuget-Packages")]
[IsDependentOn(typeof(CleanTask))]
public sealed class RestoreNugetPackagesTask : FrostingTask<BuildContext>
{
    /// <inheritdoc />
    public override void Run(BuildContext context)
    {
        context.DotNetRestore(Path.GetFullPath(context.SolutionName));
    }
}
