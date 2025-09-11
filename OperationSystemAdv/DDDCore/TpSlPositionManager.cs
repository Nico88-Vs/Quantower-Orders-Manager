using System;
using System.Collections.Generic;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv.DDDCore
{
    public class TpSlPositionManager : IPositionManager<TpSlItemPosition>
    {
        public int TradeCount => throw new NotImplementedException();

        public List<TpSlItemPosition> Items { get; private set; }

        public List<TpSlItemPosition> ClosedItems { get; private set; }

        public event EventHandler QuitAll;

        private readonly object _lock = new object();
        private Symbol _symbol;
        private Account _account;
        private bool _isInitialized = false;
        private Dictionary<string, List<string>> _itemsDictionary;

        public TpSlPositionManager()
        {
            Items = new List<TpSlItemPosition>();
            ClosedItems = new List<TpSlItemPosition>();

            _itemsDictionary = new Dictionary<string, List<string>>();

            Core.Instance.PositionAdded += Instance_PositionAdded;
            Core.Instance.ClosedPositionAdded += Instance_ClosedPositionAdded;
            Core.Instance.OrderAdded += Instance_OrderAdded; ;
        }

        private void Instance_OrderAdded(Order obj)
        {
            lock (_lock)
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

        private void Instance_ClosedPositionAdded(ClosedPosition obj)
        {
            lock (_lock)
            {
                if (obj.Symbol == _symbol && obj.Account == _account)
                {
                    var item = this.Items.FirstOrDefault(x => x.Side == obj.Side && x.Position.Id == obj.Id);
                    if (item != null)
                    {
                        this.Items.Remove(item);
                        this.ClosedItems.Add(item);
                    }
                }
            }
        }

        private void Instance_PositionAdded(Position obj)
        {
            lock (_lock)
            {
                if (obj.Symbol == _symbol && obj.Account == _account)
                {
                    CatchPosition(obj);
                }
            }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void PlaceEntryOrder(PlaceOrderRequestParameters req, string comment, List<PlaceOrderRequestParameters> sl, List<PlaceOrderRequestParameters> tp, object sender = null)
        {
            if (!_isInitialized)
            {
                this._symbol = req.Symbol;
                this._account = req.Account;
                this._isInitialized = true;

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
            }

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
                var item = this.Items.FirstOrDefault(x => x.Side == pos.Side && x.EntryOrder != null && x.Position == null);
                item.SetPosition(pos);
            }
            catch (Exception)
            {
                Core.Instance.Loggers.Log($"Errore nel settare la posizione per l'item {pos.Comment}", LoggingLevel.Error);
                throw;
            }
        }

        public void UpdateSl(TpSlItemPosition item, Func<double, double> updateFunction)
        {
            throw new NotImplementedException();
        }

        public void UpdateTp(TpSlItemPosition item, Func<double, double> updateFunction)
        {
            throw new NotImplementedException();
        }

        public void CreateItem(string comment)
        {
            if (!_itemsDictionary.ContainsKey(comment))
            {
                var item = new TpSlItemPosition(comment);
                Items.Add(item);
                _itemsDictionary.Add(item.Id, new List<string>());

                Core.Instance.Loggers.Log($"✅ Creato nuovo SlTpItems con ID: {comment}", LoggingLevel.System);
            }
            else
            {
                Core.Instance.Loggers.Log($"⚠️ Item con ID {comment} già esistente, non ricreato.", LoggingLevel.Error);
            }
        }

        public KeyValuePair<string, OrderTypeSubcomment>? GetSplittedComment(string comment)
        {
            try
            {
                var splittedcomment = comment.Split('.');

                if (splittedcomment.Length == 2)
                {
                    var success = Enum.TryParse<OrderTypeSubcomment>(splittedcomment[1], out OrderTypeSubcomment type);
                    if (success)
                    {
                        return new KeyValuePair<string, OrderTypeSubcomment>(splittedcomment[0], type);
                    }
                    else
                    {
                        // TODO: LOGS
                        return null;
                    }
                }
                else
                {
                    // TODO: LOGS
                    return null;
                }
            }
            catch (Exception)
            {
                // TODO: LOGS
                return null;
            }
        }
    }
}
