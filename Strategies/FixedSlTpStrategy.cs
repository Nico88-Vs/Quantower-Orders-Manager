using DivergentStrV0_1.OperationSystemAdv;
using DivergentStrV0_1.OperationSystemAdv.DDDCore;
using System;
using System.Collections.Generic;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.Strategies
{
    public class FixedSlTpStrategy : ISlTpStrategy<double> 
    {
        private readonly double _slPercent;
        private readonly double _tpPercent;

        public FixedSlTpStrategy(double slPercent, double tpPercent)
        {
            if (slPercent <= 0)
                throw new ArgumentException("SL percent must be positive", nameof(slPercent));
            if (tpPercent <= 0)
                throw new ArgumentException("TP percent must be positive", nameof(tpPercent));
                
            _slPercent = slPercent;
            _tpPercent = tpPercent;
        }

        public double SlMarketData { get; set; }
        public double TpMarketData { get; set; }

        public List<double> CalculateSl(double marketData, Side side, double entry_price)
        {
            if (entry_price <= 0)
                throw new ArgumentException("Entry price must be positive", nameof(entry_price));
                
            double slPrice = side == Side.Buy 
                ? entry_price * (1 - _slPercent)  // For Buy: SL below entry price
                : entry_price * (1 + _slPercent); // For Sell: SL above entry price
                
            return new List<double> { slPrice };
        }

        public List<double> CalculateTp(double marketData, Side side, double entry_price)
        {
            if (entry_price <= 0)
                throw new ArgumentException("Entry price must be positive", nameof(entry_price));
                
            double tpPrice = side == Side.Buy 
                ? entry_price * (1 + _tpPercent)  // For Buy: TP above entry price
                : entry_price * (1 - _tpPercent); // For Sell: TP below entry price
                
            return new List<double> { tpPrice };
        }

        public Func<double, double> UpdateSl(double marketData, ITpSlItems item)
        {
            // Fixed strategy doesn't update SL dynamically - returns current SL
            return currentSl => currentSl;
        }

        public Func<double, double> UpdateTp(double marketData, ITpSlItems item)
        {
            // Fixed strategy doesn't update TP dynamically - returns current TP
            return currentTp => currentTp;
        }
    }
}
