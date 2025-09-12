using DivergentStrV0_1.OperationSystemAdv.DDDCore;
using System.Collections.Generic;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv
{
    public interface IConditionable
    {
        public IManagerFacade _manager { get; }
        public PerformanceMetrics Metrics { get; }
        public bool Initialized { get; }
        public Account Account { get; }
        public Symbol Symbol { get; }
        public double Quantity { get; }
        public string Description { get; }
        void RegisterHandlers();
        void Init(HistoryRequestParameters req, Account account, bool loadAsync, string description = "", bool allowHeavyMetrics = false);
        void InjectStrategy(object strategy);
        string StrategyName { get; }
        public void Update(object obj);
        public void Dispose();
        public abstract double SetQuantity();
        public List<string> RegistredGuid { get;}
    }
}
