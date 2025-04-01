using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TpSlManager;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv
{
    public interface ITpSlManager<T>
    {
        void Init(SlTpCondictionHolder<T> holder, int maxShort = 3, int maxLong = 3);
        SlTpItems PlaceEntryOrder(string id);
        void RegisterTrade(Trade trade);
        void RegisterPositionRemoved(Position position);
        double NetProfit { get; }
        IEnumerable<SlTpItems> Items { get; }
    }

}
