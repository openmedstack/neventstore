using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Common.Tools.DotNet.Pack;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace OpenMedStack.NEventStore.Build;

[TaskName("Pack")]
//[IsDependentOn(typeof(PersistenceTestsTask))]
[IsDependentOn(typeof(TestsTask))]
public sealed class PackTask : FrostingTask<BuildContext>
{
    /// <inheritdoc />
    public override void Run(BuildContext context)
    {
        context.Log.Information("Package version: {0}", context.BuildVersion);

        var packSettings = new DotNetPackSettings
        {
            Configuration = context.BuildConfiguration,
            NoBuild = false,
            NoRestore = true,
            OutputDirectory = "./artifacts/packages",
            IncludeSymbols = true,
            MSBuildSettings = new DotNetMSBuildSettings().SetConfiguration(context.BuildConfiguration)
                .SetVersion(context.BuildVersion)
        };

        context.DotNetPack("./src/openmedstack.neventstore.abstractions/openmedstack.neventstore.abstractions.csproj",
            packSettings);
        context.DotNetPack("./src/openmedstack.neventstore/openmedstack.neventstore.csproj", packSettings);
        context.DotNetPack("./src/openmedstack.neventstore.server/openmedstack.neventstore.server.csproj",
            packSettings);
        context.DotNetPack("./src/openmedstack.neventstore.grpcclient/openmedstack.neventstore.grpcclient.csproj",
            packSettings);
        context.DotNetPack("./src/openmedstack.neventstore.httpclient/openmedstack.neventstore.httpclient.csproj",
            packSettings);
        context.DotNetPack("./src/openmedstack.neventstore.pollingclient/openmedstack.neventstore.pollingclient.csproj",
            packSettings);
        context.DotNetPack(
            "./src/openmedstack.neventstore.postgresclient/openmedstack.neventstore.postgresclient.csproj",
            packSettings);
    }
}
