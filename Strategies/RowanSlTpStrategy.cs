using DivergentStrV0_1.OperationSystemAdv;
using System;
using System.Collections.Generic;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.Strategies
{
    public struct SlTpData
    {
        public double[] sessionsHighs { get; set; }
        public double[] sessionsLows { get; set; }

        public Symbol Symbol { get; set; }

        public double TriggerPrice { get; set; }
        public double currentPrice { get; set; }
    }

    internal class RowanSlTpStrategy : ISlTpStrategy<SlTpData>
    {
        public int max_slInTicks { get; set; }
        public int min_slInTicks { get; set; }
        public int MinTpInTicks { get; set; }
        private int delta_InTicks;

        public RowanSlTpStrategy(int min_Tick, int max_Tick)
        {
            if (min_Tick <= 0)
                throw new ArgumentException("Min ticks must be positive", nameof(min_Tick));
            if (max_Tick <= 0)
                throw new ArgumentException("Max ticks must be positive", nameof(max_Tick));
            if (min_Tick > max_Tick)
                throw new ArgumentException("Min ticks cannot be greater than max ticks");
                
            this.max_slInTicks = max_Tick;
            this.min_slInTicks = min_Tick;
            delta_InTicks = Math.Abs(min_Tick - max_Tick); 
        }

        public List<double> CalculateSl(SlTpData marketData, Side side, double entry_price)
        {
            var sl_temp = marketData.Symbol.CalculateTicks(entry_price, marketData.TriggerPrice);
            
            var sl = Math.Abs(sl_temp) > max_slInTicks ? max_slInTicks : sl_temp;

            double sl_price  = side == Side.Buy ? marketData.Symbol.CalculatePrice(entry_price , -sl) :
                marketData.Symbol.CalculatePrice(entry_price, + sl);

            return new List<double> { sl_price };
            
        }

        public List<double> CalculateTp(SlTpData marketData, Side side, double entry_price)
        {
            throw new NotImplementedException();
        }
        public Func<double, double> UpdateSl(SlTpData marketData, SlTpItems item)
        {
            try
            {
                return current_sl =>
                {
                    var delta = item.Symbol.CalculateTicks(current_sl, marketData.currentPrice);
                    bool isOut = delta > this.delta_InTicks;

                    if (!isOut)
                        return current_sl;

                    // ramo “isOut”: gestisci tutti i casi
                    return item.Side switch
                    {
                        Side.Buy => item.Symbol.CalculatePrice(marketData.currentPrice, -max_slInTicks),
                        Side.Sell => item.Symbol.CalculatePrice(marketData.currentPrice, +max_slInTicks),
                        _ => current_sl // default: evita il buco di ritorno
                    };
                };
            }
            catch (Exception)
            {
                // log se vuoi…
                return current_sl => current_sl;   // scegli RETURN...
                                                   // throw;                          // ...oppure THROW (togli il return sopra)
            }
        }

        public Func<double, double> UpdateTp(SlTpData marketData, SlTpItems item)
        {
            throw new NotImplementedException();
        }
    }
}
