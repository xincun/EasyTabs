using System;

namespace EasyTabs.EventList
{
    /// <summary>
    /// Provides data for the <see cref="EasyTabs.EventList.ListWithEvents{T}.CollectionModified" /> events
    /// </summary>
    [Serializable]
    public class ListModificationEventArgs : ListRangeEventArgs
    {
        public ListModificationEventArgs(ListModification modification, int startIndex, int count) : base(startIndex, count)
        {
            Modification = modification;
        }

        /// <summary>
        /// Gets the type of list modification
        /// </summary>
        public ListModification Modification { get; }
    }
}
