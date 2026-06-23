using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace optimizerDuck.Services.System;

/// <summary>Monitors Windows registry keys for changes using RegNotifyChangeKeyValue.</summary>
public interface IRegistryWatcher : IDisposable
{
    /// <summary>Raised when a watched registry key or its values change. The event argument is the registry path that changed.</summary>
    event EventHandler<string>? RegistryKeyChanged;

    /// <summary>Starts watching the specified registry path for changes. Multiple paths can be watched simultaneously.</summary>
    /// <param name="registryPath">The full registry path (e.g., "HKCU\Software\Microsoft\Windows\CurrentVersion\Run").</param>
    void Watch(string registryPath);

    /// <summary>Stops watching the specified registry path.</summary>
    /// <param name="registryPath">The registry path to stop watching.</param>
    void Unwatch(string registryPath);
}

internal sealed class RegistryWatcher(ILogger<RegistryWatcher> logger) : IRegistryWatcher
{
    public event EventHandler<string>? RegistryKeyChanged;

    private readonly ConcurrentDictionary<string, PathWatcher> _watchers = new(
        StringComparer.OrdinalIgnoreCase
    );
    private bool _disposed;

    public void Watch(string registryPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(registryPath))
            return;

        if (_watchers.ContainsKey(registryPath))
            return;

