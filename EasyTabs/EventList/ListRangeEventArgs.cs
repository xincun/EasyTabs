using System;

namespace EasyTabs.EventList
{
    /// <summary>
    /// Provides data for the <see cref="EasyTabs.EventList.ListWithEvents{T}.RangeAdded" /> events
    /// </summary>
    [Serializable]
    public class ListRangeEventArgs : EventArgs
    {
        public ListRangeEventArgs(int startIndex, int count)
        {
            StartIndex = startIndex;
            Count = count;
        }

        /// <summary>
        /// Gets the index of the first item in the range
        /// </summary>
        public int StartIndex { get; }

        /// <summary>
        /// Gets the number of items in the range
        /// </summary>
        public int Count { get; }
    }
}
