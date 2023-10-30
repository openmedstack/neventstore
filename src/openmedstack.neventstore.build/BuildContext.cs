using Cake.Core;
using Cake.Frosting;

namespace OpenMedStack.NEventStore.Build;

public sealed class BuildContext : FrostingContext
{
    public string BuildConfiguration { get; set; } = "Release";
    public string BuildVersion { get; set; } = null!;
    public string InformationalVersion { get; set; } = null!;

    public string SolutionName = "openmedstack-neventstore.sln";

    public BuildContext(ICakeContext context)
        : base(context)
    {
        //Environment.WorkingDirectory = Environment.WorkingDirectory.Combine("..").Combine("..");
        BuildConfiguration = context.Arguments.GetArguments("configuration").FirstOrDefault() ?? "Debug";
    }
}
