using DivergentStrV0_1.OperationSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv
{
    public abstract class ConditionableBase<T> : IConditionable
    {
        protected readonly ITpSlManager<T> _manager;
        protected readonly IOrderExecutor _orderExecutor;

        public Account Account { get; protected set; }
        public Symbol Symbol { get; protected set; }
        public double Quantity { get; protected set; } = 1;
        public bool UsePosition { get; protected set; } = true;

        public string ConditionName => this.GetType().Name;

        protected ConditionableBase(Account account, Symbol symbol, ITpSlManager<T> manager, IOrderExecutor orderExecutor)
        {
            this.Account = account;
            this.Symbol = symbol;
            this._manager = manager;
            this._orderExecutor = orderExecutor;
        }

        public virtual void Trade(Side side, double price)
        {
            var comment = GenerateComment();

            //TODO: Finire l implementazione di PlaceOrderRequestParameters
            var ord_Request = new PlaceOrderRequestParameters
            {
                Account = this.Account,
                Symbol = this.Symbol,
                Side = side,
                Quantity = this.Quantity,
                Price = price,
                Comment = comment
            };

            var result = _orderExecutor.ExecuteMarketOrder(ord_Request);

            if (result.Status == TradingOperationResultStatus.Success)
            {
                _manager.PlaceEntryOrder(comment);
            }
        }

        protected virtual string GenerateComment()
        {
            var guid = Guid.NewGuid().ToString("N");
            return $"{ConditionName}_{guid}";
        }

        // abstract:
        public abstract void Update(object obj);
        public abstract void SetCondictionHolder();
        public abstract void ManagerInit();
        public abstract void GetMetrics();
        public abstract void Close();

        // metriche legate al manager
        public virtual double NetProfit => _manager.NetProfit;
        public virtual double LongCount => _manager.Items.Count(x => x.Side == Side.Buy);
        public virtual double ShortCount => _manager.Items.Count(x => x.Side == Side.Sell);
        public virtual int LongExpo => _manager.Items.Count(x => x.Side == Side.Buy && x.Status != PositionManagerStatus.Closed);
        public virtual int ShortExpo => _manager.Items.Count(x => x.Side == Side.Sell && x.Status != PositionManagerStatus.Closed);
        public virtual int LongPositionCount => _manager.Items.Select(x => x.RelatedPosition).Distinct().Count(x => x?.Side == Side.Buy);
        public virtual int ShortPositionCount => _manager.Items.Select(x => x.RelatedPosition).Distinct().Count(x => x?.Side == Side.Sell);
    }

}
