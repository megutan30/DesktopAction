using MultiWindowActionGame;
using System.Diagnostics;

public class StageManager
{
    private static readonly Lazy<StageManager> lazy =
        new Lazy<StageManager>(() => new StageManager());
    public static StageManager Instance { get { return lazy.Value; } }

    private int currentStage = 0;
    private Goal? currentGoal;
    public Goal? CurrentGoal=>currentGoal;
    private List<StageData> stages = new List<StageData>();

    private StageManager()
    {
        InitializeStages();
    }

    private void InitializeStages()
    {
        // ステージデータの初期化
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size)>
            {
                (WindowType.Normal, new Point(100, 600), new Size(1000, 200)),
            },
            GoalPosition = new Point(1000, 700),
            GoalInFront = true,
            PlayerStartPosition = new Point(150, 650)
        });

        // 他のステージも追加
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size)>
            {
                (WindowType.Resizable, new Point(100, 100), new Size(300, 200)),
                (WindowType.Movable, new Point(450, 100), new Size(300, 200))
            },
            GoalPosition = new Point(700, 300),
            GoalInFront = false,
            PlayerStartPosition = new Point(150, 150)
        });

    }

    public void StartStage(int stageNumber)
    {
        if (stageNumber < 0 || stageNumber >= stages.Count) return;

        // 現在のステージをクリア
        WindowManager.Instance.ClearWindows();
        currentGoal?.Close();

        currentStage = stageNumber;
        var stageData = stages[currentStage];

        if (stageData.GoalInFront)
        {
            // ゴールを最前面に生成
            currentGoal = new Goal(stageData.GoalPosition, true);
            currentGoal.Show();

            // ウィンドウを生成
            foreach (var windowData in stageData.Windows)
            {
                WindowFactory.CreateWindow(windowData.type, windowData.location, windowData.size);
            }
        }
        else
        {
            // ゴールを最後方に生成
            currentGoal = new Goal(stageData.GoalPosition, false);
            currentGoal.Show();

            // ウィンドウを生成
            foreach (var windowData in stageData.Windows)
            {
                WindowFactory.CreateWindow(windowData.type, windowData.location, windowData.size);
            }
        }

        // プレイヤーの位置をリセット
        var player = MainGame.GetPlayer();
        if (player != null)
        {
            player.ResetPosition(stageData.PlayerStartPosition);
        }
    }
    public StageData GetStage(int stageNumber)
    {
        if (stageNumber < 0 || stageNumber >= stages.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(stageNumber));
        }
        return stages[stageNumber];
    }
    public bool CheckGoal(Player player)
    {
        if (currentGoal == null) return false;

        // デバッグ情報を出力
        if (MainGame.IsDebugMode)
        {
            Debug.WriteLine($"Player Bounds: {player.Bounds}");
            Debug.WriteLine($"Goal Bounds: {currentGoal.Bounds}");
            Debug.WriteLine($"Intersects: {player.Bounds.IntersectsWith(currentGoal.Bounds)}");
        }

        // プレイヤーとゴールの重なりをチェック
        if (!player.Bounds.IntersectsWith(currentGoal.Bounds))
        {
            return false;
        }

        // Z-バッファのチェック
        var intersectingWindows = WindowManager.Instance.GetIntersectingWindows(
            Rectangle.Intersect(player.Bounds, currentGoal.Bounds)
        ).ToList();

        // デバッグ出力
        System.Diagnostics.Debug.WriteLine($"Intersecting windows count: {intersectingWindows.Count}");
        foreach (var window in intersectingWindows)
        {
            System.Diagnostics.Debug.WriteLine($"Window: {window.GetType().Name} at Z-index: {WindowManager.Instance.GetWindowZIndex(window)}");
        }

        if (true)
        {
            // ゴール達成
            System.Diagnostics.Debug.WriteLine("Goal reached!");
            OnGoalReached();
            return true;
        }

        return false;
    }

    private void OnGoalReached()
    {
        // 次のステージへ
        StartStage(currentStage + 1);
    }
}

public class StageData
{
    public List<(WindowType type, Point location, Size size)> Windows { get; set; }
    public Point GoalPosition { get; set; }
    public bool GoalInFront { get; set; }
    public Point PlayerStartPosition { get; set; }  // プレイヤーの開始位置を追加
}