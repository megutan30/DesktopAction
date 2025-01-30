using MultiWindowActionGame;

public static class WindowMessageHandler
{
    public readonly struct MessageHandleResult
    {
        public bool Handled { get; }
        public IntPtr Result { get; }

        public MessageHandleResult(bool handled, IntPtr result = default)
        {
            Handled = handled;
            Result = result;
        }

        public static MessageHandleResult NotHandled => new(false);
        public static MessageHandleResult Success => new(true, IntPtr.Zero);
        public static MessageHandleResult WithResult(IntPtr result) => new(true, result);
    }

    public static MessageHandleResult HandleWindowMessage(GameWindow window, Message m)
    {
        if (m.Msg == WindowMessages.WM_ACTIVATE && !window.IsInitializing)
        {
            if (m.WParam.ToInt32() != 0)
            {
                HandleLeftButtonDown(window);
                HandleLeftButtonUp(window);
                return MessageHandleResult.Success;
            }
        }
        // ストラテジーにメッセージを渡す前に共通処理を実行
        var commonResult = HandleCommonMessages(window, m);
        if (commonResult.Handled)
        {
            return commonResult;
        }

        // ストラテジー固有の処理
        window.Strategy.HandleWindowMessage(window, m);

        // ストラテジー処理後の共通処理
        var postResult = HandlePostStrategyMessages(window, m);
        if (postResult.Handled)
        {
            return postResult;
        }

        return MessageHandleResult.NotHandled;
    }

    private static MessageHandleResult HandleCommonMessages(GameWindow window, Message m)
    {
        return m.Msg switch
        {
            WindowMessages.WM_MOUSEACTIVATE => new MessageHandleResult(true, (IntPtr)WindowMessages.MA_NOACTIVATE),
            WindowMessages.WM_NCHITTEST => HandleHitTest(window, m),
            WindowMessages.WM_SYSCOMMAND => HandleSysCommand(window, m),
            _ => MessageHandleResult.NotHandled
        };
    }

    private static MessageHandleResult HandlePostStrategyMessages(GameWindow window, Message m)
    {
        switch (m.Msg)
        {
            case WindowMessages.WM_LBUTTONDOWN:
                HandleLeftButtonDown(window);
                return MessageHandleResult.Success;

            case WindowMessages.WM_LBUTTONUP:
                HandleLeftButtonUp(window);
                return MessageHandleResult.Success;

            case WindowMessages.WM_MOUSEMOVE:
                HandleMouseMove(window);
                return MessageHandleResult.Success;

            default:
                return MessageHandleResult.NotHandled;
        }
    }

    private static MessageHandleResult HandleHitTest(GameWindow window, Message m)
    {
        // キャプション領域でのヒットテスト
        if (m.Result.ToInt32() == WindowMessages.HTCAPTION)
        {
            return MessageHandleResult.WithResult(IntPtr.Zero);
        }
        return MessageHandleResult.NotHandled;
    }

    private static MessageHandleResult HandleSysCommand(GameWindow window, Message m)
    {
        int command = m.WParam.ToInt32() & 0xFFF0;
        switch (command)
        {
            case WindowMessages.SC_CLOSE:
                return MessageHandleResult.Success;

            case WindowMessages.SC_MINIMIZE:
                HandleMinimize(window);
                return MessageHandleResult.Success;

            case WindowMessages.SC_RESTORE:
                HandleRestore(window);
                return MessageHandleResult.Success;

            default:
                return MessageHandleResult.NotHandled;
        }
    }

    private static void HandleLeftButtonDown(GameWindow window)
    {
        WindowManager.Instance.BringWindowToFront(window);
    }

    private static void HandleLeftButtonUp(GameWindow window)
    {
        WindowManager.Instance.CheckPotentialParentWindow(window);
        UpdatePlayerMovableRegion(window);
    }

    private static void HandleMouseMove(GameWindow window)
    {
        // 必要に応じてマウス移動時の処理を追加
        var player = MainGame.GetPlayer();
        if (player?.Parent == null) return;

        var region = WindowManager.Instance.CalculateMovableRegion(player.Parent);
        player.UpdateMovableRegion(region);
    }

    private static void HandleMinimize(GameWindow window)
    {
        window.OnMinimize();
    }

    private static void HandleRestore(GameWindow window)
    {
        window.OnRestore();
        WindowManager.Instance.CheckPotentialParentWindow(window);
    }

    private static void UpdatePlayerMovableRegion(GameWindow window)
    {
        var player = MainGame.GetPlayer();
        if (player?.Parent == null) return;

        var region = WindowManager.Instance.CalculateMovableRegion(player.Parent);
        player.UpdateMovableRegion(region);
    }
}