using System.Runtime.InteropServices;

namespace LayoutsFromModel.Helpers
{
    /// <summary>
    /// Проверяет нажатие Escape и запрашивает подтверждение отмены операции.
    /// </summary>
    internal sealed class EscapeCancellation
    {
        private const int VirtualKeyEscape = 0x1B;
        private const int KeyIsDownMask = 0x8000;
        private const int KeyWasPressedMask = 0x0001;

        private bool escapeIsDown;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        public EscapeCancellation()
        {
            Reset();
        }

        /// <summary>
        /// Сбрасывает сохранённый Windows признак предыдущего нажатия Escape.
        /// </summary>
        public void Reset()
        {
            short keyState = GetAsyncKeyState(VirtualKeyEscape);
            escapeIsDown = (keyState & KeyIsDownMask) != 0;
        }

        public bool IsCancellationRequested()
        {
            short keyState = GetAsyncKeyState(VirtualKeyEscape);
            bool isDown = (keyState & KeyIsDownMask) != 0;

            if (escapeIsDown)
            {
                escapeIsDown = isDown;
                return false;
            }

            bool wasPressed = (keyState & KeyWasPressedMask) != 0;
            if (!isDown && !wasPressed)
                return false;

            escapeIsDown = isDown;

            System.Windows.MessageBoxResult confirmation = System.Windows.MessageBox.Show(
                "Вы действительно хотите прервать создание листов?",
                "Создание листов",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question,
                System.Windows.MessageBoxResult.No);

            // Обновляем состояние после закрытия диалога, чтобы удерживаемый ESC
            // не открыл подтверждение повторно на следующей контрольной точке.
            escapeIsDown = (GetAsyncKeyState(VirtualKeyEscape) & KeyIsDownMask) != 0;
            return confirmation == System.Windows.MessageBoxResult.Yes;
        }
    }
}
