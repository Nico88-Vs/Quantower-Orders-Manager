using System;
using System.Collections.Generic;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv.DDDCore
{
    public interface ITpSlItems
    {
        double ClosedQuantity { get; }
        Order EntryOrder { get; }
        List<Trade> EntryTrades { get; }
        List<Trade> ExitTrades { get; }
        bool Exposed { get; }
        double ExposedQuantity { get; }
        double Fees { get; }
        double FilledQuantity { get; }
        double GrossProfit { get; }
        string Id { get; }
        double NetProfit { get; }
        Position Position { get; }
        double Quantity { get; }
        Side Side { get; }
        PositionManagerStatus Status { get; }
        Symbol Symbol { get; set; }

        event EventHandler<PositionManagerStatus[]> ItemClosed;
        event EventHandler QuitAll;
        void Quit();
        void TryUpdateStatus();
        TpSlItems2 TryUpdateTrade(Trade trade);
        void UpdateSlOrders(Func<double, double> updateFunction);
        void UpdateTpOrders(Func<double, double> updateFunction);
    }
}