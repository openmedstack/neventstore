using Cake.Core.IO;
using Cake.Frosting;

namespace OpenMedStack.NEventStore.Build;

[TaskName("Persistence-Tests")]
[IsDependentOn(typeof(DynamoDbTestsTask))]
public sealed class PersistenceTestsTask : DockerTestTask
{
    protected override string[] DockerComposeFiles { get; } =
        ["./tests/openmedstack.neventstore.persistence.acceptancetests/docker-compose.yml"];

    protected override FilePath Project { get; } =
        new(
            "./tests/openmedstack.neventstore.persistence.acceptancetests/openmedstack.neventstore.persistence.acceptancetests.csproj");
}
