using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace LocalSoundboard.Services;

public sealed record HotkeyBinding(string Name, string Gesture, Action Execute);

public sealed class GlobalHotkeyService : IDisposable
{
    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = [];
    private int _nextId;

    public GlobalHotkeyService(nint handle)
    {
        _source = HwndSource.FromHwnd(handle) ?? throw new InvalidOperationException("No window handle.");
        _source.AddHook(HandleMessage);
    }

    public IReadOnlyList<string> Apply(IEnumerable<HotkeyBinding> bindings)
    {
        Clear();
        var errors = new List<string>();
        var used = new HashSet<(uint Modifiers, uint Key)>();
        foreach (var binding in bindings.Where(binding => !string.IsNullOrWhiteSpace(binding.Gesture)))
        {
            if (!TryParse(binding.Gesture, out var modifiers, out var key))
            {
                errors.Add($"{binding.Name}: invalid shortcut.");
                continue;
            }
            if (!used.Add((modifiers, key)))
            {
                errors.Add($"{binding.Name}: duplicate shortcut {binding.Gesture}.");
                continue;
            }
            var id = ++_nextId;
            if (RegisterHotKey(_source.Handle, id, modifiers | 0x4000, key)) _actions[id] = binding.Execute;
            else errors.Add($"{binding.Name}: {binding.Gesture} is already in use or reserved by Windows.");
        }
        return errors;
    }

    public static bool TryParse(string text, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        try
        {
            if (new KeyGestureConverter().ConvertFromInvariantString(text) is not KeyGesture gesture) return false;
            if ((gesture.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0) return false;
            if (gesture.Key is Key.None or Key.System) return false;
            modifiers = ((gesture.Modifiers & ModifierKeys.Alt) != 0 ? 1u : 0)
                | ((gesture.Modifiers & ModifierKeys.Control) != 0 ? 2u : 0)
                | ((gesture.Modifiers & ModifierKeys.Shift) != 0 ? 4u : 0)
                | ((gesture.Modifiers & ModifierKeys.Windows) != 0 ? 8u : 0);
            virtualKey = (uint)KeyInterop.VirtualKeyFromKey(gesture.Key);
            return virtualKey != 0;
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException or FormatException) { return false; }
    }

    private nint HandleMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0312 && _actions.TryGetValue((int)wParam, out var action))
        {
            handled = true;
            action();
        }
        return 0;
    }

    private void Clear()
    {
        foreach (var id in _actions.Keys) UnregisterHotKey(_source.Handle, id);
        _actions.Clear();
    }

    public void Dispose() { Clear(); _source.RemoveHook(HandleMessage); }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);
}
