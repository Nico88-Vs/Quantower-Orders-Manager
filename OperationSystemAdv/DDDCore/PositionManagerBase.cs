using System;
using System.Collections.Generic;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv.DDDCore
{
    /// <summary>
    /// Generic base class that provides common functionality for position managers.
    /// </summary>
    /// <typeparam name="T">Type of items managed by the position manager.</typeparam>
    public abstract class PositionManagerBase<T> : IPositionManager<T> where T : ITpSlItems
    {
        protected readonly Dictionary<string, List<string>> _itemsDictionary;
        protected readonly object _lockObj = new object();

        public List<T> Items { get; protected set; }
        public List<T> ClosedItems { get; protected set; }

        public abstract event EventHandler QuitAll;
        public abstract double ExposedAmmount { get; }

        public abstract int TradeCount { get; }

        protected PositionManagerBase()
        {
            Items = new List<T>();
            ClosedItems = new List<T>();
            _itemsDictionary = new Dictionary<string, List<string>>();
        }

        public abstract void PlaceEntryOrder(PlaceOrderRequestParameters req, string comment, List<PlaceOrderRequestParameters> sl, List<PlaceOrderRequestParameters> tp, object sender = null);

        public abstract void UpdateSl(T item, Func<double, double> updateFunction);

        public abstract void UpdateTp(T item, Func<double, double> updateFunction);

        public abstract void Dispose();

        /// <summary>
        /// Creates a new item and adds it to the internal collections if it does not already exist.
        /// </summary>
        public virtual void CreateItem(string comment)
        {
            if (!_itemsDictionary.ContainsKey(comment))
            {
                var item = CreateNewItem(comment);
                Items.Add(item);
                _itemsDictionary.Add(item.Id, new List<string>());
                OnItemCreated(item);
            }
        }

        /// <summary>
        /// Factory method used to create a new item instance.
        /// </summary>
        protected abstract T CreateNewItem(string comment);

        /// <summary>
        /// Hook for derived classes to execute additional logic when a new item is created.
        /// </summary>
        /// <param name="item">The newly created item.</param>
        protected virtual void OnItemCreated(T item) { }

        /// <summary>
        /// Utility to split comment into identifier and subcomment type.
        /// </summary>
        public KeyValuePair<string, OrderTypeSubcomment>? GetSplittedComment(string comment)
        {
            try
            {
                var splittedcomment = comment.Split('.');

                if (splittedcomment.Length == 2 && Enum.TryParse<OrderTypeSubcomment>(splittedcomment[1], out var type))
                {
                    return new KeyValuePair<string, OrderTypeSubcomment>(splittedcomment[0], type);
                }
            }
            catch
            {
                // Ignore and return null
            }

            return null;
        }
    }
}

