using DivergentStrV0_1.OperationSystemAdv.DDDCore;
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

    public abstract class ConditionableBase<T> : IConditionable , IDisposable
    {
        protected List<OrderType> _allowedOrdersType;
        public virtual TpSlManager _manager { get; private set; } = GlobalTpSlManager.Instance;

        public virtual PerformanceMetrics Metrics { get; private set; }

        public Account Account { get; private set; }
        public bool Initialized { get; private set; }
        public Symbol Symbol { get; private set; }
        //HACK: definito cosi la gestione delle quantita 
        public double Quantity { get; private set; } 
        public IDomainEventDispatcher Dispatcher { get; private set; }
        public string StrategyName => this.GetType().Name;
        public string Description { get; private set; }
        public ISlTpStrategy<T> Strategy { get; private set; }
        public HystoryDataProvider HistoryProvider { get; protected set; }

        protected ConditionableBase()
        {
            this.Metrics = new PerformanceMetrics();
            this.Metrics.SetStrategyTag(this.StrategyName);
            this.Initialized = false;
            this.Metrics.EnableHeavyMetrics = true;
        }

        public virtual void Init(HistoryRequestParameters req,Account account, bool loadAsync , IDomainEventDispatcher dispatcher = null, string description = "", bool allowHeavyMetrics = false)
        {
            this.Account = account;
            this.Metrics.SetPerformanceMetrics(allowHeavyMetrics, this.StrategyName, this.Account);
            this.Description = description;
            this.Symbol = req.Symbol;
            this.Dispatcher = dispatcher != null ? dispatcher : new DomainEventDispatcher();
            this.Metrics.SetAccount(this.Account);
            this.RegisterHandlers();
            this._allowedOrdersType = Symbol.GetAlowedOrderTypes(OrderTypeUsage.All).ToList();
            this.Quantity = this.SetQuantity();
            this.InitHistoryProvider(req, loadAsync);

            this.Initialized = true;
        }
        public virtual void InjectStrategy(object strategy)
        {
            try
            {
                this.Strategy = (ISlTpStrategy<T>)strategy;

            }
            catch (Exception)
            {
                //TODO: Handle this error
                throw;
            }
        }



        #region DEPRECATED [Da rimuovere nella prossima versione]
        /*
         * ⚠️ Questo blocco è deprecato

         * TODO: sostituire DEPRECATED o rimuovere
         */
        #endregion

        public virtual void RegisterHandlers()
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

            var limit = _allowedOrdersType.FirstOrDefault(x => x.Behavior == OrderTypeBehavior.Limit);
            var stop = _allowedOrdersType.FirstOrDefault(x => x.Behavior == OrderTypeBehavior.Stop);
            if (limit == null)
            {
                //HACK:Handle this scenario , rapid implementation only , missing order  type handeling and trigger price managemant
            }

            var sl = Strategy.CalculateSl(slMarketData);
            var slReqests = this.HandleExitReq(sl, ord_Request, stop);

            var tp = Strategy.CalculateTp(tpMarketData);
            var tpReqests = this.HandleExitReq(tp, ord_Request, limit);

            _manager.PlaceEntryOrder(ord_Request, comment, slReqests, tpReqests, this);
        }

        //TODO: Handle OrderType
        
        protected string GenerateComment()
        {
            var guid = Guid.NewGuid().ToString("N");
            return $"{StrategyName}_{guid}";
        }

        #region Utility
        protected virtual int GetVolumeTimeout() => 10000;
        protected virtual int GetRetryDelay() => 500;
        protected virtual List<HistoryUpdAteType> GetUpdateTypes() =>
            new() { HistoryUpdAteType.NewItem, HistoryUpdAteType.UpdateItem };

        protected virtual VolumeAnalysisCalculationParameters BuildVolumeRequest()
        {
            var request = new VolumeAnalysisCalculationParameters()
            {
                CalculatePriceLevels = false,
                DeltaCalculationType = this.Symbol.DeltaCalculationType,
            };

            return request;
        }
        // abstract:
        public abstract void Update(object obj);
        public virtual void OnVolumeDataReady() { }
        public abstract double SetQuantity();
        protected virtual double RoundQuantity(double quantity)
        {
            var req = Math.Floor(quantity / Symbol.MinLot) * Symbol.MinLot;

            if (req < Symbol.MinLot)
                req = 0;

            //TODO: Dispact Trading Info
            return Math.Min(req, Symbol.MaxLot);
        }

        protected virtual void InitHistoryProvider(HistoryRequestParameters historyRequest, bool enableAsyncVolume)
        {

            this.HistoryProvider = HystoryDataProviderFactory.Create(
                request: historyRequest,
                symbol: this.Symbol,
                updateTypes: this.GetUpdateTypes(),
                volumeRequest: this.BuildVolumeRequest(),
                async: enableAsyncVolume,
                onUpdate: this.Update,
                onVolumeReady: this.OnVolumeDataReady,
                timeoutMs: this.GetVolumeTimeout(),
                retryDelayMs: this.GetRetryDelay()
            );
        }

        protected virtual List<PlaceOrderRequestParameters> HandleExitReq(List<double> prices, PlaceOrderRequestParameters origin, OrderType orType)
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
                        OrderTypeId = orType.Id,
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

        public virtual void Dispose()
        {
            // TODO: Dispose all the resources dispatcher?
            this.HistoryProvider?.Dispose();
            this.Strategy = null;
            this.Account = null;
            this.Symbol = null;
            this.Initialized = false;
            this.Dispatcher = null;
            this.Description = null;
            this.Quantity = 0;
            this._manager?.Dispose();
        }
        #endregion


        #region deprecated 
        //public abstract void GetMetrics();

        //public virtual double NetProfit => _manager.NetProfit;
        //public virtual double GrossProfit => _manager.GrossProfit;
        //public virtual double PaiedFees => _manager.PaiedFees;

        //public virtual int PositiveOperations => _manager.N_Positive_Operations;
        //public virtual int NegativeOperations => _manager.N_Negative_Operations;

        //public virtual int ShortCount => _manager.N_Short;
        //public virtual int LongCount => _manager.N_Long;

        //public virtual int PositiveLongs => _manager.N_Positive_Longs;
        //public virtual int PositiveShorts => _manager.N_Positive_Short;

        //public virtual int NegativeLongs => _manager.N_Negative_Long;
        //public virtual int NegativeShorts => _manager.N_Negative_Short;

        //public virtual int OperationCount => _manager.N_Operations;
        //public virtual bool Exposed => _manager.Exposed;
        //public virtual double ExposedAmount => _manager.ExposedAmount;
        //public virtual int TradeCount => _manager.TradeCount;
        #endregion
    }

}
