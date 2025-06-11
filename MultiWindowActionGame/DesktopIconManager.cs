using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace MultiWindowActionGame
{
    public class DesktopIconInfo
    {
        public string Name { get; set; } = "";
        public Rectangle Bounds { get; set; }
        public Point Position => Bounds.Location;
        public Size Size => Bounds.Size;
        public bool IsSelected { get; set; }
        public IntPtr IconHandle { get; set; }
    }

    public class DesktopIconManager
    {
        private static readonly Lazy<DesktopIconManager> lazy =
            new Lazy<DesktopIconManager>(() => new DesktopIconManager());
        public static DesktopIconManager Instance => lazy.Value;

        // Win32 API 定数
        private const int LVM_FIRST = 0x1000;
        private const int LVM_GETITEMCOUNT = LVM_FIRST + 4;
        private const int LVM_GETITEMRECT = LVM_FIRST + 14;
        private const int LVM_GETITEMTEXT = LVM_FIRST + 115;
        private const int LVIR_BOUNDS = 0;
        private const int LVIR_ICON = 1;
        private const int LVIR_LABEL = 2;
        private const int PROCESS_VM_OPERATION = 0x0008;
        private const int PROCESS_VM_READ = 0x0010;
        private const int PROCESS_VM_WRITE = 0x0020;
        private const int MEM_COMMIT = 0x1000;
        private const int MEM_RELEASE = 0x8000;
        private const int PAGE_READWRITE = 0x04;

        // Win32 API 関数
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll")]
        private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

        [DllImport("kernel32.dll")]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, uint nSize, out uint lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer, uint nSize, out uint lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LVITEM
        {
            public uint mask;
            public int iItem;
            public int iSubItem;
            public uint state;
            public uint stateMask;
            public IntPtr pszText;
            public int cchTextMax;
            public int iImage;
            public IntPtr lParam;
        }

        private DesktopIconManager() { }

        /// <summary>
        /// デスクトップアイコンの情報を取得
        /// </summary>
        public List<DesktopIconInfo> GetDesktopIcons()
        {
            var icons = new List<DesktopIconInfo>();

            try
            {
                // デスクトップのListViewハンドルを取得
                IntPtr desktopListView = GetDesktopListView();
                if (desktopListView == IntPtr.Zero)
                {
                    return icons;
                }

                // アイコンの数を取得
                int iconCount = (int)SendMessage(desktopListView, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);

                if (iconCount <= 0)
                {
                    return icons;
                }

                // プロセスIDを取得
                uint processId;
                GetWindowThreadProcessId(desktopListView, out processId);

                // プロセスハンドルを取得
                IntPtr processHandle = OpenProcess(
                    PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE,
                    false,
                    processId);

                if (processHandle == IntPtr.Zero)
                {
                    return icons;
                }

                try
                {
                    // 各アイコンの情報を取得
                    for (int i = 0; i < iconCount; i++)
                    {
                        var iconInfo = GetIconInfo(desktopListView, processHandle, i);
                        if (iconInfo != null)
                        {
                            icons.Add(iconInfo);
                        }
                    }
                }
                finally
                {
                    CloseHandle(processHandle);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"デスクトップアイコン取得エラー: {ex.Message}");
            }

            return icons;
        }

        /// <summary>
        /// デスクトップのListViewハンドルを取得
        /// </summary>
        private IntPtr GetDesktopListView()
        {
            // Progman（Program Manager）を検索
            IntPtr progman = FindWindow("Progman", null);
            if (progman == IntPtr.Zero)
                return IntPtr.Zero;

            // SHELLDLL_DefViewを検索
            IntPtr shellDllDefView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellDllDefView == IntPtr.Zero)
            {
                // Windows 10/11では別の場所にある場合がある
                IntPtr workerw = FindWindow("WorkerW", null);
                while (workerw != IntPtr.Zero)
                {
                    shellDllDefView = FindWindowEx(workerw, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (shellDllDefView != IntPtr.Zero)
                        break;
                    workerw = FindWindowEx(IntPtr.Zero, workerw, "WorkerW", null);
                }
            }

            if (shellDllDefView == IntPtr.Zero)
                return IntPtr.Zero;

            // SysListView32を検索
            return FindWindowEx(shellDllDefView, IntPtr.Zero, "SysListView32", null);
        }

        /// <summary>
        /// 指定されたインデックスのアイコン情報を取得
        /// </summary>
        private DesktopIconInfo GetIconInfo(IntPtr listView, IntPtr processHandle, int index)
        {
            try
            {
                // リモートプロセスのメモリを確保
                IntPtr remoteRect = VirtualAllocEx(processHandle, IntPtr.Zero, (uint)Marshal.SizeOf<RECT>(), MEM_COMMIT, PAGE_READWRITE);
                IntPtr remoteText = VirtualAllocEx(processHandle, IntPtr.Zero, 512, MEM_COMMIT, PAGE_READWRITE);
                IntPtr remoteLvItem = VirtualAllocEx(processHandle, IntPtr.Zero, (uint)Marshal.SizeOf<LVITEM>(), MEM_COMMIT, PAGE_READWRITE);

                if (remoteRect == IntPtr.Zero || remoteText == IntPtr.Zero || remoteLvItem == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    // アイコンの矩形を取得
                    RECT rect = new RECT();
                    IntPtr rectPtr = Marshal.AllocHGlobal(Marshal.SizeOf<RECT>());
                    Marshal.StructureToPtr(rect, rectPtr, false);

                    WriteProcessMemory(processHandle, remoteRect, rectPtr, (uint)Marshal.SizeOf<RECT>(), out _);

                    IntPtr result = SendMessage(listView, LVM_GETITEMRECT, new IntPtr(index), remoteRect);

                    if (result != IntPtr.Zero)
                    {
                        ReadProcessMemory(processHandle, remoteRect, rectPtr, (uint)Marshal.SizeOf<RECT>(), out _);
                        rect = Marshal.PtrToStructure<RECT>(rectPtr);
                    }

                    Marshal.FreeHGlobal(rectPtr);

                    // アイコン名を取得
                    LVITEM lvItem = new LVITEM
                    {
                        mask = 0x0001, // LVIF_TEXT
                        iItem = index,
                        iSubItem = 0,
                        pszText = remoteText,
                        cchTextMax = 512
                    };

                    IntPtr lvItemPtr = Marshal.AllocHGlobal(Marshal.SizeOf<LVITEM>());
                    Marshal.StructureToPtr(lvItem, lvItemPtr, false);

                    WriteProcessMemory(processHandle, remoteLvItem, lvItemPtr, (uint)Marshal.SizeOf<LVITEM>(), out _);

                    SendMessage(listView, LVM_GETITEMTEXT, new IntPtr(index), remoteLvItem);

                    // テキストを読み取り
                    byte[] buffer = new byte[512];
                    IntPtr bufferPtr = Marshal.AllocHGlobal(512);
                    ReadProcessMemory(processHandle, remoteText, bufferPtr, 512, out _);
                    Marshal.Copy(bufferPtr, buffer, 0, 512);

                    string iconName = Encoding.Unicode.GetString(buffer).TrimEnd('\0');

                    Marshal.FreeHGlobal(lvItemPtr);
                    Marshal.FreeHGlobal(bufferPtr);

                    return new DesktopIconInfo
                    {
                        Name = iconName,
                        Bounds = new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top),
                        IconHandle = listView
                    };
                }
                finally
                {
                    // メモリを解放
                    if (remoteRect != IntPtr.Zero)
                        VirtualFreeEx(processHandle, remoteRect, 0, MEM_RELEASE);
                    if (remoteText != IntPtr.Zero)
                        VirtualFreeEx(processHandle, remoteText, 0, MEM_RELEASE);
                    if (remoteLvItem != IntPtr.Zero)
                        VirtualFreeEx(processHandle, remoteLvItem, 0, MEM_RELEASE);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"アイコン情報取得エラー: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// デスクトップアイコンの情報をデバッグ出力
        /// </summary>
        public void PrintDesktopIcons()
        {
            var icons = GetDesktopIcons();
            System.Diagnostics.Debug.WriteLine($"デスクトップアイコン数: {icons.Count}");

            foreach (var icon in icons)
            {
                System.Diagnostics.Debug.WriteLine($"アイコン: {icon.Name}");
                System.Diagnostics.Debug.WriteLine($"  位置: ({icon.Position.X}, {icon.Position.Y})");
                System.Diagnostics.Debug.WriteLine($"  サイズ: {icon.Size.Width} x {icon.Size.Height}");
                System.Diagnostics.Debug.WriteLine($"  範囲: {icon.Bounds}");
            }
        }

        /// <summary>
        /// 指定した点にあるデスクトップアイコンを取得
        /// </summary>
        public DesktopIconInfo GetIconAt(Point point)
        {
            var icons = GetDesktopIcons();
            return icons.FirstOrDefault(icon => icon.Bounds.Contains(point));
        }

        /// <summary>
        /// 指定した矩形と重なるデスクトップアイコンを取得
        /// </summary>
        public List<DesktopIconInfo> GetIconsIntersecting(Rectangle bounds)
        {
            var icons = GetDesktopIcons();
            return icons.Where(icon => icon.Bounds.IntersectsWith(bounds)).ToList();
        }
    }
}