using System;
using System.Collections.Generic;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv.DDDCore
{
    public class TpSlPositionManager : PositionManagerBase<TpSlItemPosition>
    {
        public override int TradeCount => throw new NotImplementedException();


        private Symbol _symbol;
        private Account _account;
        private bool _isInitialized = false;
        public override event EventHandler QuitAll;
        public override double ExposedAmmount
        {
            get
            {
                #region 🐞 BUG [Bug noto da risolvere #3] 
                //BUG #3 costruzione ciclica di ordini
                #endregion
                return Items.Sum(x => x.EntryOrder.TotalQuantity);
            }
        }

        public TpSlPositionManager()
        {
            Core.Instance.PositionAdded += Instance_PositionAdded;
            Core.Instance.OrderAdded += Instance_OrderAdded;

            #region 🧪 HACK [Soluzione temporanea]
            // Controllo lo stato degli altri eventi Qt
            Core.Instance.OrderRemoved += (e) =>
            {
                Order order = e as Order;
            };

            Core.Instance.PositionRemoved += (obj) =>
            {

                #region 🧪 HACK [Possibile bug fix]
                // SDpostato da position removed 
                // Elimino tutti gli item correlati
                #endregion


                #region 🐞 BUG [Bug noto da risolvere]
                //Il processo di rimozione viene interrotto dagli eventi qt , provo a rimuovere il lock
                #endregion


                //lock (_lockObj)
                //{
                    if (obj.Symbol == _symbol && obj.Account == _account)
                    {
                        var item = this.Items.Where(x => x.Side == obj.Side && x.Position.Id == obj.Id);
                        if (item.Any())
                        {
                            foreach (var i in item)
                            {
                                //📝 TODO: [Da completare] gestire la chiusura degli ordiri relativo al bug noto di ciclicita di inserimento ordini
                                //this.Items.Remove(i);
                                //this.ClosedItems.Add(i);
                                i.TryUpdateStatus();
                            }
                        }
                    }
                //}
            };
            #endregion

        }

        private void Instance_OrderAdded(Order obj)
        {
            lock (_lockObj)
            {
                CatchOrders(obj);
            }
        }

        private void CatchOrders(Order order)
        {
            if (order.Comment != null)
            {
                var splitted = GetSplittedComment(order.Comment);

                if (splitted is KeyValuePair<string, OrderTypeSubcomment> parsedComment)
                    if (!this.Items.Any(x => x.Id == parsedComment.Key))
                    {
                        this.CreateItem(parsedComment.Key);
                        try
                        {
                            this.Items.FirstOrDefault(x => x.Id == this._itemsDictionary.Keys.Last()).SetEntryOrder(order);
                        }
                        catch (Exception ex)
                        {
                            Core.Instance.Loggers.Log($"Errore nel settare l'entry order per l'item {this._itemsDictionary.Keys.Last()}: {ex.Message}", LoggingLevel.Error);
                            throw;
                        }
                    }
            }
        }

        private void Instance_PositionAdded(Position obj)
        {
            lock (_lockObj)
            {
                if (obj.Symbol == _symbol && obj.Account == _account)
                {
                    CatchPosition(obj);
                }
            }
        }

        public override void Dispose()
        {
            throw new NotImplementedException();
        }

        public override void PlaceEntryOrder(PlaceOrderRequestParameters req, string comment, List<PlaceOrderRequestParameters> sl, List<PlaceOrderRequestParameters> tp, object sender = null)
        {
            if (!_isInitialized)
            {
                this._symbol = req.Symbol;
                this._account = req.Account;
                this._isInitialized = true;
            }


            #region 🐞 BUG [Bug noto da risolvere]
            // BUG Vengono eseguiti ingressi multipli anche se gli item sono gia registrati 
            #endregion


            PlaceOrderRequestParameters orderobj = req;
            orderobj.Comment = $"{comment}.{OrderTypeSubcomment.Entry.ToString()}";

            foreach (var item in sl)
            {
                var slobj = SlTpHolder.CreateSL(item.Price);
                orderobj.StopLossItems.Add(slobj);
            }

            foreach (var item in tp)
            {
                var tpobj = SlTpHolder.CreateTP(item.Price);
                orderobj.TakeProfitItems.Add(tpobj);
            }

            var result = Core.Instance.PlaceOrder(orderobj);

            if ( result.Status == TradingOperationResultStatus.Success)
                Core.Instance.Loggers.Log($"✅ Entry order placed successfully with comment: {orderobj.Comment}", LoggingLevel.System);
            else
                Core.Instance.Loggers.Log($"❌ Failed to place entry order with comment: {orderobj.Comment}. Reason: {result.Message}", LoggingLevel.Error);

            if (!this._isInitialized)
            {
                Core.Instance.Loggers.Log("TpSlPositionManager is not initialized.", LoggingLevel.Error);
                return;
            }
        }

        private void CatchPosition(Position pos)
        {
            try
            {

                #region 🐞 BUG [possibile bug]
                //null exeption
                #endregion

                var item = this.Items.FirstOrDefault(x => x.Side == pos.Side && x.EntryOrder != null && x.Position == null);
                if (item != null)
                    item.SetPosition(pos);
            }
            catch (Exception)
            {
                Core.Instance.Loggers.Log($"Errore nel settare la posizione per l'item {pos.Comment}", LoggingLevel.Error);
                throw;
            }
        }

        public override void UpdateSl(TpSlItemPosition item, Func<double, double> updateFunction)
        {
            throw new NotImplementedException();
        }

        public override void UpdateTp(TpSlItemPosition item, Func<double, double> updateFunction)
        {
            throw new NotImplementedException();
        }

        public override void CreateItem(string comment)
        {
            if (!_itemsDictionary.ContainsKey(comment))
            {
                base.CreateItem(comment);
                this.Items.Last().ItemClosed += this.TpSlPositionManager_ItemClosed;
                Core.Instance.Loggers.Log($"✅ Creato nuovo SlTpItems con ID: {comment}", LoggingLevel.System);
            }
            else
            {
                Core.Instance.Loggers.Log($"⚠️ Item con ID {comment} già esistente, non ricreato.", LoggingLevel.Error);
            }
        }

        private void TpSlPositionManager_ItemClosed(object sender, PositionManagerStatus[] e)
        {
            var item = sender as TpSlItemPosition;
            if (item != null)
            {
                item.ItemClosed -= this.TpSlPositionManager_ItemClosed;
                this.Items.Remove(item);
                this.ClosedItems.Add(item);
                Core.Instance.Loggers.Log($"✅ Item con ID {item.Id} chiuso e spostato in ClosedItems.", LoggingLevel.System);
            }

        }
        protected override TpSlItemPosition CreateNewItem(string comment) => new TpSlItemPosition(comment);

    }
}
