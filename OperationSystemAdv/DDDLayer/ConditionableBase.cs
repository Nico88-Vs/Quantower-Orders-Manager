using DivergentStrV0_1.OperationSystemAdv.DDDCore;
using DivergentStrV0_1.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Integration;

namespace DivergentStrV0_1.OperationSystemAdv
{
    //REQ : Keep it Updated

    public abstract class ConditionableBase<T> : IConditionable , IDisposable
    {
        protected List<OrderType> _allowedOrdersType;
        public virtual ManagerDue _manager { get; private set; } = GlobalTpSlManagerDue.Instance;

        public virtual PerformanceMetrics Metrics { get; private set; }

        public Account Account { get; private set; }
        public bool Initialized { get; private set; }
        public Symbol Symbol { get; private set; }
        //HACK: definito cosi la gestione delle quantita 
        public double Quantity { get; private set; }
        public string StrategyName => this.GetType().Name;
        public string Description { get; private set; }
        public ISlTpStrategy<T> Strategy { get; private set; }
        public HystoryDataProvider HistoryProvider { get; protected set; }

        public List<string> RegistredGuid => new();

        protected ConditionableBase()
        {
            this.Metrics = new PerformanceMetrics();
            this.Metrics.SetStrategyTag(this.StrategyName);
            this.Initialized = false;
            this.Metrics.EnableHeavyMetrics = true;
        }

        public virtual void Init(HistoryRequestParameters req,Account account, bool loadAsync, string description = "", bool allowHeavyMetrics = false)
        {
            this.Account = account;
            this.Metrics.SetPerformanceMetrics(allowHeavyMetrics, this.StrategyName, this.Account);
            this.Description = description;
            this.Symbol = req.Symbol;
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
            //📝 TODO: [HIGH] Implementare registrazione completa degli handlers
            //📝 TODO: [MEDIUM] Riattivare Dispatcher.Register quando disponibile
            //📝 TODO: [LOW] Aggiungere handlers per eventi di mercato
        }

