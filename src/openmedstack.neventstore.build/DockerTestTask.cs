using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Test;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Docker;
using Cake.Frosting;

namespace OpenMedStack.NEventStore.Build;

public abstract class DockerTestTask : FrostingTask<BuildContext>
{
    protected abstract string[] DockerComposeFiles { get; }

    protected abstract FilePath Project { get; }

    /// <inheritdoc />
    public override void Run(BuildContext context)
    {
        context.Log.Information("Docker compose up");
        context.Log.Information("Ensuring test report output");

        context.EnsureDirectoryExists(context.Environment.WorkingDirectory.Combine("artifacts")
            .Combine("testreports"));

        var upsettings = new DockerComposeUpSettings
        {
            Detach = true,
            Files = DockerComposeFiles
        };
        context.DockerComposeUp(upsettings);

        context.Log.Information("Testing: {0}", Project.FullPath);
        var reportName = "./artifacts/testreports/"
          + context.BuildVersion
          + "_"
          + System.IO.Path.GetFileNameWithoutExtension(Project.FullPath).Replace('.', '_')
          + ".xml";
        reportName = System.IO.Path.GetFullPath(reportName);

        context.Log.Information(reportName);

        var coreTestSettings = new DotNetTestSettings
        {
            NoBuild = true,
            NoRestore = true,
            // Set configuration as passed by command line
            Configuration = context.BuildConfiguration,
            ArgumentCustomization = x => x.Append("--logger \"trx;LogFileName=" + reportName + "\"")
        };

        context.DotNetTest(Project.FullPath, coreTestSettings);
    }

    public override void Finally(BuildContext context)
    {
        context.Log.Information("Docker compose down");

        var downSettings = new DockerComposeDownSettings
        {
            Files = DockerComposeFiles
        };
        context.DockerComposeDown(downSettings);
    }
}
