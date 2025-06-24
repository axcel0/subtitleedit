using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Controls
{
    /// <summary>
    /// Thread-safe collection implementation for NikseComboBox items.
    /// </summary>
    public sealed class NikseComboBoxCollection : IList, IDisposable
    {
        #region Fields

        private readonly List<object> _items;
        private readonly object _syncLock = new object();
        private bool _disposed;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the NikseComboBoxCollection class.
        /// </summary>
        /// <param name="control">The control that owns this collection.</param>
        public NikseComboBoxCollection(Control control)
        {
            _items = new List<object>();
            SyncRoot = control ?? throw new ArgumentNullException(nameof(control));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the number of elements contained in the collection.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_syncLock)
                {
                    return _items.Count;
                }
            }
        }

        /// <summary>
        /// Gets an object that can be used to synchronize access to the collection.
        /// </summary>
        public object SyncRoot { get; }

        /// <summary>
        /// Gets a value indicating whether access to the collection is synchronized (thread safe).
        /// </summary>
        public bool IsSynchronized => true;

        /// <summary>
        /// Gets a value indicating whether the collection is read-only.
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// Gets a value indicating whether the collection has a fixed size.
        /// </summary>
        public bool IsFixedSize => false;

        /// <summary>
        /// Gets or sets the element at the specified index.
        /// </summary>
        public object this[int index]
        {
            get
            {
                lock (_syncLock)
                {
                    ValidateIndex(index);
                    return _items[index];
                }
            }
            set
            {
                lock (_syncLock)
                {
                    ValidateIndex(index);
                    _items[index] = value;
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Converts the collection to a List&lt;object&gt;.
        /// </summary>
        /// <returns>A new list containing all items from the collection.</returns>
        public List<object> ToList()
        {
            lock (_syncLock)
            {
                return new List<object>(_items);
            }
        }

        /// <summary>
        /// Adds multiple items to the collection.
        /// </summary>
        /// <typeparam name="T">The type of items to add.</typeparam>
        /// <param name="items">The items to add.</param>
        public void AddItems<T>(IEnumerable<T> items) where T : class
        {
            if (items == null) return;

            lock (_syncLock)
            {
                _items.AddRange(items.Cast<object>());
            }
        }

        /// <summary>
        /// Adds an array of items to the collection (for WinForms designer support).
        /// </summary>
        /// <param name="items">The items to add.</param>
        public void AddRange(object[] items)
        {
            if (items == null) return;

            lock (_syncLock)
            {
                _items.AddRange(items);
            }
        }

        /// <summary>
        /// Sorts the collection using the specified selector function.
        /// </summary>
        /// <param name="func">The function to extract the sort key.</param>
        public void SortBy(Func<object, object> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            lock (_syncLock)
            {
                _items.Sort((x, y) => Comparer.Default.Compare(func(x), func(y)));
            }
        }

        /// <summary>
        /// Returns the first element that satisfies the specified condition, or null if no such element is found.
        /// </summary>
        /// <param name="predicate">The condition to test each element.</param>
        /// <returns>The first matching element or null.</returns>
        public object FirstOrDefault(Func<object, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            lock (_syncLock)
            {
                return _items.FirstOrDefault(predicate);
            }
        }

        #endregion

        #region IList Implementation

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        public IEnumerator GetEnumerator()
        {
            lock (_syncLock)
            {
                return ToList().GetEnumerator();
            }
        }

        /// <summary>
        /// Copies the elements of the collection to an Array, starting at a particular Array index.
        /// </summary>
        public void CopyTo(Array array, int index)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));

            lock (_syncLock)
            {
                _items.ToArray().CopyTo(array, index);
            }
        }

        /// <summary>
        /// Adds an item to the collection.
        /// </summary>
        /// <param name="value">The item to add.</param>
        /// <returns>The position into which the new element was inserted.</returns>
        public int Add(object value)
        {
            lock (_syncLock)
            {
                _items.Add(value);
                return _items.Count - 1;
            }
        }

        /// <summary>
        /// Determines whether the collection contains a specific value.
        /// </summary>
        /// <param name="value">The value to locate.</param>
        /// <returns>true if the value is found; otherwise, false.</returns>
        public bool Contains(object value)
        {
            lock (_syncLock)
            {
                return _items.Contains(value);
            }
        }

        /// <summary>
        /// Removes all items from the collection.
        /// </summary>
        public void Clear()
        {
            lock (_syncLock)
            {
                _items.Clear();
                ResetOwnerSelection();
            }
        }

        /// <summary>
        /// Determines the index of a specific item in the collection.
        /// </summary>
        /// <param name="value">The item to locate.</param>
        /// <returns>The index of the item if found; otherwise, -1.</returns>
        public int IndexOf(object value)
        {
            lock (_syncLock)
            {
                return _items.IndexOf(value);
            }
        }

        /// <summary>
        /// Inserts an item to the collection at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which value should be inserted.</param>
        /// <param name="value">The item to insert.</param>
        public void Insert(int index, object value)
        {
            lock (_syncLock)
            {
                _items.Insert(index, value);
            }
        }

        /// <summary>
        /// Removes the first occurrence of a specific item from the collection.
        /// </summary>
        /// <param name="value">The item to remove.</param>
        public void Remove(object value)
        {
            lock (_syncLock)
            {
                _items.Remove(value);
                ValidateOwnerSelection();
            }
        }

        /// <summary>
        /// Removes the item at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the item to remove.</param>
        public void RemoveAt(int index)
        {
            lock (_syncLock)
            {
                ValidateIndex(index);
                _items.RemoveAt(index);
                ValidateOwnerSelection();
            }
        }

        #endregion

        #region Private Methods

        private void ValidateIndex(int index)
        {
            if (index < 0 || index >= _items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index was out of range.");
            }
        }

        private void ResetOwnerSelection()
        {
            if (SyncRoot is NikseComboBox ncb)
            {
                ncb.SelectedIndexReset(); // do not fire change events
            }
        }

        private void ValidateOwnerSelection()
        {
            if (SyncRoot is NikseComboBox ncb && ncb.SelectedIndex >= _items.Count)
            {
                ncb.SelectedIndexReset(); // do not fire change events
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Releases all resources used by the NikseComboBoxCollection.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        private void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                lock (_syncLock)
                {
                    _items.Clear();
                }
                _disposed = true;
            }
        }

        #endregion
    }
}
