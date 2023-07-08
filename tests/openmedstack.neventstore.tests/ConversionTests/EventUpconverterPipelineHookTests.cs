using OpenMedStack.NEventStore.Abstractions;

namespace OpenMedStack.NEventStore.Tests.ConversionTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Conversion;
    using Microsoft.Extensions.Logging.Abstractions;
    using NEventStore;
    using NEventStore.Persistence;
    using NEventStore.Persistence.AcceptanceTests.BDD;
    using Xunit;

    public class WhenOpeningACommitThatDoesNotHaveConvertibleEvents : UsingEventConverter
    {
        private ICommit _commit = null!;
        private ICommit _converted = null!;

        protected override Task Context()
        {
            _commit = CreateCommit(new EventMessage(new NonConvertingEvent()));
            return Task.CompletedTask;
        }

        protected override async Task Because()
        {
            _converted = await EventUpconverter.Select(_commit).ConfigureAwait(false);
        }

        [Fact]
        public void should_not_be_converted()
        {
            Assert.Same(_commit, _converted);
        }

        [Fact]
        public void should_have_the_same_instance_of_the_event()
        {
            Assert.Equal(_converted.Events.Single(), _commit.Events.Single());
        }
    }

    public class WhenOpeningACommitThatHasConvertibleEvents : UsingEventConverter
    {
        private ICommit _commit = null!;

        private readonly Guid _id = Guid.NewGuid();
        private ICommit _converted = null!;

        protected override Task Context()
        {
            _commit = CreateCommit(new EventMessage(new ConvertingEvent(_id)));
            return Task.CompletedTask;
        }

        protected override async Task Because()
        {
            _converted = await EventUpconverter.Select(_commit).ConfigureAwait(false);
        }

        [Fact]
        public void should_be_of_the_converted_type()
        {
            Assert.IsType<ConvertingEvent3>(_converted.Events.Single().Body);
        }

        [Fact]
        public void should_have_the_same_id_of_the_commited_event()
        {
            Assert.Equal(_id, ((ConvertingEvent3)_converted.Events.Single().Body).Id);
        }
    }

    public class WhenAnEventConverterImplementsTheIConvertEventsInterfaceExplicitly : UsingEventConverter
    {
        private ICommit _commit = null!;
        private readonly Guid _id = Guid.NewGuid();
        private ICommit _converted = null!;
        private EventMessage _eventMessage = null!;

        protected override Task Context()
        {
            _eventMessage = new EventMessage(new ConvertingEvent2(_id, "FooEvent"));

            _commit = CreateCommit(_eventMessage);

            return Task.CompletedTask;
        }

        protected override async Task Because()
        {
            _converted = await EventUpconverter.Select(_commit).ConfigureAwait(false);
        }

        [Fact]
        public void should_be_of_the_converted_type()
        {
            Assert.IsType<ConvertingEvent3>(_converted.Events.Single().Body);
        }

        [Fact]
        public void should_have_the_same_id_of_the_commited_event()
        {
            Assert.Equal(_id, ((ConvertingEvent3)_converted.Events.Single().Body).Id);
        }
    }

    public abstract class UsingEventConverter : SpecificationBase
    {
        private IEnumerable<Assembly> _assemblies = null!;
        private Dictionary<Type, Func<object, object>> _converters = null!;
        private EventUpconverterPipelineHook? _eventUpconverter;

        public UsingEventConverter()
        {
            OnStart().Wait();
        }

        protected EventUpconverterPipelineHook EventUpconverter => _eventUpconverter ??= CreateUpConverterHook();

        private EventUpconverterPipelineHook CreateUpConverterHook()
        {
            _assemblies = GetAllAssemblies();
            _converters = GetConverters(_assemblies);
            return new EventUpconverterPipelineHook(_converters, NullLogger.Instance);
        }

        private Dictionary<Type, Func<object, object>> GetConverters(IEnumerable<Assembly> toScan)
        {
            var c = from a in toScan
                    from t in a.GetTypes()
                    let i = t.GetInterface(typeof(IUpconvertEvents<,>).FullName!)
                    where i != null
                    let sourceType = i.GetGenericArguments().First()
                    let convertMethod = i.GetMethods(BindingFlags.Public | BindingFlags.Instance).First()
                    let instance = Activator.CreateInstance(t)
                    select new KeyValuePair<Type, Func<object, object>>(sourceType,
                        e => convertMethod.Invoke(instance, new[] { e })!);
            try
            {
                return c.ToDictionary(x => x.Key, x => x.Value);
            }
            catch (ArgumentException ex)
            {
                throw new MultipleConvertersFoundException(ex.Message, ex);
            }
        }

        private IEnumerable<Assembly> GetAllAssemblies()
        {
            return
                Assembly.GetCallingAssembly().GetReferencedAssemblies().Select(Assembly.Load)
                    .Concat(new[] { Assembly.GetCallingAssembly() });
        }

        protected static ICommit CreateCommit(EventMessage eventMessage)
        {
            return new Commit(Bucket.Default,
                Guid.NewGuid().ToString(),
                0,
                Guid.NewGuid(),
                0,
                DateTimeOffset.MinValue,
                0,
                null,
                new[] { eventMessage });
        }
    }

    public class ConvertingEventConverter : IUpconvertEvents<ConvertingEvent, ConvertingEvent2>
    {
        public ConvertingEvent2 Convert(ConvertingEvent sourceEvent) => new(sourceEvent.Id, "Temp");
    }

    public class ExplicitConvertingEventConverter : IUpconvertEvents<ConvertingEvent2, ConvertingEvent3>
    {
        ConvertingEvent3 IUpconvertEvents<ConvertingEvent2, ConvertingEvent3>.Convert(ConvertingEvent2 sourceEvent) =>
            new(sourceEvent.Id, "Temp", true);
    }

    public class NonConvertingEvent
    {
    }

    public class ConvertingEvent
    {
        public ConvertingEvent(Guid id)
        {
            Id = id;
        }

        public Guid Id { get; set; }
    }

    public class ConvertingEvent2
    {
        public ConvertingEvent2(Guid id, string name)
        {
            Id = id;
            Name = name;
        }

        public Guid Id { get; set; }
        public string Name { get; set; }
    }

    public class ConvertingEvent3
    {
        public ConvertingEvent3(Guid id, string name, bool imExplicit)
        {
            Id = id;
            Name = name;
            ImExplicit = imExplicit;
        }

        public Guid Id { get; set; }
        public string Name { get; set; }
        public bool ImExplicit { get; set; }
    }
}
