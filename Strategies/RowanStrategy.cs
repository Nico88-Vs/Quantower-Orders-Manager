using DivergentStrV0_1.OperationSystemAdv;
using DivergentStrV0_1.OperationSystemAdv.DDDCore;
using DivergentStrV0_1.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.Strategies
{
    internal class RowanStrategy : ConditionableBase<double>
    {

        #region === TRADABLE SESSIONS (UTC) ===
        // Sessione 1
        public bool EnableTradeSession1 { get; set; } = true;
        public string TradeSession1Name { get; set; } = "SESSION-1";
        public List<DayOfWeek> TradeSession1DaysMask { get; set; }
        public string TradeSession1OpenUtc { get; set; } = "08:00";
        public string TradeSession1CloseUtc { get; set; } = "12:00";

        // Sessione 2
        public bool EnableTradeSession2 { get; set; } = true;
        public string TradeSession2Name { get; set; } = "SESSION-2";
        public List<DayOfWeek> TradeSession2DaysMask { get; set; }
        public string TradeSession2OpenUtc { get; set; } = "13:00";
        public string TradeSession2CloseUtc { get; set; } = "17:00";

        // Sessione 3
        public bool EnableTradeSession3 { get; set; } = true;
        public string TradeSession3Name { get; set; } = "SESSION-3";
        public List<DayOfWeek> TradeSession3DaysMask { get; set; }
        public string TradeSession3OpenUtc { get; set; } = "18:00";
        public string TradeSession3CloseUtc { get; set; } = "21:00";
        #endregion

        private Indicator _atrIndicator;
        private Indicator _deltaBaseIndicator;
        private int _maxOpen;
        private double _totalQuantity;
        private double _maxSessionLos;
        private double _startSessionLoss;

        public bool AllowToTrade
        {
            get
            {
                var usdloss = this.Metrics.NetProfit - this._startSessionLoss;
                var maxLoss = usdloss < 0 && Math.Abs(usdloss) >= Math.Abs(_maxSessionLos);
                var sessionActive = StaticSessionManager.CurrentStatus == Status.Active;

                if (maxLoss || !sessionActive)
                    return false;
                else
                    return true;
            }
        }

        public RowanStrategy(Indicator DeltaBaseIndicator, Indicator atrsIndicator, int max_open, double totalquantity, double maxSessionLosUsd) : base()
        {

            this._atrIndicator = atrsIndicator;
            this._deltaBaseIndicator = DeltaBaseIndicator;

            this._maxOpen = max_open;
            this._totalQuantity = totalquantity;
            _maxSessionLos = maxSessionLosUsd;

            //📝 TODO: [Creare le sessioni]

        }


        //📝 TODO: [load async disabilitato]

        public override void Init(HistoryRequestParameters req, Account account, bool loadAsync = false, string description = "", bool allowHeavyMetrics = false)
        {
            base.Init(req, account, false, description, allowHeavyMetrics);
            StaticSessionManager.TradeSessionsStatusChanged += StaticSessionManager_TradeSessionsStatusChanged;
        }


        //📝 TODO: [Logs]
        //📝 TODO: [Non Funziona se viene riavviata la strategia durante la sessione]

        private void StaticSessionManager_TradeSessionsStatusChanged(object sender, Status e)
        {
            if (e == Status.Active)
            {
                // La sessione di trading è attiva
                this._startSessionLoss = this.Metrics.NetProfit;
            }
        }

        protected override List<HistoryUpdAteType> GetUpdateTypes()
        {
            return new List<HistoryUpdAteType>
            {
                HistoryUpdAteType.NewItem,
            };
        }

        public override double SetQuantity() => this._totalQuantity / this._maxOpen;

        public override void Update(object obj)
        {
            try
            {
                HistoryEventArgs item = (HistoryEventArgs)obj;
                HistoryItem data = (HistoryItem)item.HistoryItem;

                StaticSessionManager.Update(data);

                var tradeStatus = StaticSessionManager.CurrentStatus;

                if (this.Metrics.ExposedCount < _maxOpen)
                {
                    //puoi tradare 
                }
            }
            catch (Exception)
            {

                //📝 TODO: [Log]

                throw;
            }
        }
    }

   
}
