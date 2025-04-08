using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DivergentStrV0_1.OperationSystemAdv
{
    public interface IDomainEventDispatcher
    {
        void Dispatch(IDomainEvent domainEvent);
        void Register<T>(IDomainEventHandler<T> handler) where T : IDomainEvent;
    }

    public class DomainEventDispatcher : IDomainEventDispatcher
    {
        private readonly Dictionary<Type, List<object>> _handlers = new();

        public void Register<T>(IDomainEventHandler<T> handler) where T : IDomainEvent
        {
            var eventType = typeof(T);
            if (!_handlers.ContainsKey(eventType))
                _handlers[eventType] = new List<object>();

            _handlers[eventType].Add(handler);
        }

        public void Dispatch(IDomainEvent domainEvent)
        {
            var eventType = domainEvent.GetType();
            if (_handlers.TryGetValue(eventType, out var handlers))
            {
                foreach (var handler in handlers.Cast<IDomainEventHandler<IDomainEvent>>())
                {
                    handler.Handle(domainEvent);
                }
            }
        }
    }

    // Esempio di registrazione:
    // var dispatcher = new DomainEventDispatcher();
    // dispatcher.Register(new TradeFilledLogger());
    // dispatcher.Register(new PositionClosedLogger());
    // dispatcher.Register(new PartialCloseNotifier());
}
