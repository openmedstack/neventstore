using Cake.Frosting;
using OpenMedStack.NEventStore.Build;

return new CakeHost()
    .InstallTool(new Uri("nuget:?package=GitVersion.Tool&version=5.12.0"))
    //.InstallTool(new Uri("nuget:?package=Cake.Docker&version=1.2.3"))
    .UseContext<BuildContext>()
    .Run(args);
