using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Integration;

namespace DivergentStrV0_1.OperationSystemAdv
{
    //REQ : Keep it Updated

    public abstract class ConditionableBase<T> : IConditionable
    {
        protected readonly TpSlManager<T> _manager;
        protected readonly List<OrderType> _allowedOrdersType;

        public Account Account { get; protected set; }
        public Symbol Symbol { get; protected set; }
        //HACK: definito cosi la gestione delle quantita 
        public double Quantity => this.SetQuantity();
        public IDomainEventDispatcher Dispatcher { get; private set; }
        public string ConditionName => this.GetType().Name;
        public string Description { get; private set; }
        private ISlTpStrategy<T> Strategy { get; set; }

        protected ConditionableBase(Account account, Symbol symbol, ISlTpStrategy<T> strategy, IDomainEventDispatcher dispatcher = null, string description = "")
        {
            this.Description = description;
            this.Account = account;
            this.Symbol = symbol;
            this.Strategy = strategy;
            this.Dispatcher = dispatcher != null ? dispatcher : new DomainEventDispatcher();
            this._manager = this.BuildManager();
            this.RegisterHandlers();
            this._allowedOrdersType = Symbol.GetAlowedOrderTypes(OrderTypeUsage.All).ToList();
        }

        protected ConditionableBase(Account account, Symbol symbol, IDomainEventDispatcher dispatcher = null, string description = "")
        {
            this.Description = description;
            this.Symbol = symbol;
            this.Dispatcher = dispatcher != null ? dispatcher : new DomainEventDispatcher();
            this._manager = this.BuildManager();
            this.RegisterHandlers();
            this._allowedOrdersType = Symbol.GetAlowedOrderTypes(OrderTypeUsage.All).ToList();
        }

        protected virtual void RegisterHandlers()
        {
            //TODO: Need Implementations
            //Dispatcher.Register(new TradingOperations(this));
        }

        public virtual void Trade(Side side, double price)
        {
            var comment = GenerateComment();

            //TODO: Finire l implementazione di PlaceOrderRequestParameters
            //HACK: Rindondanza di comment
            var ord_Request = new PlaceOrderRequestParameters
            {
                Account = this.Account,
                Symbol = this.Symbol,
                Side = side,
                Quantity = this.RoundQuantity(Quantity),
                Price = price,
                TriggerPrice = price,
                OrderTypeId = Symbol.GetAlowedOrderTypes(OrderTypeUsage.Order).FirstOrDefault(x => x.Behavior == OrderTypeBehavior.Market).Id,
                Comment = comment
            };

            _manager.PlaceEntryOrder(ord_Request, comment, this);
        }

        public virtual void Trade(Side side, double price, OrderType orderType)
        {
            var comment = GenerateComment();

            //TODO: Finire l implementazione di PlaceOrderRequestParameters
            //HACK: Rindondanza di comment
            var ord_Request = new PlaceOrderRequestParameters
            {
                Account = this.Account,
                Symbol = this.Symbol,
                Side = side,
                Quantity = this.RoundQuantity(Quantity),
                Price = price,
                TriggerPrice = price,
                OrderTypeId = orderType.Id,
                Comment = comment
            };

            _manager.PlaceEntryOrder(ord_Request, comment, this);
        }

        protected string GenerateComment()
        {
            var guid = Guid.NewGuid().ToString("N");
            return $"{ConditionName}_{guid}";
        }

        // abstract:
        public abstract void Update(object obj);
        public abstract double SetQuantity();
        protected virtual TpSlManager<T> BuildManager()
        {
            var holder = new SlTpCondictionHolder<T>(this.Symbol)
            {
                Strategy = this.Strategy
            };
            holder.UseStrategyMode();

            return new TpSlManager<T>(holder);
        }

        protected virtual double RoundQuantity(double quantity)
        {
            var req = Math.Floor(quantity / Symbol.MinLot) * Symbol.MinLot;

            //TODO: Dispact Trading Info
            return Math.Min(req, Symbol.MaxLot);
        }

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
