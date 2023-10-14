using Microsoft.Extensions.DependencyInjection;
using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NEventStore;
using NEventStore.Persistence.AcceptanceTests;
using NEventStore.Persistence.AcceptanceTests.BDD;
using Xunit;

public class DefaultSerializationWireupTests
{
    public class WhenBuildingAnEventStoreWithoutAnExplicitLogger : SpecificationBase
    {
        private IServiceProvider _wireup = null!;
        private Exception _exception = null!;

        public WhenBuildingAnEventStoreWithoutAnExplicitLogger()
        {
            OnStart().Wait();
        }

        protected override Task Context()
        {
            var collection = new ServiceCollection().RegisterInMemoryEventStore();
            _wireup = collection.BuildServiceProvider();
            return Task.CompletedTask;
        }

        protected override Task Because()
        {
            _exception = Catch.Exception(() => { var eventStore = _wireup.GetRequiredService<ICommitEvents>(); })!;

            return Task.CompletedTask;
        }

        protected override void Cleanup()
        {
        }

        [Fact]
        public void should_throw_an_invalid_operation_exception()
        {
            // _exception.Should().NotBeOfType<ArgumentNullException>();
            Assert.IsType<InvalidOperationException>(_exception);
        }
    }
}
