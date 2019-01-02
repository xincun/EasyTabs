namespace EasyTabs.EventList
{
    public enum ListModification
    {
        /// <summary>
        /// The list has been cleared
        /// </summary>
        Cleared = 0,

        /// <summary>
        /// A new item has been added
        /// </summary>
        ItemAdded,

        /// <summary>
        /// An item has been modified
        /// </summary>
        ItemModified,

        /// <summary>
        /// An item has been removed
        /// </summary>
        ItemRemoved,

        /// <summary>
        /// A range of items has been added
        /// </summary>
        RangeAdded,

        /// <summary>
        /// A range of items has been removed
        /// </summary>
        RangeRemoved
    }
}
