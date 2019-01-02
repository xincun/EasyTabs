using System;

namespace EasyTabs.EventList
{
    /// <summary>
    /// Provides data for the <see cref="EasyTabs.EventList.ListWithEvents{T}.ItemAdded" /> events
    /// </summary>
    [Serializable]
    public class ListItemEventArgs : EventArgs
    {
        public ListItemEventArgs(int itemIndex)
        {
            ItemIndex = itemIndex;
        }

        /// <summary>
        /// Gets the index of the item changed
        /// </summary>
        public int ItemIndex { get; }
    }
}
