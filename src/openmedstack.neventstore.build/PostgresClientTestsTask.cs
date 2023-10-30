using Cake.Core.IO;
using Cake.Frosting;

namespace OpenMedStack.NEventStore.Build;

[TaskName("Postgres-Client-Tests")]
[IsDependentOn(typeof(TestsTask))]
public sealed class PostgresClientTestsTask : DockerTestTask
{
    protected override string[] DockerComposeFiles { get; } =
        { "./tests/openmedstack.neventstore.server.client.tests/docker-compose.yml" };

    protected override FilePath Project { get; } =
        new("./tests/openmedstack.neventstore.server.client.tests/openmedstack.neventstore.server.client.tests.csproj");
}