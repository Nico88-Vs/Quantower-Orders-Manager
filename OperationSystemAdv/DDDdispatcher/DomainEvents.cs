using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv
{
    public interface IDomainEvent { }

    public class TradingOperations : IDomainEvent
    {
        public Object Operation { get; }
        public IConditionable Sender_Strategy { get; }

        public TradingOperations(object operation ,IConditionable strategy = null)
        {
            Operation = operation;
            Sender_Strategy = strategy;
        }
    }

    public class TradingErrors : IDomainEvent
    {
        public TradingOperationResult Request_Resoult { get; }
        public IConditionable Sender_Strategy { get; }

        public TradingErrors(TradingOperationResult req_Resoult, IConditionable strategy)
        {
            Request_Resoult = req_Resoult;
            Sender_Strategy = strategy;
        }
    }

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
