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
        //Stage3
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size, string? text)>
            {
                (WindowType.Movable, new Point(50, 600), new Size(200, 200),null),
                (WindowType.Resizable, new Point(50, 200), new Size(200, 200),null),
                (WindowType.Normal, new Point(1000, 600), new Size(500, 200),null),
                (WindowType.TextDisplay, new Point(350, 50), new Size(300, 100), "Stage 4"),
            },
            GoalPosition = new Point(1300, 700),
            GoalInFront = true,
            PlayerStartPosition = new Point(150, 650),
            NoEntryZones = new List<(Point, Size)>
            {
                (new Point(660, 0), new Size(100, 400)),
                (new Point(660, 500), new Size(100, 400)),
            }
        });
        // ステージデータの初期化
        //Stage1
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size,string? text)>
            {
                (WindowType.Normal, new Point(100, 600), new Size(500, 200),null),
                (WindowType.Normal, new Point(500, 500), new Size(600, 200),null),
                (WindowType.TextDisplay, new Point(350, 50), new Size(300, 100), "Stage 1"),
            },
            GoalPosition = new Point(1000, 600),
            GoalInFront = true,
            PlayerStartPosition = new Point(150, 650),
            NoEntryZones = new List<(Point, Size)>
            {
            }
        });

        //Stage2
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size, string? text)>
            {
                (WindowType.Normal, new Point(100, 600), new Size(500, 200),null),
                (WindowType.Movable, new Point(550, 600), new Size(200, 200),null),
                (WindowType.Normal, new Point(1000, 600), new Size(500, 200),null),
                (WindowType.TextDisplay, new Point(350, 50), new Size(300, 100), "Stage 2"),
            },
            GoalPosition = new Point(1300, 700),
            GoalInFront = true,
            PlayerStartPosition = new Point(150, 650),
            NoEntryZones = new List<(Point, Size)>
            {
            }
        });
    }

    public void StartStage(int stageNumber)
    {
        if (stageNumber < 0 || stageNumber >= stages.Count) return;

        // 現在のステージをクリア
        WindowManager.Instance.ClearWindows();
        currentGoal?.Close();
        NoEntryZoneManager.Instance.ClearZones();

        currentStage = stageNumber;
        var stageData = stages[currentStage];

        // 不可侵領域の生成
        foreach (var zoneData in stageData.NoEntryZones)
        {
            NoEntryZoneManager.Instance.AddZone(zoneData.location, zoneData.size);
        }

        if (stageData.GoalInFront)
        {
            // ゴールを最前面に生成
            currentGoal = new Goal(stageData.GoalPosition, true);
            currentGoal.Show();

            // ウィンドウを生成
            foreach (var windowData in stageData.Windows)
            {
                WindowFactory.CreateWindow(windowData.type, windowData.location, windowData.size,windowData.text);
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
                WindowFactory.CreateWindow(windowData.type, windowData.location, windowData.size,windowData.text);
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
    }

    private void OnGoalReached()
    {
        // 次のステージへ
        StartStage(currentStage + 1);
    }
}

// StageDataに不可侵領域の情報を追加
public class StageData
{
    public List<(WindowType type, Point location, Size size, string? text)> Windows { get; set; }
    public Point GoalPosition { get; set; }
    public bool GoalInFront { get; set; }
    public Point PlayerStartPosition { get; set; }
    public List<(Point location, Size size)> NoEntryZones { get; set; } = new List<(Point, Size)>();  // 追加
}