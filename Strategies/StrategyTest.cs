using DivergentStrV0_1.OperationSystemAdv;
using TradingPlatform.BusinessLayer;
namespace DivergentStrV0_1.Strategies
{
    internal class StrategyTest : ConditionableBase<double>
    {
        //public void init(Account account, Symbol symbol, ISlTpStrategy<double> strategy, IDomainEventDispatcher dispatcher = null, string description = "")
        //{
        //}

        public StrategyTest()
        {
            
        }

        public override void Close() => throw new System.NotImplementedException();
        public override double SetQuantity()
        {
            return Account.Balance;
        }

        public  void CustomTrade(Side side, double price) => this.Trade(side, price, price, price);
      

        public override void Update(object obj)
        {
            if (obj is TradeData tradeData)
            {

                if (tradeData.Tarde)
                {
                    CustomTrade(Side.Buy, tradeData.Price);
                }
            }
           
        }
    }

    internal struct TradeData
    {
        public bool Tarde { get; private set; }
        public double Price { get; private set; }

        public TradeData(bool trade, double price)
        {
            this.Tarde = trade;
            this.Price = price;
            
        }
    }

}
