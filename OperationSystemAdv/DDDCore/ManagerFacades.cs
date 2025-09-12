using System;
using System.Collections.Generic;
using System.Linq;
using DivergentStrV0_1.Utils;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv.DDDCore
{
    public enum ManagerType
    {
        OrdersHistoryBased, // ManagerDue (TpSlItems2)
        LegacyOrdersBased,  // TpSlManager (SlTpItems)
        PositionBased       // TpSlPositionManager (TpSlItemPosition)
    }

    public interface IManagerFacade : IDisposable
    {
        IReadOnlyList<ITpSlItems> Items { get; }
        IReadOnlyList<ITpSlItems> ClosedItems { get; }
        int TradeCount { get; }

        void PlaceEntryOrder(PlaceOrderRequestParameters req, string comment,
            List<PlaceOrderRequestParameters> sl, List<PlaceOrderRequestParameters> tp, object sender = null);

        void UpdateSl(ITpSlItems item, Func<double, double> updateFunction);
        void UpdateTp(ITpSlItems item, Func<double, double> updateFunction);
    }

    internal sealed class ManagerDueFacade : IManagerFacade
    {
        private readonly Utils.ManagerDue _inner;
        public ManagerDueFacade(Utils.ManagerDue inner)
        {
            _inner = inner;
        }

        public IReadOnlyList<ITpSlItems> Items => _inner.Items.Cast<ITpSlItems>().ToList();
        public IReadOnlyList<ITpSlItems> ClosedItems => _inner.ClosedItems.Cast<ITpSlItems>().ToList();
        public int TradeCount => _inner.TradeCount;

        public void PlaceEntryOrder(PlaceOrderRequestParameters req, string comment, List<PlaceOrderRequestParameters> sl, List<PlaceOrderRequestParameters> tp, object sender = null)
            => _inner.PlaceEntryOrder(req, comment, sl, tp, sender);

        public void UpdateSl(ITpSlItems item, Func<double, double> updateFunction)
            => _inner.UpdateSl((TpSlItems2)item, updateFunction);

        public void UpdateTp(ITpSlItems item, Func<double, double> updateFunction)
            => _inner.UpdateTp((TpSlItems2)item, updateFunction);

        public void Dispose() => _inner.Dispose();
    }

    internal sealed class TpSlManagerFacade : IManagerFacade
    {
        private readonly OperationSystemAdv.TpSlManager _inner;
        public TpSlManagerFacade(OperationSystemAdv.TpSlManager inner)
        {
            _inner = inner;
        }

        public IReadOnlyList<ITpSlItems> Items => _inner.Items.Cast<ITpSlItems>().ToList();
        public IReadOnlyList<ITpSlItems> ClosedItems => _inner.ClosedItems.Cast<ITpSlItems>().ToList();
        public int TradeCount => _inner.TradeCount;

        public void PlaceEntryOrder(PlaceOrderRequestParameters req, string comment, List<PlaceOrderRequestParameters> sl, List<PlaceOrderRequestParameters> tp, object sender = null)
            => _inner.PlaceEntryOrder(req, comment, sl, tp, sender);

        public void UpdateSl(ITpSlItems item, Func<double, double> updateFunction)
            => _inner.UpdateSl((SlTpItems)item, updateFunction);

        public void UpdateTp(ITpSlItems item, Func<double, double> updateFunction)
            => _inner.UpdateTp((SlTpItems)item, updateFunction);

        public void Dispose() => _inner.Dispose();
    }

    internal sealed class PositionManagerFacade : IManagerFacade
    {
        private readonly TpSlPositionManager _inner;
        public PositionManagerFacade(TpSlPositionManager inner)
        {
            _inner = inner;
        }

        public IReadOnlyList<ITpSlItems> Items => _inner.Items.Cast<ITpSlItems>().ToList();
        public IReadOnlyList<ITpSlItems> ClosedItems => _inner.ClosedItems.Cast<ITpSlItems>().ToList();
        public int TradeCount => _inner.TradeCount;

        public void PlaceEntryOrder(PlaceOrderRequestParameters req, string comment, List<PlaceOrderRequestParameters> sl, List<PlaceOrderRequestParameters> tp, object sender = null)
            => _inner.PlaceEntryOrder(req, comment, sl, tp, sender);

        public void UpdateSl(ITpSlItems item, Func<double, double> updateFunction)
            => _inner.UpdateSl((TpSlItemPosition)item, updateFunction);

        public void UpdateTp(ITpSlItems item, Func<double, double> updateFunction)
            => _inner.UpdateTp((TpSlItemPosition)item, updateFunction);

        public void Dispose() => _inner.Dispose();
    }

    public static class ManagerFacadeFactory
    {
        public static IManagerFacade Create(ManagerType type)
        {
            switch (type)
            {
                case ManagerType.LegacyOrdersBased:
                    return new TpSlManagerFacade(GlobalTpSlManager.Instance);
                case ManagerType.PositionBased:
                    return new PositionManagerFacade(new TpSlPositionManager());
                case ManagerType.OrdersHistoryBased:
                default:
                    return new ManagerDueFacade(Utils.GlobalTpSlManagerDue.Instance);
            }
        }
    }
}
