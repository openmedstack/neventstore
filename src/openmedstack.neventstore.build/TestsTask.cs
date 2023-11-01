using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Test;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;

namespace OpenMedStack.NEventStore.Build;

[TaskName("Tests")]
[IsDependentOn(typeof(BuildTask))]
public sealed class TestsTask : FrostingTask<BuildContext>
{
    /// <inheritdoc />
    public override void Run(BuildContext context)
    {
        context.Log.Information("Ensuring test report output");
        context.EnsureDirectoryExists(context.Environment.WorkingDirectory.Combine("artifacts").Combine("testreports"));

        var projects = new FilePathCollection
        {
            new FilePath("./tests/openmedstack.neventstore.tests/openmedstack.neventstore.tests.csproj"),
            new FilePath("./tests/openmedstack.neventstore.server.client.tests/openmedstack.neventstore.server.client.tests.csproj")
        };

        foreach (var project in projects)
        {
            context.Log.Information("Testing: {0}", project.FullPath);
            var reportName = "./artifacts/testreports/"
              + context.BuildVersion
              + "_"
              + System.IO.Path.GetFileNameWithoutExtension(project.FullPath).Replace('.', '_')
              + ".xml";
            reportName = System.IO.Path.GetFullPath(reportName);

            context.Log.Information(reportName);

            var coreTestSettings = new DotNetTestSettings()
            {
                NoBuild = true,
                NoRestore = true,
                // Set configuration as passed by command line
                Configuration = context.BuildConfiguration,
                ArgumentCustomization = x => x.Append("--logger \"trx;LogFileName=" + reportName + "\"")
            };

            context.DotNetTest(project.FullPath, coreTestSettings);
        }
    }
}
