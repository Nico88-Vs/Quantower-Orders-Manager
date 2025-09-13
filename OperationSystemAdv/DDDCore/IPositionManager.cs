using System;
using System.Collections.Generic;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv.DDDCore
{
    public interface IPositionManager<T> where T : ITpSlItems
    {
        int TradeCount { get; }
        event EventHandler QuitAll;
        List<T> Items { get; }
        List<T> ClosedItems { get; }

        void Dispose();
        void PlaceEntryOrder(PlaceOrderRequestParameters req, string comment, List<PlaceOrderRequestParameters> sl, List<PlaceOrderRequestParameters> tp, object sender = null);
        void UpdateSl(T item, Func<double, double> updateFunction);
        void UpdateTp(T item, Func<double, double> updateFunction);
        void CreateItem(string comment);
        KeyValuePair<string, OrderTypeSubcomment>? GetSplittedComment(string comment);
        double ExposedAmmount { get; }
    }
}