        var watcher = new PathWatcher(registryPath, OnPathChanged, logger);
        if (_watchers.TryAdd(registryPath, watcher))
        {
            watcher.Start();
        }
        else
        {
            watcher.Dispose();
        }
    }

    public void Unwatch(string registryPath)
    {
        if (string.IsNullOrWhiteSpace(registryPath))
            return;

        if (_watchers.TryRemove(registryPath, out var watcher))
        {
            watcher.Dispose();
        }
    }

    private void OnPathChanged(string path)
    {
        RegistryKeyChanged?.Invoke(this, path);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var watcher in _watchers.Values)
        {
            watcher.Dispose();
        }
        _watchers.Clear();
    }

    private sealed class PathWatcher : IDisposable
    {
        private readonly string _path;
        private readonly Action<string> _onChanged;
        private readonly ILogger _logger;
        private readonly object _lock = new();

        private IntPtr _hKey;
        private AutoResetEvent? _event;
        private RegisteredWaitHandle? _registeredWait;
        private CancellationTokenSource? _retryCts;
        private DateTime _lastFiredTime = DateTime.MinValue;
        private bool _isWatching;
        private bool _disposed;

        public PathWatcher(string path, Action<string> onChanged, ILogger logger)
        {
            _path = path;
            _onChanged = onChanged;
            _logger = logger;
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_disposed || _isWatching)
                    return;

                _isWatching = true;
                _retryCts = new CancellationTokenSource();
                _ = ArmWatcherAsync(_retryCts.Token);
            }
        }

        private async Task ArmWatcherAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    lock (_lock)
                    {
                        if (_disposed || token.IsCancellationRequested)
                            return;

                        var (hive, subKey) = ParseRegistryPath(_path);
                        var error = NativeMethods.RegOpenKeyEx(
                            hive,
                            subKey,
                            0,
                            NativeMethods.KEY_NOTIFY,
                            out _hKey
                        );

                        if (error == 0 && _hKey != IntPtr.Zero)
                        {
                            _event = new AutoResetEvent(false);

                            NativeMethods.RegNotifyChangeKeyValue(
                                _hKey,
                                false,
                                NativeMethods.REG_NOTIFY_CHANGE_LAST_SET
                                    | NativeMethods.REG_NOTIFY_CHANGE_NAME,
                                _event.SafeWaitHandle.DangerousGetHandle(),
                                true
                            );

                            _registeredWait = ThreadPool.RegisterWaitForSingleObject(
                                _event,
                                OnKeyChangedCallback,
                                null,
                                Timeout.Infinite,
                                true
                            );

                            return;
                        }
                    }

                    await Task.Delay(1000, token);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "RegistryWatcher: Failed to watch {Path}, retrying",
                        _path
                    );
                    try
                    {
                        await Task.Delay(1000, token);
                    }
                    catch
                    {
                        break;
                    }
                }
            }
        }

        private void OnKeyChangedCallback(object? state, bool timedOut)
        {
            lock (_lock)
            {
                if (_disposed || !_isWatching)
                    return;

                CleanupArm();

                var now = DateTime.UtcNow;
                if (now - _lastFiredTime > TimeSpan.FromMilliseconds(300))
                {
                    _lastFiredTime = now;
                    _onChanged(_path);
                }

                if (_retryCts != null && !_retryCts.Token.IsCancellationRequested)
                {
                    _ = ArmWatcherAsync(_retryCts.Token);
                }
            }
        }

        private void CleanupArm()
        {
            if (_registeredWait != null)
            {
                _registeredWait.Unregister(null);
                _registeredWait = null;
            }
            if (_event != null)
            {
                _event.Dispose();
                _event = null;
            }
            if (_hKey != IntPtr.Zero)
            {
                NativeMethods.RegCloseKey(_hKey);
                _hKey = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;

                _disposed = true;
                _isWatching = false;

                if (_retryCts != null)
                {
                    _retryCts.Cancel();
                    _retryCts.Dispose();
                    _retryCts = null;
                }

                CleanupArm();
            }
        }

        private static (IntPtr hive, string subKey) ParseRegistryPath(string path)
        {
            var firstBackslash = path.IndexOf('\\');
            var root =
                firstBackslash > 0
                    ? path[..firstBackslash].ToUpperInvariant()
                    : path.ToUpperInvariant();

            // Strip trailing colon for compatibility with RegistryService format (e.g. "HKLM:", "HKCU:")
            if (root.EndsWith(":"))
                root = root[..^1];

            var subKey = firstBackslash > 0 ? path[(firstBackslash + 1)..] : string.Empty;

            var hive = root switch
            {
                "HKEY_CURRENT_USER" or "HKCU" => NativeMethods.HKEY_CURRENT_USER,
                "HKEY_LOCAL_MACHINE" or "HKLM" => NativeMethods.HKEY_LOCAL_MACHINE,
                "HKEY_CLASSES_ROOT" or "HKCR" => NativeMethods.HKEY_CLASSES_ROOT,
                "HKEY_USERS" or "HKU" => NativeMethods.HKEY_USERS,
                "HKEY_CURRENT_CONFIG" or "HKCC" => NativeMethods.HKEY_CURRENT_CONFIG,
                _ => throw new ArgumentException($"Unknown registry hive: {root}", nameof(path)),
            };

            return (hive, subKey);
        }
    }

    private static class NativeMethods
    {
        internal static readonly IntPtr HKEY_CURRENT_USER = new(unchecked((int)0x80000001));
        internal static readonly IntPtr HKEY_LOCAL_MACHINE = new(unchecked((int)0x80000002));
        internal static readonly IntPtr HKEY_CLASSES_ROOT = new(unchecked((int)0x80000000));
        internal static readonly IntPtr HKEY_USERS = new(unchecked((int)0x80000003));
        internal static readonly IntPtr HKEY_CURRENT_CONFIG = new(unchecked((int)0x80000005));

        internal const int KEY_NOTIFY = 0x0010;
        internal const uint REG_NOTIFY_CHANGE_NAME = 0x00000001;
        internal const uint REG_NOTIFY_CHANGE_LAST_SET = 0x00000004;

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern int RegOpenKeyEx(
            IntPtr hKey,
            string lpSubKey,
            uint ulOptions,
            int samDesired,
            out IntPtr phkResult
        );

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern int RegNotifyChangeKeyValue(
            IntPtr hKey,
            bool bWatchSubtree,
            uint dwNotifyFilter,
            IntPtr hEvent,
            bool fAsynchronous
        );

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern int RegCloseKey(IntPtr hKey);
    }
}
