using DivergentStrV0_1.OperationSystemAdv.DDDCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv
{
    public interface IConditionable
    {
        protected TpSlManager _manager { get; }
        public PerformanceMetrics Metrics { get; }
        public bool Initialized { get; }
        public Account Account { get; }
        public Symbol Symbol { get; }
        public double Quantity { get; }
        public IDomainEventDispatcher Dispatcher { get; }
        public string Description { get; }
        void RegisterHandlers();
        void Init(Account account, Symbol symbol, IDomainEventDispatcher dispatcher = null, string description = "", bool allowHeavyMetrics = false);
        void InjectStrategy(object strategy);
        string StrategyName { get; }
        public void Update(object obj);
        public void Close();
        public abstract double SetQuantity();
    }
}
