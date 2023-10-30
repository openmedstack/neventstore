using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace OpenMedStack.NEventStore.Build;

[TaskName("Default")]
[IsDependentOn(typeof(PackTask))]
public sealed class DefaultTask : FrostingTask
{
    public override void Run(ICakeContext context)
    {
        context.Log.Information("Starting build");
    }
}
