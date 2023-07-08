namespace OpenMedStack.NEventStore.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging.Abstractions;
    using NEventStore;
    using NEventStore.Persistence.AcceptanceTests;
    using NEventStore.Persistence.AcceptanceTests.BDD;
    using Xunit;

    public class DefaultSerializationWireupTests
    {
        public class WhenBuildingAnEventStoreWithoutAnExplicitSerializer : SpecificationBase
        {
            private Wireup _wireup = null!;
            private Exception _exception = null!;
            private IStoreEvents _eventStore = null!;

            public WhenBuildingAnEventStoreWithoutAnExplicitSerializer()
            {
                OnStart().Wait();
            }

            protected override Task Context()
            {
                _wireup = Wireup.Init(NullLogger.Instance).UsingInMemoryPersistence();
                return Task.CompletedTask;
            }

            protected override Task Because()
            {
                _exception = Catch.Exception(() => { _eventStore = _wireup.Build(); })!;

                return Task.CompletedTask;
            }

            protected override void Cleanup()
            {
                _eventStore.Dispose();
            }

            [Fact]
            public void should_not_throw_an_argument_null_exception()
            {
                // _exception.Should().NotBeOfType<ArgumentNullException>();
                Assert.Null(_exception);
            }
        }
    }
}
