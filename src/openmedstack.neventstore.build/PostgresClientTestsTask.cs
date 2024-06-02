using Cake.Core.IO;
using Cake.Frosting;

namespace OpenMedStack.NEventStore.Build;

[TaskName("Postgres-Client-Tests")]
[IsDependentOn(typeof(TestsTask))]
public sealed class PostgresClientTestsTask : DockerTestTask
{
    protected override string[] DockerComposeFiles { get; } =
        ["./tests/openmedstack.neventstore.postgresclient.tests/docker-compose.yml"];

    protected override FilePath Project { get; } =
        new("./tests/openmedstack.neventstore.postgresclient.tests/openmedstack.neventstore.postgresclient.tests.csproj");
}
