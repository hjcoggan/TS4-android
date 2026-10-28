using System.Runtime.InteropServices;
using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Platform;

/// <summary>
/// The first connected pad through XInput, which Windows and Wine/Proton both provide. Avalonia
/// reads no pad itself, so the window polls this and feeds <see cref="PadNavigator"/>. Where
/// XInput is missing (a native Linux build, after 1.0) it reads nothing.
/// </summary>
internal static partial class XInputPad
{
    private static bool _unavailable;

    public static bool TryRead(out PadState state)
    {
        state = default;
        if (_unavailable || !OperatingSystem.IsWindows()) return false;
        try
        {
            for (uint user = 0; user < 4; user++)
            {
                if (XInputGetState(user, out var raw) != 0) continue;
                state = new PadState(raw.Buttons, raw.LeftX, raw.LeftY);
                return true;
            }
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
            _unavailable = true;
        }
        return false;
    }

    // XINPUT_STATE: a packet number, then XINPUT_GAMEPAD.
    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short LeftX;
        public short LeftY;
        public short RightX;
        public short RightY;
    }

    [LibraryImport("xinput1_4.dll")]
    private static partial uint XInputGetState(uint user, out XInputState state);
}
