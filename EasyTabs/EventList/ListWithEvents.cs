using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace EasyTabs.EventList
{
    [Serializable]
    [DebuggerDisplay("Count = {Count}")]
    public class ListWithEvents<T> : List<T>, IList
    {
        public ListWithEvents()
        {

        }

        public ListWithEvents(IEnumerable<T> collection) : base(collection)
        {

        }

        public ListWithEvents(int capacity) : base(capacity)
        {

        }

        /// <summary>
        /// Synchronization root for thread safety
        /// </summary>
        private object SyncRoot { get; } = new object();

        /// <summary>
        /// Flag indicating whether events are being suppressed during an operation
        /// </summary>
        public bool IgnoreEvents { get; protected set; } = false;

        /// <summary>
        /// Overloads <see cref="System.Collections.Generic.List{T}.this" />
        /// </summary>
        public new virtual T this[int index]
        {
            get => base[index];
            set
            {
                lock (SyncRoot)
                {
                    bool equal = false;
                    if (base[index] != null)
                        equal = base[index].Equals(value);
                    else if (base[index] == null && value == null)
                        equal = true;
                    if (!equal)
                    {
                        base[index] = value;
                        OnItemModified(new ListItemEventArgs(index));
                    }
                }
            }
        }


        /// <summary>
        /// Stops raising events until <see cref="ResumeEvents" /> is called
        /// </summary>
        public void SuppressEvents()
        {
            IgnoreEvents = true;
        }

        /// <summary>
        /// Resumes raising events after <see cref="SuppressEvents" /> call
        /// </summary>
        public void ResumeEvents()
        {
            IgnoreEvents = false;
        }

        /// <summary>
        /// Overloads <see cref="System.Collections.Generic.List{T}.Add" />
        /// </summary>
        /// <remarks>This operation is thread-safe</remarks>
        /// <param name="item">Item to add</param>
        public new virtual void Add(T item)
        {
            int count;
            lock (SyncRoot)
            {
                base.Add(item);
                count = Count - 1;
            }
            OnItemAdded(new ListItemEventArgs(count));
        }

        /// <summary>
        /// Overloads <see cref="System.Collections.Generic.List{T}.AddRange" />
        /// </summary>
        /// <remarks>This operation is thread-safe</remarks>
        /// <param name="collection">Collection to add</param>
        public new virtual void AddRange(IEnumerable<T> collection)
        {
            lock (SyncRoot)
                InsertRange(Count, collection);
        }

        /// <summary>
        /// Overloads <see cref="System.Collections.Generic.List{T}.Insert" />
        /// </summary>
		/// <remarks>This operation is thread-safe</remarks>
        /// <param name="index">Insert index</param>
        /// <param name="item">Item to insert</param>
        public new virtual void Insert(int index, T item)
        {
            lock (SyncRoot)
                base.Insert(index, item);
            OnItemAdded(new ListItemEventArgs(index));
        }

        /// <summary>
        /// Overloads <see cref="System.Collections.Generic.List{T}.InsertRange" />
        /// </summary>
		/// <remarks>This operation is thread-safe</remarks> 
        /// <param name="index">Insert index</param>
        /// <param name="collection">Collection to insert</param>
        public new virtual void InsertRange(int index, IEnumerable<T> collection)
        {
            int count;
            lock (SyncRoot)
            {
                base.InsertRange(index, collection);
                count = Count - index;
            }
            OnRangeAdded(new ListRangeEventArgs(index, count));
        }

        /// <summary>
        /// Overloads <see cref="System.Collections.Generic.List{T}.Remove" />
        /// </summary>
		/// <remarks>This operation is thread-safe</remarks>
        /// <param name="item">Item to remove</param>
        public new virtual bool Remove(T item)
        {
            bool result;
            lock (SyncRoot)
                result = base.Remove(item);
            if (result)
                OnItemRemoved(EventArgs.Empty);
            return result;
        }

        /// <summary>
        /// Overloads <see cref="System.Collections.Generic.List{T}.RemoveRange" />
        /// </summary>
		/// <remarks>This operation is thread-safe</remarks>
        /// <param name="index">Index</param>
        /// <param name="count">Count</param>
        public new virtual void RemoveRange(int index, int count)
        {
            int listCountOld;
            int listCoundNew;
            lock (SyncRoot)
            {
                listCountOld = Count;
                base.RemoveRange(index, count);
                listCoundNew = Count;
            }
            if (listCountOld != listCoundNew)
                OnRangeRemoved(EventArgs.Empty);
        }

        /// <summary>
        /// Removes the specified list of entries from the collection
        /// </summary>
		/// <remarks>
		/// This operation employs <see cref="Remove" /> method for removing each individual item which is thread-safe. However overall operation isn't atomic, and hence does not guarantee thread-safety
		/// </remarks>
        /// <param name="collection">Collection to be removed from the list</param>
        public virtual void RemoveRange(List<T> collection)
        {
            for (int i = 0; i < collection.Count; i++)
                Remove(collection[i]);
        }

        /// <summary>
        /// Overloads <see cref="System.Collections.Generic.List{T}.RemoveAll" />
        /// </summary>
		/// <remarks>This operation is thread-safe</remarks>
        /// <param name="index">Index</param>
        /// <param name="count">Count</param>
        public new virtual int RemoveAll(Predicate<T> match)
        {
            int count;
            lock (SyncRoot)
                count = base.RemoveAll(match);
            if (Count > 0)
                OnRangeRemoved(EventArgs.Empty);
            return count;
        }

        /// <summary>
        /// Overloads <see cref="System.Collections.Generic.List{T}.RemoveAt" />
        /// </summary>
        /// <remarks>This operation is thread-safe</remarks>
        /// <param name="index">Item to add</param>
        public new virtual void RemoveAt(int index)
        {
            lock (SyncRoot)
                base.RemoveAt(index);
            OnItemRemoved(EventArgs.Empty);
        }

        /// <summary>
        /// Adds an item to the end of the list
        /// </summary>
        /// <param name="value">Item to add to the list</param>
        /// <returns>Index of the new item in the list</returns>
        int IList.Add(object value)
        {
            if (value is T)
            {
                Add((T)value);
                return Count - 1;
            }
            return -1;
        }

        /// <summary>
        /// Raises <see cref="CollectionModified" /> and <see cref="Cleared" /> events
        /// </summary>
        /// <param name="e">An <see cref="System.EventArgs" /> that contains the event data</param>
        protected virtual void OnCleared(EventArgs e)
        {
            if (IgnoreEvents)
                return;
            Cleared?.Invoke(this, e);
            OnCollectionModified(new ListModificationEventArgs(ListModification.Cleared, -1, -1));
        }

        /// <summary>
        /// Raises <see cref="CollectionModified" /> events
        /// </summary>
        /// <param name="e">An <see cref="EasyTabs.EventList.ListModificationEventArgs" /> that contains the event data</param>
        protected virtual void OnCollectionModified(ListModificationEventArgs e)
        {
            if (IgnoreEvents)
                return;
            CollectionModified?.Invoke(this, e);
        }

        /// <summary>
        /// Raises <see cref="CollectionModified" /> and <see cref="ItemAdded" /> events
        /// </summary>
        /// <param name="e">An <see cref="EasyTabs.EventList.ListItemEventArgs" /> that contains the event data</param>
        protected virtual void OnItemAdded(ListItemEventArgs e)
        {
            if (IgnoreEvents)
                return;
            ItemAdded?.Invoke(this, e);
            OnCollectionModified(new ListModificationEventArgs(ListModification.ItemAdded, e.ItemIndex, 1));
        }

        /// <summary>
        /// Raises <see cref="CollectionModified" /> and <see cref="ItemModified" /> events
        /// </summary>
        /// <param name="e">An <see cref="EasyTabs.EventList.ListItemEventArgs" /> that contains the event data</param>
        protected virtual void OnItemModified(ListItemEventArgs e)
        {
            if (IgnoreEvents)
                return;
            ItemModified?.Invoke(this, e);
            OnCollectionModified(new ListModificationEventArgs(ListModification.ItemModified, e.ItemIndex, 1));
        }

        /// <summary>
        /// Raises <see cref="CollectionModified" /> and <see cref="ItemRemoved" /> events
        /// </summary>
        /// <param name="e">An <see cref="System.EventArgs" /> that contains the event data</param>
        protected virtual void OnItemRemoved(EventArgs e)
        {
            if (IgnoreEvents)
                return;
            ItemRemoved?.Invoke(this, e);
            OnCollectionModified(new ListModificationEventArgs(ListModification.ItemRemoved, -1, 1));
        }

        /// <summary>
        /// Raises <see cref="CollectionModified" /> and <see cref="RangeAdded" /> events
        /// </summary>
        /// <param name="e">An <see cref="EasyTabs.EventList.ListRangeEventArgs" /> that contains the event data</param>
        protected virtual void OnRangeAdded(ListRangeEventArgs e)
        {
            if (IgnoreEvents)
                return;
            RangeAdded?.Invoke(this, e);
            OnCollectionModified(new ListModificationEventArgs(ListModification.RangeAdded, e.StartIndex, e.Count));
        }

        /// <summary>
        /// Raises <see cref="CollectionModified" /> and <see cref="RangeRemoved" /> events
        /// </summary>
        /// <param name="e">An <see cref="System.EventArgs" /> that contains the event data</param>
        protected virtual void OnRangeRemoved(EventArgs e)
        {
            if (IgnoreEvents)
                return;
            RangeRemoved?.Invoke(this, e);
            OnCollectionModified(new ListModificationEventArgs(ListModification.RangeRemoved, -1, -1));
        }

        /// <summary>
        /// Occurs whenever the list's content is modified
        /// </summary>
        public event EventHandler<ListModificationEventArgs> CollectionModified;

        /// <summary>
        /// Occurs whenever the list is cleared
        /// </summary>
        public event EventHandler Cleared;

        /// <summary>
        /// Occurs whenever a new item is added to the list
        /// </summary>
        public event EventHandler<ListItemEventArgs> ItemAdded;

        /// <summary>
        /// Occurs whenever a item is modified
        /// </summary>
        public event EventHandler<ListItemEventArgs> ItemModified;

        /// <summary>
        /// Occurs whenever an  item is removed from the list
        /// </summary>
        public event EventHandler ItemRemoved;

        /// <summary>
        /// Occurs whenever a range of items is added to the list
        /// </summary>
        public event EventHandler<ListRangeEventArgs> RangeAdded;

        /// <summary>
        /// Occurs whenever a range of items is removed from the list
        /// </summary>
        public event EventHandler RangeRemoved;
    }
}
