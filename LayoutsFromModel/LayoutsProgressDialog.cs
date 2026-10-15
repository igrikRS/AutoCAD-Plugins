using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace LayoutsFromModel
{
    /// <summary>
    /// Показывает ход создания листов и позволяет запросить отмену операции.
    /// </summary>
    internal sealed class LayoutsProgressDialog : Window
    {
        private const uint MonitorDefaultToNearest = 2;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;

        private readonly TextBlock statusText;
        private readonly ProgressBar progressBar;
        private readonly Button cancelButton;
        private bool allowClose;

        public bool IsCancellationRequested { get; private set; }

        public LayoutsProgressDialog(int totalLayouts)
        {
            Title = "Создание листов — ход выполнения";
            Width = 480;
            Height = 155;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            ShowInTaskbar = false;

            var panel = new Grid { Margin = new Thickness(16) };
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            statusText = new TextBlock
            {
                Margin = new Thickness(0, 0, 0, 8),
                Text = string.Format("Создано 0 из {0} листов", totalLayouts)
            };
            Grid.SetRow(statusText, 0);
            panel.Children.Add(statusText);

            progressBar = new ProgressBar
            {
                Height = 22,
                Minimum = 0,
                Maximum = Math.Max(1, totalLayouts),
                Value = 0,
                Margin = new Thickness(0, 0, 0, 12)
            };
            Grid.SetRow(progressBar, 1);
            panel.Children.Add(progressBar);

            cancelButton = new Button
            {
                Content = "Отмена",
                Width = 100,
                Height = 25,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            cancelButton.Click += OnCancelClick;
            Grid.SetRow(cancelButton, 2);
            panel.Children.Add(cancelButton);

            Content = panel;
        }

        public void CenterOnAutoCadMonitor(IntPtr autoCadWindowHandle)
        {
            IntPtr dialogHandle = new WindowInteropHelper(this).Handle;
            IntPtr monitorHandle = MonitorFromWindow(
                autoCadWindowHandle,
                MonitorDefaultToNearest);

            var monitorInfo = new MonitorInfo();
            monitorInfo.Size = Marshal.SizeOf(typeof(MonitorInfo));

            Rect dialogRect;
            if (dialogHandle == IntPtr.Zero ||
                monitorHandle == IntPtr.Zero ||
                !GetMonitorInfo(monitorHandle, ref monitorInfo) ||
                !GetWindowRect(dialogHandle, out dialogRect))
            {
                return;
            }

            int left = monitorInfo.WorkArea.Left +
                       (monitorInfo.WorkArea.Right - monitorInfo.WorkArea.Left - dialogRect.Width) / 2;
            int top = monitorInfo.WorkArea.Top +
                      (monitorInfo.WorkArea.Bottom - monitorInfo.WorkArea.Top - dialogRect.Height) / 2;

            SetWindowPos(dialogHandle, IntPtr.Zero, left, top, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
        }

        public void UpdateProgress(int createdLayouts, int totalLayouts)
        {
            statusText.Text = string.Format(
                "Создано {0} из {1} листов",
                createdLayouts,
                totalLayouts);
            progressBar.Value = createdLayouts;
            ProcessPendingUiEvents();
        }

        public void CloseProgress()
        {
            allowClose = true;
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!allowClose)
            {
                RequestCancellation();
                e.Cancel = true;
            }

            base.OnClosing(e);
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            RequestCancellation();
        }

        private void RequestCancellation()
        {
            IsCancellationRequested = true;
            statusText.Text = "Отмена создания листов...";
            cancelButton.IsEnabled = false;
            ProcessPendingUiEvents();
        }

        private void ProcessPendingUiEvents()
        {
            Dispatcher.Invoke(
                DispatcherPriority.Background,
                new Action(delegate { }));
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr windowHandle, uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitorHandle, ref MonitorInfo monitorInfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr windowHandle, out Rect rect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(
            IntPtr windowHandle,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width { get { return Right - Left; } }
            public int Height { get { return Bottom - Top; } }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public Rect Monitor;
            public Rect WorkArea;
            public uint Flags;
        }
    }
}
