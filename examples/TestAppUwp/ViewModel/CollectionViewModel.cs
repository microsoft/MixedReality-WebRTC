// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI.Core;

namespace TestAppUwp
{
    /// <summary>
    /// Base class for observable collections with a single selected item.
    /// </summary>
    /// <typeparam name="T">The type of collection items.</typeparam>
    public class CollectionViewModel<T> : ObservableCollection<T> where T : class
    {
        T _selectedItem;
        bool _autoSelectOnAdd = true;

        public event Action SelectionChanged;

        /// <summary>
        /// Item currently selected in the collection.
        /// </summary>
        public T SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    OnSelectedItemChanged();
                    SelectionChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// Automatically select an element when the collection is not empty and
        /// no element is selected yet.
        /// </summary>
        public bool AutoSelectOnAdd
        {
            get { return _autoSelectOnAdd; }
            set
            {
                if (SetProperty(ref _autoSelectOnAdd, value) && _autoSelectOnAdd)
                {
                    // If autoselect set to true, try to select immediately
                    SelectFirstItemIfAny();
                }
            }
        }

        private readonly CoreDispatcher _dispatcher;

        public CollectionViewModel()
        {
            // TODO - Make this collection thread-aware for the PropertyChanged event does not
            // make it multi-tread safe, since the internal container is not. This only avoids
            // some wrong thread error in XAML when raising the PropertyChanged event.
            _dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher;
        }

        /// <summary>
        /// Ensure an item is selected if the collection is not emtpy by selecting
        /// the first item in the collection if <see cref="SelectedItem"/> is <c>null</c>.
        /// </summary>
        public void SelectFirstItemIfAny()
        {
            if ((Count > 0) && (_selectedItem == null))
            {
                SelectedItem = this[0];
            }
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnCollectionChanged(e);

            // Auto-select on add
            if (_autoSelectOnAdd && (_selectedItem == null) && (e.Action == NotifyCollectionChangedAction.Add))
            {
                SelectedItem = this[0];
            }
        }

        /// <summary>
        /// Callback invoked when the selected item changed, to allow derived classes to do some extra
        /// work based on the selected item. The default implementation does nothing.
        /// </summary>
        protected virtual void OnSelectedItemChanged() { }

        /// <summary>
        /// Try to set a property to a new value, and raise the <see cref="PropertyChanged"/> event if
        /// the value actually changed, or does nothing if not. Values are compared with the built-in
        /// <see cref="object.Equals(object, object)"/> method.
        /// </summary>
        /// <typeparam name="T">The property type.</typeparam>
        /// <param name="storage">Storage field for the property, whose value is overwritten with the new value.</param>
        /// <param name="value">New property value to set, which is possibly the same value it currently has.</param>
        /// <param name="propertyName">
        /// Property name. This is automatically inferred by the compiler if the method is called from
        /// within a property setter block <c>set { }</c>.
        /// </param>
        /// <returns>Return <c>true</c> if the property value actually changed, or <c>false</c> otherwise.</returns>
        protected bool SetProperty<U>(ref U storage, U value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return false;
            }
            storage = value;
            RaisePropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Raise the <see cref="PropertyChanged"/> event for the given property name, taking care of dispatching
        /// the call to the appropriate thread.
        /// </summary>
        /// <param name="propertyName">Name of the property which changed.</param>
        protected void RaisePropertyChanged(string propertyName)
        {
            // The event must be raised on the UI thread
            if (_dispatcher.HasThreadAccess)
            {
                OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
            }
            else
            {
                _ = _dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () => OnPropertyChanged(new PropertyChangedEventArgs(propertyName)));
            }
        }
    }
}
