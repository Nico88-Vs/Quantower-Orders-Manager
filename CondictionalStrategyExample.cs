using System;
using System.Collections.Generic;
using System.Linq;
using DivergentStrV0_1.OrdersManagerClasses;
using TpSlManager;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1
{
    /// <summary>
    /// Represents an example strategy that trades based on moving averages and Fibonacci levels.
    /// Inherits from ConditionableBase and implements specific conditions for trading.
    /// </summary>
    public class FirstStrategyCondiction : ConditionableBase<Indicator>
    {
        public Indicator AtrIndicator { get; set; }

        #region Lifecycle
        /// <summary>
        /// Initializes a new instance of the CondictionalStrategyExample class.
        /// </summary>
        /// <param name="account">The trading account used for the strategy.</param>
        /// <param name="symbol">The trading symbol the strategy operates on.</param>
        /// <param name="quantity">The quantity of assets to trade.</param>
        public FirstStrategyCondiction(Indicator atrIndicator, Account account, Symbol symbol, double quantity, int maxShortExpo = 1, int maxLongExpo = 1) 
            : base(account , symbol, quantity, maxShortExpo, maxLongExpo)
        {
            this.AtrIndicator = atrIndicator;
            this.SetCondictionHolder();
            base.ManagerInit();
        }

        /// <summary>
        /// Here Unscribe Events if required
        /// </summary>
        public override void Close()
        {
            base.Close();
        }
        public override void GetMetrics() => throw new NotImplementedException();

        /// <summary>
        /// Sets the condition holder with the necessary Stop Loss and Take Profit logic.
        /// </summary>
        public override void SetCondictionHolder() => this.InitializeCondictionHolder(new Indicator[1] { this.AtrIndicator }, new Indicator[1] { this.AtrIndicator }, new SlTpCondictionHolder<Indicator>.DefineSl[1] { this.GetSl }, new SlTpCondictionHolder<Indicator>.DefineTp[1] { this.GeTp });

        public override void Update(object obj)
        {
            if (TpSlManager<Indicator>.SlTpItems.Count > 0)
                this.CondictionHolder.Computator.UpdateOrder(TpSlManager<Indicator>.SlTpItems);
        }
        #endregion
        #region Trading Condiction

        /// <summary>
        /// Calculates the Stop Loss based on the provided indicator and the unique order ID.
        /// </summary>
        /// <param name="indicator">The indicator used to calculate the SL value.</param>
        /// <param name="guid">The unique ID for the SlTpItems.</param>
        /// <returns>The calculated SL value.</returns>
        public override double GetSl(Indicator indicator, string guidOrdersReference)
        {
            SlTpItems sltpitem = GetSlTpItemById(guidOrdersReference);
            Side s = sltpitem.Side;
            return 0;
           
        }

        /// <summary>
        /// Calculates the Take Profit based on the provided indicator and the unique order ID.
        /// </summary>
        /// <param name="indicator">The indicator used to calculate the TP value.</param>
        /// <param name="guid">The unique ID for the SlTpItems.</param>
        /// <returns>The calculated TP value.</returns>
        public override double GeTp(Indicator indicator, string guidOrdersReference)
        {
            SlTpItems sltpitem = GetSlTpItemById(guidOrdersReference);
            Side s = sltpitem.Side;
            List<double> ls = new List<double>();
            double resoult = 0;

            foreach (LineSeries line in indicator.LinesSeries)
            {
                //if (Computator.CloudLineIndex.Contains(Array.IndexOf(indicator.LinesSeries, line)))
                //{
                //    ls.Add(line.GetValue());
                //}

            }

            switch (s)
            {
                case Side.Buy:
                    resoult = this.GetClosest(ls, sltpitem.EntryPrice, false);
                    break;
                case Side.Sell:
                    resoult = this.GetClosest(ls, sltpitem.EntryPrice, true);
                    break;
            }

            return resoult;
        }
        #endregion

        #region Utils
        private double GetClosest(List<double> ls, double entryprice, bool isDown)
        {
            List<double> selected = new List<double>();

            if (isDown)
            {
                selected = ls.Where(x => x < entryprice).ToList();
                selected.OrderByDescending(x => x);
            }
            else
            {
                ls.Where(x => x > entryprice).ToList();
                selected.OrderBy(x => x);
            }

            if (selected.Count > 0) 
                return selected.First();
            else
            {
                if (isDown) return entryprice * 0.99;
                else return entryprice * 1.01;
            }
        }
        #endregion
    }
}
