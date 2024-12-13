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
    private RetryButton? currentRetryButton;
    private StartButton? currentStartButton;

    private StageManager()
    {
        InitializeStages();
    }

    private void InitializeStages()
    {

        // ステージデータの初期化

        // タイトルステージ（インデックス0）
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size, string? text)>
            {
                (WindowType.TextDisplay, new Point(300, 100), new Size(400, 150), "Window Action Game"),
                (WindowType.NormalBlack, new Point(300, 500), new Size(200, 100),null),
                // 必要に応じて他のウィンドウを追加
            },
            StartButtonPosition = new Point(450, 300),  // 画面中央付近
            IsTitleStage = true
        });

        //Stage4
        //親子関係＆Zバッファ
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size, string? text)>
            {
                (WindowType.Movable, new Point(50, 600), new Size(200, 200),null),
                (WindowType.Resizable, new Point(50, 200), new Size(200, 200),null),
                (WindowType.Minimizable, new Point(1000, 600), new Size(500, 200),null),
                (WindowType.TextDisplay, new Point(350, 50), new Size(300, 100), "Stage 8"),
            },
            GoalPosition = new Point(1300, 700),
            GoalInFront = true,
            PlayerStartPosition = new Point(150, 650),
            NoEntryZones = new List<(Point, Size)>
            {
                (new Point(860, 0), new Size(400, 400)),
                (new Point(860, 500), new Size(400, 400)),
            },
             RetryButtonPosition = new Point(10, 10),
        });

        //Stage8
        //最小化未定
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size, string? text)>
            {
                (WindowType.Movable, new Point(50, 600), new Size(200, 200),null),
                (WindowType.Resizable, new Point(50, 200), new Size(200, 200),null),
                (WindowType.Minimizable, new Point(1000, 600), new Size(500, 200),null),
                (WindowType.TextDisplay, new Point(350, 50), new Size(300, 100), "Stage 8"),
            },
            GoalPosition = new Point(1300, 700),
            GoalInFront = true,
            PlayerStartPosition = new Point(150, 650),
            NoEntryZones = new List<(Point, Size)>
            {
                (new Point(860, 0), new Size(100, 400)),
                (new Point(860, 500), new Size(100, 400)),
            },


             RetryButtonPosition = new Point(100, 100),
        });

        //Stage1
        //操作方法と移動をさせる
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size,string? text)>
            {
                (WindowType.NormalBlack, new Point(100, 600), new Size(500, 200),null),
                (WindowType.NormalWhite, new Point(500, 500), new Size(600, 200),null),
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
        //ムーバル
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size, string? text)>
            {
                (WindowType.NormalBlack, new Point(100, 600), new Size(500, 200),null),
                (WindowType.Movable, new Point(550, 610), new Size(200, 200),null),
                (WindowType.NormalBlack, new Point(1000, 600), new Size(500, 200),null),
                (WindowType.TextDisplay, new Point(350, 50), new Size(300, 100), "Stage 2"),
            },
            GoalPosition = new Point(1300, 700),
            GoalInFront = true,
            PlayerStartPosition = new Point(150, 650),
            NoEntryZones = new List<(Point, Size)>
            {
            }
        });
        //Stage3
        //リサイズウィンドウ
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size, string? text)>
            {
                (WindowType.NormalBlack, new Point(100, 200), new Size(500, 200),null),
                (WindowType.Resizable, new Point(580, 200), new Size(200, 200),null),
                 (WindowType.NormalBlack, new Point(1000, 600), new Size(500, 200),null),
                (WindowType.TextDisplay, new Point(350, 50), new Size(300, 100), "Stage 3"),
            },
            GoalPosition = new Point(1300, 700),
            GoalInFront = true,
            PlayerStartPosition = new Point(150, 250),
            NoEntryZones = new List<(Point, Size)>
            {
            }
        });
        //Stage4
        //不可侵領域
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size, string? text)>
            {
                (WindowType.NormalBlack, new Point(100, 600), new Size(300, 200),null),
                (WindowType.Movable, new Point(380, 580), new Size(200, 200),null),
                (WindowType.NormalBlack, new Point(1000, 600), new Size(500, 200),null),
                (WindowType.TextDisplay, new Point(350, 50), new Size(300, 100), "Stage 4"),
            },
            GoalPosition = new Point(1300, 700),
            GoalInFront = true,
            PlayerStartPosition = new Point(150, 650),
            NoEntryZones = new List<(Point, Size)>
            {
                (new Point(660, 500), new Size(100, 400)),
            }
        });

        //Stage5
        //Zバッファが当たり判定
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size, string? text)>
            {
                (WindowType.NormalBlack, new Point(50, 670), new Size(500, 200),null),
                (WindowType.NormalWhite, new Point(450, 540), new Size(500, 200),null),
                (WindowType.NormalBlack, new Point(850, 410), new Size(500, 200),null),
                (WindowType.NormalWhite, new Point(450, 280), new Size(500, 200),null),
                (WindowType.NormalBlack, new Point(50, 150), new Size(500, 200),null),
                (WindowType.TextDisplay, new Point(350, 50), new Size(300, 100), "Stage 5"),
            },
            GoalPosition = new Point(100, 800),
            GoalInFront = true,
            PlayerStartPosition = new Point(350, 300),
            NoEntryZones = new List<(Point, Size)>
            {
            }
        });
        //Stage5
        //Zバッファ2
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size, string? text)>
            {
                (WindowType.NormalBlack, new Point(050, 670), new Size(800, 200),null),
                (WindowType.NormalWhite, new Point(050, 540), new Size(500, 200),null),
                (WindowType.NormalBlack, new Point(050, 410), new Size(500, 200),null),
                (WindowType.NormalWhite, new Point(050, 280), new Size(500, 200),null),
                (WindowType.NormalBlack, new Point(050, 150), new Size(500, 200),null),


                (WindowType.NormalBlack, new Point(850, 150), new Size(500, 200),null),
                (WindowType.NormalWhite, new Point(850, 280), new Size(500, 200),null),
                (WindowType.NormalBlack, new Point(850, 410), new Size(500, 200),null),
                (WindowType.NormalWhite, new Point(850, 540), new Size(500, 200),null),
                (WindowType.NormalBlack, new Point(550, 670), new Size(800, 200),null),

                (WindowType.TextDisplay, new Point(350, 50), new Size(300, 100), "Stage 5"),
            },
            GoalPosition = new Point(1200, 200),
            GoalInFront = true,
            PlayerStartPosition = new Point(250, 250),
            NoEntryZones = new List<(Point, Size)>
            {
            }
        });
        //Stage6
        //最初から親子関係のあるウィンドウ
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size, string? text)>
            {
                (WindowType.Resizable, new Point(625,100), new Size(1000, 600),null),
                (WindowType.NormalBlack, new Point(675, 100), new Size(300, 200),null),
                (WindowType.NormalBlack, new Point(75, 75), new Size(300, 150),null),
                (WindowType.TextDisplay, new Point(350, 50), new Size(300, 100), "Stage 6"),
            },
            GoalPosition = new Point(100, 150),
            GoalInFront = true,
            PlayerStartPosition = new Point(1050, 600),
            NoEntryZones = new List<(Point, Size)>
            {
            }
        });
        //Stage7
        //親子関係を利用する
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size, string? text)>
            {
                (WindowType.Movable, new Point(50, 600), new Size(200, 200),null),
                (WindowType.Resizable, new Point(50, 200), new Size(200, 200),null),
                (WindowType.NormalBlack, new Point(1000, 600), new Size(500, 200),null),
                (WindowType.TextDisplay, new Point(350, 50), new Size(300, 100), "Stage 7"),
            },
            GoalPosition = new Point(1300, 700),
            GoalInFront = true,
            PlayerStartPosition = new Point(150, 650),
            NoEntryZones = new List<(Point, Size)>
            {
                (new Point(860, 0), new Size(100, 400)),
                (new Point(860, 500), new Size(100, 400)),
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
        currentRetryButton?.Close();
        currentStartButton?.Close();

        currentStage = stageNumber;
        var stageData = stages[currentStage];
        if (stageData == null) return;

        if (!stageData.IsTitleStage)
        {
            // 通常ステージの場合のみプレイヤーを生成
            MainGame.Instance.InitializePlayer(stageData.PlayerStartPosition);
            if (stageData.GoalInFront)
            {
                // ゴールを最前面に生成
                currentGoal = new Goal(stageData.GoalPosition, true);
                currentGoal.Show();
            }
            else
            {
                // ゴールを最後方に生成
                currentGoal = new Goal(stageData.GoalPosition, false);
                currentGoal.Show();
            }
        }
        // タイトル画面以外の場合のみリトライボタンを生成
        if (!stageData.IsTitleStage && stageData.RetryButtonPosition.HasValue)
        {
            currentRetryButton = new RetryButton(stageData.RetryButtonPosition.Value);
            currentRetryButton.Show();
        }

        // タイトル画面の場合はStartボタンを生成
        if (stageData.IsTitleStage && stageData.StartButtonPosition.HasValue)
        {
            currentStartButton = new StartButton(stageData.StartButtonPosition.Value);
            currentStartButton.Show();
        }

        // 不可侵領域の生成
        foreach (var zoneData in stageData.NoEntryZones)
        {
            NoEntryZoneManager.Instance.AddZone(zoneData.location, zoneData.size);
        }

        // ウィンドウを生成
        foreach (var windowData in stageData.Windows)
        {
            WindowFactory.CreateWindow(windowData.type, windowData.location, windowData.size, windowData.text);
        }

        // プレイヤーの位置をリセット
        var player = MainGame.GetPlayer();
        if (player != null)
        {
            player.ResetPosition(stageData.PlayerStartPosition);
            player.ResetSize(new Size(40,40));
        }
    }

    public void RestartCurrentStage()
    {
        StartStage(currentStage);
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
    public void StartNextStage()
    {
        StartStage(currentStage + 1);
    }
}

public class StageData
{
    public List<(WindowType type, Point location, Size size, string? text)> Windows { get; set; }
    public Point GoalPosition { get; set; }
    public bool GoalInFront { get; set; }
    public Point PlayerStartPosition { get; set; }
    public List<(Point location, Size size)> NoEntryZones { get; set; } = new List<(Point, Size)>();
    public Point? RetryButtonPosition { get; set; }
    public Point? StartButtonPosition { get; set; }
    public bool IsTitleStage { get; set; }
}