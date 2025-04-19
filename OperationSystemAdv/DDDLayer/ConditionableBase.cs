using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Integration;

namespace DivergentStrV0_1.OperationSystemAdv
{
    //REQ : Keep it Updated

    public abstract class ConditionableBase<T> : IConditionable
    {
        protected readonly TpSlManager _manager = new TpSlManager();
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
            this.RegisterHandlers();
            this._allowedOrdersType = Symbol.GetAlowedOrderTypes(OrderTypeUsage.All).ToList();
        }

        protected ConditionableBase(Account account, Symbol symbol, IDomainEventDispatcher dispatcher = null, string description = "")
        {
            this.Description = description;
            this.Symbol = symbol;
            this.Dispatcher = dispatcher != null ? dispatcher : new DomainEventDispatcher();
            this.RegisterHandlers();
            this._allowedOrdersType = Symbol.GetAlowedOrderTypes(OrderTypeUsage.All).ToList();

        }

        protected virtual void RegisterHandlers()
        {
            //TODO: Need Implementations
            //Dispatcher.Register(new TradingOperations(this));
        }

        public virtual void Trade(Side side, double price, T slMarketData, T tpMarketData)
        {
            var comment = GenerateComment();

            //TODO: Finire l implementazione di PlaceOrderRequestParameters
            //HACK: Rindondanza di comment
            var ord_Request = new PlaceOrderRequestParameters
            {
                Account = this.Account,
                Symbol = this.Symbol,
                Side = side,
                Quantity = this.RoundQuantity(Quantity/price),
                Price = price,
                TriggerPrice = price,
                OrderTypeId = Symbol.GetAlowedOrderTypes(OrderTypeUsage.Order).FirstOrDefault(x => x.Behavior == OrderTypeBehavior.Market).Id,
                Comment = comment
            };

            var sl = Strategy.CalculateSl(slMarketData);
            var slReqests = this.HandleExitReq(sl, ord_Request);

            var tp = Strategy.CalculateTp(tpMarketData);
            var tpReqests = this.HandleExitReq(tp, ord_Request);

            _manager.PlaceEntryOrder(ord_Request, comment, slReqests, tpReqests, this);
        }

        //TODO: Handle OrderType
        //public virtual void CustomTrade(Side side, double price, OrderType orderType)
        //{
        //    var comment = GenerateComment();

        //    //TODO: Finire l implementazione di PlaceOrderRequestParameters
        //    //HACK: Rindondanza di comment
        //    var ord_Request = new PlaceOrderRequestParameters
        //    {
        //        Account = this.Account,
        //        Symbol = this.Symbol,
        //        Side = side,
        //        Quantity = this.RoundQuantity(Quantity),
        //        Price = price,
        //        TriggerPrice = price,
        //        OrderTypeId = orderType.Id,
        //        Comment = comment
        //    };

        //    _manager.PlaceEntryOrder(ord_Request, comment, this);
        //}

        protected string GenerateComment()
        {
            var guid = Guid.NewGuid().ToString("N");
            return $"{ConditionName}_{guid}";
        }

        // abstract:
        public abstract void Update(object obj);
        public abstract double SetQuantity();

        protected virtual double RoundQuantity(double quantity)
        {
            var req = Math.Floor(quantity / Symbol.MinLot) * Symbol.MinLot;

            //TODO: Dispact Trading Info
            return Math.Min(req, Symbol.MaxLot);
        }

        protected virtual List<PlaceOrderRequestParameters> HandleExitReq(List<double> prices, PlaceOrderRequestParameters origin)
        {
            //TODO:I prezzi d uscita sono sbagliati 
            List<PlaceOrderRequestParameters> collection = new List<PlaceOrderRequestParameters>();
            try
            {
                foreach (var item in prices)
                {
                    PlaceOrderRequestParameters exitReq = new PlaceOrderRequestParameters
                    {
                        Account = origin.Account,
                        Symbol = origin.Symbol,
                        Side = origin.Side == Side.Buy ? Side.Sell : Side.Buy,
                        //TODO:handle unmatching quantity
                        Quantity = this.RoundQuantity(origin.Quantity/prices.Count),
                        Price = item,
                        TriggerPrice = item,
                        OrderTypeId = Symbol.GetAlowedOrderTypes(OrderTypeUsage.Order).FirstOrDefault(x => x.Behavior == OrderTypeBehavior.Market).Id,
                        AdditionalParameters = new List<SettingItem>
                        {
                            new SettingItemBoolean(OrderType.REDUCE_ONLY, true)
                        }
                    };

                    collection.Add(exitReq);
                }
                return collection;

            }
            catch (Exception ex)
            {
                //TODO:Logs
                return collection;

            }
        }

        public abstract void GetMetrics();
        public abstract void Close();

        //TODO: metriche legate al manager
        public virtual double NetProfit => _manager.NetProfit;
        public virtual double LongCount => _manager.N_Long;
        public virtual double ShortCount => _manager.N_Short;
        public virtual int LongExpo => _manager.Items.Count(x => x.Side == Side.Buy && x.Status != PositionManagerStatus.Closed);
        public virtual int ShortExpo => _manager.Items.Count(x => x.Side == Side.Sell && x.Status != PositionManagerStatus.Closed);
    }

}
