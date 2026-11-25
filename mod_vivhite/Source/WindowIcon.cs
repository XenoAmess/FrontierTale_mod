using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Vivhite
{
    internal sealed class WindowIcon : IDisposable
    {
        private const uint SetIconMessage = 0x0080;
        private const uint GetIconMessage = 0x007f;
        private const int SmallClassIcon = -34;
        private const int LargeClassIcon = -14;
        private const int LargeWidthMetric = 11;
        private const int LargeHeightMetric = 12;
        private const int SmallWidthMetric = 49;
        private const int SmallHeightMetric = 50;
        private static readonly IntPtr Small = IntPtr.Zero;
        private static readonly IntPtr Large = new IntPtr(1);

        private readonly byte[] avatar;
        private readonly uint processId = GetCurrentProcessId();
        private IntPtr window;
        private IntPtr smallIcon;
        private IntPtr largeIcon;
        private IntPtr previousSmallIcon;
        private IntPtr previousLargeIcon;
        private IntPtr previousSmallClassIcon;
        private IntPtr previousLargeClassIcon;
        private float nextProbe;
        private bool failed;

        private WindowIcon(byte[] avatar) => this.avatar = avatar;

        internal static WindowIcon CreateForPlayer(string folder) =>
            Application.platform == RuntimePlatform.WindowsPlayer && !Application.isBatchMode
                ? new WindowIcon(File.ReadAllBytes(Path.Combine(folder, "avatar.png")))
                : null;

        internal void Update()
        {
            if (failed || Time.unscaledTime < nextProbe)
                return;
            nextProbe = Time.unscaledTime + 0.5f;
            if (window != IntPtr.Zero && IsWindow(window))
                return;

            IntPtr candidate = FindPlayerWindow();
            if (candidate == IntPtr.Zero)
                return;

            if (smallIcon == IntPtr.Zero)
                smallIcon = CreateFromAvatar(GetSystemMetrics(SmallWidthMetric), GetSystemMetrics(SmallHeightMetric));
            if (largeIcon == IntPtr.Zero)
                largeIcon = CreateFromAvatar(GetSystemMetrics(LargeWidthMetric), GetSystemMetrics(LargeHeightMetric));
            if (smallIcon == IntPtr.Zero || largeIcon == IntPtr.Zero)
            {
                failed = true;
                Debug.LogWarning("[Vivhite] Windows could not create the window icon from the avatar.");
                return;
            }

            window = candidate;
            previousSmallIcon = SendMessage(window, SetIconMessage, Small, smallIcon);
            previousLargeIcon = SendMessage(window, SetIconMessage, Large, largeIcon);
            IntPtr oldSmallClassIcon = SetClassIcon(window, SmallClassIcon, smallIcon);
            IntPtr oldLargeClassIcon = SetClassIcon(window, LargeClassIcon, largeIcon);
            if (oldSmallClassIcon != smallIcon)
                previousSmallClassIcon = oldSmallClassIcon;
            if (oldLargeClassIcon != largeIcon)
                previousLargeClassIcon = oldLargeClassIcon;
        }

        private IntPtr FindPlayerWindow()
        {
            IntPtr found = IntPtr.Zero;
            var className = new StringBuilder(64);
            EnumWindows((handle, _) =>
            {
                GetWindowThreadProcessId(handle, out uint owner);
                if (owner != processId || !IsWindowVisible(handle))
                    return true;
                if (GetClassName(handle, className, className.Capacity) == 0 || className.ToString() != "UnityWndClass")
                    return true;

                found = handle;
                return false;
            }, IntPtr.Zero);
            return found;
        }

        private IntPtr CreateFromAvatar(int width, int height) =>
            CreateIconFromResourceEx(avatar, (uint)avatar.Length, true, 0x00030000, width, height, 0);

        public void Dispose()
        {
            if (window != IntPtr.Zero && IsWindow(window))
            {
                RestoreWindowIcon(Small, smallIcon, previousSmallIcon);
                RestoreWindowIcon(Large, largeIcon, previousLargeIcon);
                RestoreClassIcon(SmallClassIcon, smallIcon, previousSmallClassIcon);
                RestoreClassIcon(LargeClassIcon, largeIcon, previousLargeClassIcon);
            }
            if (smallIcon != IntPtr.Zero)
                DestroyIcon(smallIcon);
            if (largeIcon != IntPtr.Zero)
                DestroyIcon(largeIcon);
            window = smallIcon = largeIcon = IntPtr.Zero;
            failed = true;
        }

        private void RestoreWindowIcon(IntPtr kind, IntPtr ownIcon, IntPtr original)
        {
            if (SendMessage(window, GetIconMessage, kind, IntPtr.Zero) == ownIcon)
                SendMessage(window, SetIconMessage, kind, original);
        }

        private void RestoreClassIcon(int kind, IntPtr ownIcon, IntPtr original)
        {
            if (GetClassIcon(window, kind) == ownIcon)
                SetClassIcon(window, kind, original);
        }

        private delegate bool EnumWindowCallback(IntPtr handle, IntPtr state);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr state);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint owner);

        [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int GetClassName(IntPtr handle, StringBuilder name, int size);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr handle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        private static extern IntPtr CreateIconFromResourceEx(byte[] data, uint size,
            [MarshalAs(UnmanagedType.Bool)] bool icon, uint version, int width, int height, uint flags);

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern IntPtr SendMessage(IntPtr handle, uint message, IntPtr wparam, IntPtr lparam);

        [DllImport("user32.dll", EntryPoint = "SetClassLongPtrW")]
        private static extern IntPtr SetClassIcon(IntPtr handle, int index, IntPtr icon);

        [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
        private static extern IntPtr GetClassIcon(IntPtr handle, int index);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr icon);
    }
}
