using Cake.Core.IO;
using Cake.Frosting;

namespace OpenMedStack.NEventStore.Build;

[TaskName("Postgres-Persistence-Tests")]
[IsDependentOn(typeof(PostgresClientTestsTask))]
public sealed class PostgresPersistenceTestsTask : DockerTestTask
{
    protected override string[] DockerComposeFiles { get; } =
        { "./tests/openmedstack.neventstore.persistence.postgresql.tests/docker-compose.yml" };

    protected override FilePath Project { get; } =
        new(
            "./tests/openmedstack.neventstore.persistence.postgresql.tests/openmedstack.neventstore.persistence.postgresql.tests.csproj");
}
