using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DivergentStrV0_1.OperationSystemAdv
{
    public interface IDomainEvent { }

    public class TradeFilledEvent : IDomainEvent
    {
        public string ItemId { get; }
        public double Quantity { get; }

        public TradeFilledEvent(string itemId, double quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }
    }

    public class PartialCloseEvent : IDomainEvent
    {
        public string ItemId { get; }
        public double Profit { get; }

        public PartialCloseEvent(string itemId, double profit)
        {
            ItemId = itemId;
            Profit = profit;
        }
    }

    public class PositionClosedEvent : IDomainEvent
    {
        public string ItemId { get; }

        public PositionClosedEvent(string itemId)
        {
            ItemId = itemId;
        }
    }

}