        public virtual void Trade(Side side, double price, T slMarketData, T tpMarketData)
        {
            if (price <= 0)
                throw new ArgumentException("Price must be positive", nameof(price));
            if (this.Strategy == null)
                throw new InvalidOperationException("Strategy must be injected before trading");
                
            //📝 TODO: [HIGH] Aggiungere validazione slMarketData e tpMarketData non null
            //📝 TODO: [HIGH] Verificare che Account e Symbol siano inizializzati
            //📝 TODO: [MEDIUM] Aggiungere pre-trade risk checks (max exposure, daily loss limit)
                
            var comment = GenerateComment();
            this.RegistredGuid.Add(comment);

            //📝 TODO: [CRITICAL] Finire l implementazione di PlaceOrderRequestParameters
            //📝 TODO: [HIGH] Rimuovere ridondanza di comment
            //📝 TODO: [HIGH] Implementare slippage calculation basato su ATR
            var ord_Request = new PlaceOrderRequestParameters
            {
                Account = this.Account,
                Symbol = this.Symbol,
                Side = side,
                Quantity = price > 0 ? this.RoundQuantity(Quantity/price) : 0,
                Price = price,
                TriggerPrice = price,
                OrderTypeId = Symbol.GetAlowedOrderTypes(OrderTypeUsage.Order).FirstOrDefault(x => x.Behavior == OrderTypeBehavior.Market).Id,
                Comment = comment
            };

            var limit = _allowedOrdersType.FirstOrDefault(x => x.Behavior == OrderTypeBehavior.Limit);
            var stop = _allowedOrdersType.FirstOrDefault(x => x.Behavior == OrderTypeBehavior.Stop);
            if (limit == null)
            {
                //📝 TODO: [CRITICAL] Handle this scenario - implementare gestione completa order types
                //📝 TODO: [HIGH] Aggiungere fallback per missing order types
                //📝 TODO: [HIGH] Implementare trigger price management corretto
            }

            //📝 TODO: [HIGH] Aggiungere logging per ogni trade attempt
            //📝 TODO: [MEDIUM] Validare che SL e TP siano calcolati correttamente
            var sl = Strategy.CalculateSl(slMarketData, side, price);
            var slReqests = this.HandleExitReq(sl, ord_Request, stop);

            var tp = Strategy.CalculateTp(tpMarketData, side, price);
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
            //📝 TODO: [CRITICAL] Fixare: I prezzi d'uscita sono sbagliati - verificare calcoli SL/TP
            //📝 TODO: [HIGH] Validare che prices non sia null o vuoto
            //📝 TODO: [HIGH] Gestire correttamente unmatching quantity per multiple exit orders
            
            List<PlaceOrderRequestParameters> collection = new List<PlaceOrderRequestParameters>();
            try
            {
                foreach (var item in prices)
                {
                    //📝 TODO: [MEDIUM] Aggiungere validazione che item sia un prezzo valido
                    PlaceOrderRequestParameters exitReq = new PlaceOrderRequestParameters
                    {
                        Account = origin.Account,
                        Symbol = origin.Symbol,
                        Side = origin.Side == Side.Buy ? Side.Sell : Side.Buy,
                        //📝 TODO: [HIGH] Implementare gestione corretta quantity per multiple exits
                        Quantity = prices.Count > 0 ? this.RoundQuantity(origin.Quantity/prices.Count) : 0,
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
                //📝 TODO: [HIGH] Implementare logging dettagliato per errori exit order creation
                //📝 TODO: [MEDIUM] Considerare se restituire collection vuota o rilanciare eccezione
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
            this.Description = null;
            this.Quantity = 0;
            this._manager?.Dispose();
        }

        private List<TpSlItems2> GetActiveGuid()
        {
            return _manager.Items.Where(item => RegistredGuid.Contains(item.Id)).ToList(); 
        }

        /// <summary>
        /// Chiude tutte le posizioni aperte associate alla strategia e apre una nuova
        /// posizione nel verso opposto con gli stessi parametri di ingresso/uscita.
        /// </summary>
        /// <param name="side">Direzione della nuova posizione da aprire.</param>
        /// <param name="price">Prezzo di mercato utilizzato per chiudere e riaprire.</param>
        /// <param name="slMarketData">Dati per il calcolo dello stop loss.</param>
        /// <param name="tpMarketData">Dati per il calcolo del take profit.</param>
        public virtual void ReversePosition(Side side, double price, T slMarketData, T tpMarketData)
        {

            #region 🐞 BUG [POSSIBILE BUG]
            // stiamo chiudendo tutto e notificando la chiusura ma nn sappiamo l effettivo stato della posizione ne l esito del ordine di chiusura
            #endregion

            // Chiude tutte le posizioni aperte cancellando gli ordini associati
            foreach (var item in GetActiveGuid())
            {
                var closeReq = new PlaceOrderRequestParameters
                {
                    Account = this.Account,
                    Symbol = this.Symbol,
                    Side = item.Side == Side.Buy ? Side.Sell : Side.Buy,
                    Quantity = this.RoundQuantity(item.Quantity - item.ClosedQuantity),
                    Price = price,
                    TriggerPrice = price,
                    OrderTypeId = Symbol.GetAlowedOrderTypes(OrderTypeUsage.Order)
                        .FirstOrDefault(x => x.Behavior == OrderTypeBehavior.Market).Id,
                    Comment = $"{item.Id}.{OrderTypeSubcomment.StopLoss}",
                    AdditionalParameters = new List<SettingItem>
                    {
                        new SettingItemBoolean(OrderType.REDUCE_ONLY, true)
                    }
                };

                // annulla tutti gli ordini collegati alla posizione
                item.Quit();
                // invia un ordine a mercato per chiudere la posizione
                Core.Instance.PlaceOrder(closeReq);
            }

            // apre la nuova posizione nel verso opposto
            this.Trade(side, price, slMarketData, tpMarketData);
        }

        public void UpdateSlTp(T marketData, bool isSl)
        {
            try
            {
                //📝 TODO: [HIGH] Aggiungere validazione marketData non null
                //📝 TODO: [MEDIUM] Ottimizzare: evitare multiple chiamate a GetActiveGuid()
                //📝 TODO: [LOW] Considerare parametro enum invece di bool per isSl

                if (!this.Initialized)
                {
                    throw new InvalidOperationException("ConditionableBase is not initialized. Call Init() before updating SL/TP.");
                }

                if (this._manager == null)
                {
                    throw new InvalidOperationException("TpSlManager is not initialized.");
                }

                if (this._manager.Items.Count == 0)
                    return;

                if (this.Strategy == null)
                {
                    throw new InvalidOperationException("Strategy is not initialized.");
                }

                //📝 TODO: [HIGH] Aggiungere try-catch per gestire errori durante update
                //📝 TODO: [MEDIUM] Aggiungere logging per ogni SL/TP update
                if (isSl)
                {
                    foreach (TpSlItems2 item in GetActiveGuid())
                    {
                        //📝 TODO: [HIGH] Verificare che UpdateSl non lanci NotImplementedException
                        _manager.UpdateSl(item, this.Strategy.UpdateSl(marketData, item));
                    }
                }
                else
                {
                    foreach (TpSlItems2 item in GetActiveGuid())
                    {
                        //📝 TODO: [CRITICAL] UpdateTp attualmente lancia NotImplementedException - fixare
                        _manager.UpdateTp(item, this.Strategy.UpdateTp(marketData, item));
                    }
                }
            }
            catch (Exception ex)
            {
                Core.Instance.Loggers.Log($"Error updating {(isSl ? "SL" : "TP")}: {ex.Message}");
                throw;
            }
            
        }

        #endregion


    }

}
