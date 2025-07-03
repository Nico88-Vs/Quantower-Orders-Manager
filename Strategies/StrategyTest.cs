using DivergentStrV0_1.OperationSystemAdv;
using DivergentStrV0_1.OperationSystemAdv.DDDCore;
using System;
using System.Collections.Generic;
using System.Linq;
using TradingPlatform.BusinessLayer;
namespace DivergentStrV0_1.Strategies
{
    internal class StrategyTest : ConditionableBase<double>
    {
        private double _lastDelta;

        public StrategyTest(): base()
        {
            
        }

        public override void Dispose() => base.Dispose();
        public override double SetQuantity()
        {
            return Account.Balance;
        }

        protected override void InitHistoryProvider(HistoryRequestParameters historyRequest, bool enableAsyncVolume)
        {
            base.InitHistoryProvider(historyRequest, enableAsyncVolume);
        }

        public void CustomTrade(Side side, double price) => this.Trade(side, price, price, price);
      

        public override void Update(object obj)
        {
            if (this.Metrics.Exposed)
                return;
            try
            {
                // TODO: gestire meglio questi cast
                HistoryEventArgs item = (HistoryEventArgs)obj;
                HistoryItem data = (HistoryItem)item.HistoryItem;
                double sign;
                if (_lastDelta != 0 && (sign = Math.Sign(data.VolumeAnalysisData.Total.Delta)) != Math.Sign(_lastDelta))
                {
                    Side side = sign > 0 ? Side.Buy : Side.Sell;
                    this.CustomTrade(side, data[PriceType.Close]);
                }

                this._lastDelta = data.VolumeAnalysisData.Total.Delta;
                
            }
            catch (System.Exception)
            {

                throw;
            }
           
        }

        protected override List<HistoryUpdAteType> GetUpdateTypes()
        {
            return new List<HistoryUpdAteType>
            {
                HistoryUpdAteType.NewItem,
            };
        }
    }

    internal struct TradeData
    {
        public bool Tarde { get; private set; }
        public double Price { get; private set; }

        public TradeData(bool trade, double price)
        {
            this.Tarde = trade;
            this.Price = price;
            
        }
    }

}
