using DivergentStrV0_1.OperationSystemAdv;
using DivergentStrV0_1.OperationSystemAdv.DDDCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.Utils
{
    /// <summary>
    /// Signleton class to manage the global instance of TpSlManager.
    /// </summary>
    public sealed class GlobalTpSlManagerDue
    {
        private static readonly Lazy<ManagerDue> lazyInstance = new(() => new ManagerDue());

        public static ManagerDue Instance => lazyInstance.Value;

        // Prevent instantiation
        private GlobalTpSlManagerDue() { }
    }

    public class ManagerDue : IDisposable, IPositionManager<TpSlItems2>
    {
        #region Properties 
        public List<TpSlItems2> Items { get; private set; }
        public List<TpSlItems2> ClosedItems { get; private set; }
        private Dictionary<string, List<string>> _itemsDictionary;
        private readonly object _lockObj = new object();
        public event EventHandler QuitAll;
        public int TradeCount
        {
            get
            {
                return Items.Sum(x => x.EntryTrades.Count + x.ExitTrades.Count) + ClosedItems.Sum(y => y.EntryTrades.Count + y.ExitTrades.Count);
            }
        }

        #endregion

        //📝 TODO: [Flow]
        //Implementare controllo sulle posizioni 
        //implementare il quitAll

        public ManagerDue()
        {
            Items = new List<TpSlItems2>();
            ClosedItems = new List<TpSlItems2>();
            _itemsDictionary = new Dictionary<string, List<string>>();

            Core.Instance.OrdersHistoryAdded += Instance_OrdersHistoryAdded;
            Core.Instance.TradeAdded += Instance_TradeAdded;
            Core.Instance.OrderAdded += Instance_OrderAdded;
            Core.Instance.PositionRemoved += Instance_PositionRemoved;
        }

        private void Instance_PositionRemoved(Position obj)
        {
            throw new NotImplementedException();
        }

        private void Instance_OrderAdded(Order obj)
        {
            lock (_lockObj)
            {
                this.CatchOrders(obj);
            }
        }


        private void Instance_TradeAdded(Trade obj)
        {
            lock (_lockObj)
            {
                this.CatchOrders(obj);
            }
        }

        private void Instance_OrdersHistoryAdded(OrderHistory obj)
        {
            lock (_lockObj)
            {
                this.CatchOrders(obj);
            }
        }

        #region 📘 REQ [NEXT]

        public void UpdateSl(TpSlItems2 item, Func<double, double> updateFunction)
        {
            item.UpdateSlOrders(updateFunction);
        }

        public void UpdateTp(TpSlItems2 item, Func<double, double> updateFunction)
        {
            item.UpdateTpOrders(updateFunction);
        }

        // HAndle Dispatcher
        public void PlaceEntryOrder(PlaceOrderRequestParameters req, string comment, List<PlaceOrderRequestParameters> sl, List<PlaceOrderRequestParameters> tp, object sender = null)
        {
            req.Comment = $"{comment}.{OrderTypeSubcomment.Entry.ToString()}";

            var reqest = Core.Instance.PlaceOrder(req);
            var done = false;
            //TODO:Logs

            while (reqest.Status == TradingOperationResultStatus.Success && !done)
            {
                foreach (var slOrder in sl)
                {
                    slOrder.Comment = $"{comment}.{OrderTypeSubcomment.StopLoss.ToString()}";
                    reqest = Core.Instance.PlaceOrder(slOrder);
                }

                foreach (var tpOrder in tp)
                {
                    tpOrder.Comment = $"{comment}.{OrderTypeSubcomment.TakeProfit.ToString()}";
                    reqest = Core.Instance.PlaceOrder(tpOrder);
                }

                done = true;

                //ISSUE: Dispatcher Target is null
                //_dispatcher.Dispatch(new TradingOperations(reqest, sender));
            }

            if (reqest.Status == TradingOperationResultStatus.Success)
            {
                //ISSUE: Dispatcher Target is null
                //_dispatcher.Dispatch(new TradingErrors(reqest, sender));
            }

            else
            {
                //TODO:Logs
                //TODO:Handle failure
            }
        }
        #endregion

#nullable enable
        private TpSlItems2? CatchOrders(Trade trade)
        {
            TpSlItems2? resultItem = null;
            if (trade.Comment != null)
            {
                var splitted = GetSplittedComment(trade.Comment);

                if (splitted is KeyValuePair<string, OrderTypeSubcomment> parsedComment && this.Items.Any(x => x.Id == parsedComment.Key))
                {
                    var result = Items.Where(x => x.Id == parsedComment.Key).SingleOrDefault();

                    resultItem = result;

                    if (result == null)
                        Core.Instance.Loggers.Log("More Then One Item With Same Id", LoggingLevel.Error);

                    else
                    {
                        switch (trade.PositionImpactType)
                        {
                            case PositionImpactType.Open:
                                result.EntryTrades.Add(trade);
                                break;
                            case PositionImpactType.Close:
                                result.ExitTrades.Add(trade);
                                break;
                            default:
                                try
                                {
                                    var obj = trade.Side == result.Side
                                        ? result.EntryTrades
                                        : result.ExitTrades;

                                    obj.Add(trade);
                                }
                                catch (Exception ex)
                                {
                                    Core.Instance.Loggers.Log($"Undefined PositionImpactType Error: {ex.Message}", LoggingLevel.Error);
                                    throw;
                                }
                                break;
                        }
                    }
                }
            }

            else
            {
                int finded = -1;

                while (finded < 0)
                {
                    foreach (var item in Items)
                    {
                        var result = item.TryUpdateTrade(trade);
                        if (result != null)
                        {
                            try
                            {
                                var obj = trade.Side == result.Side
                                    ? result.EntryTrades
                                    : result.ExitTrades;

                                obj.Add(trade);
                                result.TryUpdateStatus();
                                finded = 1;
                            }
                            catch (Exception ex)
                            {
                                Core.Instance.Loggers.Log($"Undefined PositionImpactType Error: {ex.Message}", LoggingLevel.Error);
                                throw;
                            }
                            break;
                        }
                    }

                    finded = 0;
                }
            }

            if (resultItem != null && resultItem.Position == null && trade.PositionId != null)
                resultItem.SetPosition(Core.Instance.Positions.Where(x => x.Id == trade.PositionId).SingleOrDefault());



            return resultItem;
        }
        private TpSlItems2? CatchOrders(Order order)
        {
            TpSlItems2? resultItem = null;
            if (order.Comment != null)
            {
                var splitted = GetSplittedComment(order.Comment);

                if (splitted is KeyValuePair<string, OrderTypeSubcomment> parsedComment)
                {
                    if (!this.Items.Any(x => x.Id == parsedComment.Key))
                        this.CreateItem(parsedComment.Key);

                    var result = Items.Where(x => x.Id == parsedComment.Key).SingleOrDefault();

                    resultItem = result;

                    if (result == null)
                        Core.Instance.Loggers.Log("More Then One Item With Same Id", LoggingLevel.Error);

                    else
                    {
                        switch (parsedComment.Value)
                        {
                            case OrderTypeSubcomment.StopLoss:
                                var i = result.SlOrders.FindIndex(x => x.Id == order.Id);
                                if (i >= 0)
                                    result.SlOrders[i] = order;
                                else
                                    result.AttachSlOrder(order);
                                break;
                            case OrderTypeSubcomment.TakeProfit:
                                var z = result.TpOrders.FindIndex(x => x.Id == order.Id);
                                if (z >= 0)
                                    result.TpOrders[z] = order;
                                else
                                    result.AttachTpOrder(order);
                                break;
                            case OrderTypeSubcomment.Entry:
                                result.AttachEntryOrder(order);
                                break;
                            default:
                                Core.Instance.Loggers.Log("Unknown Subcomment Type", LoggingLevel.Error);
                                break;
                        }
                    }


                }
            }

            else
            {
                int finded = -1;

                while (finded < 0)
                {
                    foreach (var item in Items)
                    {
                        var obj = item.TryUpdateOrder(order);
                        if (obj != null)
                        {
                            finded = 1;
                            if (resultItem == null)
                                resultItem = obj;
                            break;
                        }
                    }

                    finded = 0;
                }
            }

            if (resultItem != null && resultItem.Position == null && order.PositionId != null)
                resultItem.SetPosition(Core.Instance.Positions.Where(x => x.Id == order.PositionId).SingleOrDefault());

            return resultItem;
        }

        private TpSlItems2? CatchOrders(OrderHistory order)
        {
            TpSlItems2? resultItem = null;
            if (order.Comment != null)
            {
                var splitted = GetSplittedComment(order.Comment);

                if (splitted is KeyValuePair<string, OrderTypeSubcomment> parsedComment && this.Items.Any(x => x.Id == parsedComment.Key))
                {
                    var result = Items.Where(x => x.Id == parsedComment.Key).SingleOrDefault();

                    resultItem = result;

                    if (result == null)
                        Core.Instance.Loggers.Log("More Then One Item With Same Id", LoggingLevel.Error);

                    else
                    {
                        switch (parsedComment.Value)
                        {
                            case OrderTypeSubcomment.StopLoss:
                                result.SlOrdersHistory.Add(order);
                                break;
                            case OrderTypeSubcomment.TakeProfit:
                                result.TpOrdersHistory.Add(order);
                                break;
                            case OrderTypeSubcomment.Entry:
                                result.EntryOrdersHistory.Add(order);
                                break;
                            default:
                                Core.Instance.Loggers.Log("Unknown Subcomment Type", LoggingLevel.Error);
                                break;
                        }
                    }
                }
            }

            else
            {
                int finded = -1;

                while (finded < 0)
                {
                    foreach (var item in Items)
                    {
                        var obj = item.TryUpdateOrder(order);
                        if (obj != null)
                        {
                            finded = 1;
                            if (resultItem == null)
                                resultItem = obj;
                            break;
                        }
                    }

                    finded = 0;
                }
            }

            if (resultItem != null && resultItem.Position == null && order.PositionId != null)
                resultItem.SetPosition(Core.Instance.Positions.Where(x => x.Id == order.PositionId).SingleOrDefault());

            return resultItem;
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

        public void Dispose()
        {
            try
            {
                foreach (var item in Items)
                {
                    item.ItemClosed -= Item_ItemClosed;
                    item.QuitAll -= Item_QuitAll;
                }

            }
            catch (Exception)
            {

                throw;
            }

            //TODO: chiudere tutte le posizioni
            Items = null;
            ClosedItems = null;
            _itemsDictionary = null;

            Core.Instance.OrderAdded -= this.Instance_OrderAdded;
            Core.Instance.TradeAdded -= this.Instance_TradeAdded;
            Core.Instance.OrderAdded -= Instance_OrderAdded;



        }

        public void CreateItem(string comment)
        {
            if (!_itemsDictionary.ContainsKey(comment))
            {
                var item = new TpSlItems2(comment);
                Items.Add(item);
                item.ItemClosed += Item_ItemClosed;
                item.QuitAll += Item_QuitAll;
                _itemsDictionary.Add(item.Id, new List<string>());

            TODO: Core.Instance.Loggers.Log($"✅ Creato nuovo SlTpItems con ID: {comment}", LoggingLevel.System);
            }
            else
            {
            TODO: Core.Instance.Loggers.Log($"⚠️ Item con ID {comment} già esistente, non ricreato.", LoggingLevel.Error);
            }
        }

        private void Item_QuitAll(object? sender, EventArgs e)
        {
            this.QuitAll?.Invoke(this, EventArgs.Empty);
        }

        private void Item_ItemClosed(object? sender, PositionManagerStatus[] e)
        {
            lock (_lockObj)
            {
                TpSlItems2 item = sender as TpSlItems2;
                try
                {
                    if (!ClosedItems.Any(x => x.Id == item.Id))
                    {
                        ClosedItems.Add(item);
                        Items.Remove(item);
                        item.ItemClosed -= Item_ItemClosed;
                    TODO: Core.Instance.Loggers.Log($"✅ Item chiuso e spostato in ClosedItems: {item.Id}", LoggingLevel.System);
                    }
                    else
                    {
                        //TODO:Core.Instance.Loggers.Log($"⚠️ Tentativo di chiudere un item già chiuso: {item.Id}", LoggingLevel.Warning);
                    }
                }
                catch (Exception ex)
                {
                    Core.Instance.Loggers.Log($"Errore durante la chiusura dell'item: {ex.Message}", LoggingLevel.Error);
                    throw;
                }

            }
        }
    }
}
