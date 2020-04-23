// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI.Core;

namespace TestAppUwp
{
    /// <summary>
    /// Base class for models and view models implementing <see cref="INotifyPropertyChanged"/>.
    /// </summary>
    public class NotifierBase : INotifyPropertyChanged
    {
        /// <inheritdoc/>
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly CoreDispatcher _dispatcher;

        protected NotifierBase()
        {
            _dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher;
        }

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
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            else
            {
                _ = _dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
            }
        }
    }
}
