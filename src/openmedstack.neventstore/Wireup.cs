namespace OpenMedStack.NEventStore
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Transactions;
    using OpenMedStack.NEventStore.Conversion;
    using OpenMedStack.NEventStore.Persistence;
    using OpenMedStack.NEventStore.Persistence.InMemory;
    using System;
    using Microsoft.Extensions.Logging;

    public class Wireup
    {
        private readonly NanoContainer? _container;
        private readonly Wireup? _inner;
        private readonly ILogger _logger;

        protected Wireup(NanoContainer container, ILogger logger)
        {
            _container = container;
            _logger = logger;
        }

        protected Wireup(Wireup inner)
        {
            _inner = inner;
            _logger = inner.Logger;
        }

        public ILogger Logger => _logger;

        protected NanoContainer Container => _container ?? _inner!.Container;

        public static Wireup Init(ILogger logger)
        {
            var container = new NanoContainer(logger);
            container.Register(TransactionScopeAsyncFlowOption.Enabled);

            container.Register<IPersistStreams>(new InMemoryPersistenceEngine(logger));
            container.Register(BuildEventStore);

            return new Wireup(container, logger);
        }

        public virtual Wireup With<T>(T instance)
            where T : class
        {
            Container.Register(instance);
            return this;
        }

        public virtual Wireup HookIntoPipelineUsing(IEnumerable<IPipelineHook> hooks) =>
            HookIntoPipelineUsing(hooks.ToArray());

        public virtual Wireup HookIntoPipelineUsing(params IPipelineHook[] hooks)
        {
            _logger.LogInformation(Resources.WireupHookIntoPipeline, string.Join(", ", hooks.Select(h => h.GetType())));
            ICollection<IPipelineHook> collection = hooks.ToArray();
            Container.Register(collection);
            return this;
        }

        public virtual IStoreEvents Build()
        {
            if (_inner != null)
            {
                return _inner.Build();
            }

            return Container.Resolve<IStoreEvents>()
                   ?? throw new Exception($"Could not resolve {nameof(IStoreEvents)}");
        }

        private static IStoreEvents BuildEventStore(NanoContainer context)
        {
            var concurrency = new OptimisticPipelineHook(context.Resolve<ILogger>()!);

            var upconverter = context.Resolve<EventUpconverterPipelineHook>();

            var hooks = context.Resolve<ICollection<IPipelineHook>>() ?? Array.Empty<IPipelineHook>();
            hooks = upconverter == null
                ? new IPipelineHook[] { concurrency }
                : new IPipelineHook[] { concurrency, upconverter }.Concat(hooks).ToArray();

            var persistStreams = context.Resolve<IPersistStreams>()
                                 ?? throw new Exception($"Could not resolve {nameof(IPersistStreams)}");
            return new OptimisticEventStore(persistStreams, hooks, context.Resolve<ILogger>()!);
        }
    }
}
