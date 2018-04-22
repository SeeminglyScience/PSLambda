using System;

namespace PSLambda
{
    /// <summary>
    /// Represents a handle for the current scope.
    /// </summary>
    internal class ScopeHandle : IDisposable
    {
        private readonly Action _disposer;

        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeHandle" /> class.
        /// </summary>
        /// <param name="disposer">The action that disposes of the scope.</param>
        public ScopeHandle(Action disposer)
        {
            _disposer = disposer;
        }

        /// <summary>
        /// Disposes of the current scope.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _disposer?.Invoke();
            _isDisposed = true;
        }
    }
}
