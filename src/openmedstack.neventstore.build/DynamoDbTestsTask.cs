using Cake.Core.IO;
using Cake.Frosting;

namespace OpenMedStack.NEventStore.Build;

[TaskName("DynamoDB-Persistence-Tests")]
[IsDependentOn(typeof(PostgresClientTestsTask))]
public sealed class DynamoDbTestsTask : DockerTestTask
{
    protected override string[] DockerComposeFiles { get; } =
        ["./tests/openmedstack.neventstore.dynamodb.tests/docker-compose.yml"];

    protected override FilePath Project { get; } =
        new(
            "./tests/openmedstack.neventstore.dynamodb.tests/openmedstack.neventstore.dynamodb.tests.csproj");
}
