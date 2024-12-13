using MultiWindowActionGame;
using System.Diagnostics;
using System.Numerics;

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
    private ToTitaleButton? currentToTitaleButton;

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
                (WindowType.NormalBlack, new Point(500, 600), new Size(500, 150),null),
                (WindowType.NormalBlack, new Point(200, 450), new Size(400, 200),null),
                (WindowType.NormalBlack, new Point(900, 450), new Size(400, 200),null),
                (WindowType.NormalBlack, new Point(200, 300), new Size(400, 200),null),
                (WindowType.NormalBlack, new Point(900, 300), new Size(400, 200),null),
                (WindowType.TextDisplay, new Point(500, 120), new Size(500, 250), "Window Action Game"),
                // 必要に応じて他のウィンドウを追加
            },
            PlayerStartPosition = new Point(730, 650),
            StartButtonPosition = new Point(680, 400),
            IsTitleStage = true
        });


        //Stage4
        //親子関係＆Zバッファ



        //Stage1
        //操作方法と移動をさせる
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size,string? text)>
            {
                (WindowType.NormalBlack, new Point(50, 50), new Size(200, 100),null),// リトライボタン用
                (WindowType.NormalBlack, new Point(260, 50), new Size(200, 100),null),// タイトルボタン用

                (WindowType.NormalBlack, new Point(100, 600), new Size(500, 200),null),
                (WindowType.NormalWhite, new Point(500, 500), new Size(600, 200),null),
                (WindowType.TextDisplay, new Point(500, 50), new Size(300, 100), "Stage 1"),
            },
            GoalPosition = new Point(1000, 600),
            GoalInFront = true,
            PlayerStartPosition = new Point(150, 650),
            NoEntryZones = new List<(Point, Size)>
            {
            },
            ToTitaleButtonPosition = new Point(85, 90),
            RetryButtonPosition = new Point(295, 90),
        });

        //Stage2
        //ムーバル
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size, string? text)>
            {
                (WindowType.NormalBlack, new Point(50, 50), new Size(200, 100),null),// リトライボタン用
                (WindowType.NormalBlack, new Point(260, 50), new Size(200, 100),null),// タイトルボタン用

                (WindowType.NormalBlack, new Point(100, 600), new Size(500, 200),null),
                (WindowType.Movable, new Point(550, 610), new Size(200, 200),null),
                (WindowType.NormalBlack, new Point(1000, 600), new Size(500, 200),null),
                (WindowType.TextDisplay, new Point(500, 50), new Size(300, 100), "Stage 2"),
            },
            GoalPosition = new Point(1300, 700),
            GoalInFront = true,
            PlayerStartPosition = new Point(150, 650),
            NoEntryZones = new List<(Point, Size)>
            {
            },

            ToTitaleButtonPosition = new Point(85, 90),
            RetryButtonPosition = new Point(295, 90),
        });
        //Stage3
        //リサイズウィンドウ
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size, string? text)>
            {

                (WindowType.NormalBlack, new Point(50, 50), new Size(200, 100),null),// リトライボタン用
                (WindowType.NormalBlack, new Point(260, 50), new Size(200, 100),null),// タイトルボタン用

                (WindowType.NormalBlack, new Point(100, 200), new Size(500, 200),null),
                (WindowType.Resizable, new Point(580, 200), new Size(200, 200),null),
                 (WindowType.NormalBlack, new Point(1000, 600), new Size(500, 200),null),
                (WindowType.TextDisplay, new Point(500, 50), new Size(300, 100), "Stage 3"),
            },
            GoalPosition = new Point(1300, 700),
            GoalInFront = true,
            PlayerStartPosition = new Point(150, 250),
            NoEntryZones = new List<(Point, Size)>
            {
            },

            ToTitaleButtonPosition = new Point(85, 90),
            RetryButtonPosition = new Point(295, 90),
        });
        //Stage4
        //不可侵領域
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size, string? text)>
            {
                (WindowType.NormalBlack, new Point(50, 50), new Size(200, 100),null),// リトライボタン用
                (WindowType.NormalBlack, new Point(260, 50), new Size(200, 100),null),// タイトルボタン用

                (WindowType.NormalBlack, new Point(100, 600), new Size(300, 200),null),
                (WindowType.Movable, new Point(380, 580), new Size(200, 200),null),
                (WindowType.NormalBlack, new Point(1000, 600), new Size(500, 200),null),
                (WindowType.TextDisplay, new Point(500, 50), new Size(300, 100), "Stage 4"),
            },
            GoalPosition = new Point(1300, 700),
            GoalInFront = true,
            PlayerStartPosition = new Point(150, 650),
            NoEntryZones = new List<(Point, Size)>
            {
                (new Point(660, 500), new Size(100, 400)),
            },

            ToTitaleButtonPosition = new Point(85, 90),
            RetryButtonPosition = new Point(295, 90),
        });

        //Stage5
        //Zバッファが当たり判定
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size, string? text)>
            {
                (WindowType.NormalBlack, new Point(50, 50), new Size(200, 100),null),// リトライボタン用
                (WindowType.NormalBlack, new Point(260, 50), new Size(200, 100),null),// タイトルボタン用

                (WindowType.NormalBlack, new Point(50, 670), new Size(500, 200),null),
                (WindowType.NormalWhite, new Point(450, 540), new Size(500, 200),null),
                (WindowType.NormalBlack, new Point(850, 410), new Size(500, 200),null),
                (WindowType.NormalWhite, new Point(450, 280), new Size(500, 200),null),
                (WindowType.NormalBlack, new Point(50, 150), new Size(500, 200),null),
                (WindowType.TextDisplay, new Point(500, 50), new Size(300, 100), "Stage 5"),
            },
            GoalPosition = new Point(100, 800),
            GoalInFront = true,
            PlayerStartPosition = new Point(350, 300),
            NoEntryZones = new List<(Point, Size)>
            {
            },

            ToTitaleButtonPosition = new Point(85, 90),
            RetryButtonPosition = new Point(295, 90),
        });
        //Stage5
        //Zバッファ2
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size, string? text)>
            {
                (WindowType.NormalBlack, new Point(50, 50), new Size(200, 100),null),// リトライボタン用
                (WindowType.NormalBlack, new Point(260, 50), new Size(200, 100),null),// タイトルボタン用

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

                (WindowType.TextDisplay, new Point(350, 50), new Size(500, 100), "Stage 5"),
            },
            GoalPosition = new Point(1200, 200),
            GoalInFront = true,
            PlayerStartPosition = new Point(250, 250),
            NoEntryZones = new List<(Point, Size)>
            {
            },

            ToTitaleButtonPosition = new Point(85, 90),
            RetryButtonPosition = new Point(295, 90),
        });
        //Stage6
        //最初から親子関係のあるウィンドウ
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size, string? text)>
            {
                (WindowType.NormalBlack, new Point(50, 50), new Size(200, 100),null),// リトライボタン用
                (WindowType.NormalBlack, new Point(260, 50), new Size(200, 100),null),// タイトルボタン用

                (WindowType.Resizable, new Point(625,100), new Size(1000, 600),null),
                (WindowType.NormalBlack, new Point(675, 100), new Size(300, 200),null),
                (WindowType.NormalBlack, new Point(75, 75), new Size(300, 150),null),
                (WindowType.TextDisplay, new Point(350, 50), new Size(500, 100), "Stage 6"),
            },
            GoalPosition = new Point(100, 150),
            GoalInFront = true,
            PlayerStartPosition = new Point(1050, 600),
            NoEntryZones = new List<(Point, Size)>
            {
            },

            ToTitaleButtonPosition = new Point(85, 90),
            RetryButtonPosition = new Point(295, 90),
        });
        //Stage7
        //親子関係を利用する
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size, string? text)>
            {
                (WindowType.NormalBlack, new Point(50, 50), new Size(200, 100),null),// リトライボタン用
                (WindowType.NormalBlack, new Point(260, 50), new Size(200, 100),null),// タイトルボタン用

                (WindowType.Movable, new Point(50, 600), new Size(200, 200),null),
                (WindowType.Resizable, new Point(50, 200), new Size(200, 200),null),
                (WindowType.NormalBlack, new Point(1000, 600), new Size(500, 200),null),
                (WindowType.TextDisplay, new Point(350, 50), new Size(500, 100), "Stage 7"),
            },
            GoalPosition = new Point(1300, 700),
            GoalInFront = true,
            PlayerStartPosition = new Point(150, 650),
            NoEntryZones = new List<(Point, Size)>
            {
                (new Point(860, 0), new Size(100, 400)),
                (new Point(860, 500), new Size(100, 400)),
            },

            ToTitaleButtonPosition = new Point(85, 90),
            RetryButtonPosition = new Point(295, 90),
        });
        //Stage8
        //最小化未定
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size, string? text)>
            {
                (WindowType.NormalBlack, new Point(50, 50), new Size(200, 100),null),// リトライボタン用
                (WindowType.NormalBlack, new Point(260, 50), new Size(200, 100),null),// タイトルボタン用

                (WindowType.NormalBlack, new Point(500, 250), new Size(500, 600),null),
                (WindowType.Minimizable, new Point(500, 400), new Size(500, 200),null),
                (WindowType.TextDisplay, new Point(500, 50), new Size(300, 100), "Stage 8"),
            },
            GoalPosition = new Point(600, 700),
            GoalInFront = true,
            PlayerStartPosition = new Point(6000, 450),
            NoEntryZones = new List<(Point, Size)>
            {
            },

            ToTitaleButtonPosition = new Point(85, 90),
            RetryButtonPosition = new Point(295, 90),
        });
        //クリア画面
        stages.Add(new StageData
        {
            Windows = new List<(WindowType type, Point location, Size size, string? text)>
            {
                (WindowType.NormalBlack, new Point(500, 600), new Size(500, 150),null),
                (WindowType.TextDisplay, new Point(200, 450), new Size(400, 200),"Created by"),
                (WindowType.TextDisplay, new Point(900, 450), new Size(400, 200),"Megutan"),
                (WindowType.NormalBlack, new Point(200, 300), new Size(400, 200),null),
                (WindowType.NormalBlack, new Point(900, 300), new Size(400, 200),null),
                (WindowType.TextDisplay, new Point(500, 120), new Size(500, 250), "Thank you!!"),
                // 必要に応じて他のウィンドウを追加
            },
            PlayerStartPosition = new Point(730, 650),
            ToTitaleButtonPosition = new Point(680, 400),  //画面中央付近
            IsTitleStage = true
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
        currentToTitaleButton?.Close();

        currentStage = stageNumber;
        var stageData = stages[currentStage];

        // プレイヤーの処理
        var player = MainGame.GetPlayer();
        if (player == null)
        {
            // プレイヤーがまだ存在しない場合は新規作成
            MainGame.Instance.InitializePlayer(stageData.PlayerStartPosition);
        }
        else
        {
            // 既存のプレイヤーの位置をリセット
            player.Show();
            player.ResetSize(new Size(40, 40));
            player.ResetPosition(stageData.PlayerStartPosition);
        }

        // ゴールの生成（タイトル画面では生成しない）
        if (!stageData.IsTitleStage)
        {
            if (stageData.GoalInFront)
            {
                currentGoal = new Goal(stageData.GoalPosition, true);
                currentGoal.Show();
            }
            else
            {
                currentGoal = new Goal(stageData.GoalPosition, false);
                currentGoal.Show();
            }
        }

        // ボタンの生成
        if (stageData.RetryButtonPosition.HasValue)
        {
            currentRetryButton = new RetryButton(stageData.RetryButtonPosition.Value);
            currentRetryButton.Show();
        }

        if (stageData.StartButtonPosition.HasValue)
        {
            currentStartButton = new StartButton(stageData.StartButtonPosition.Value);
            currentStartButton.Show();
        }

        if (stageData.ToTitaleButtonPosition.HasValue)
        {
            currentToTitaleButton = new ToTitaleButton(stageData.ToTitaleButtonPosition.Value);
            currentToTitaleButton.Show();
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
    }

    public void RestartCurrentStage()
    {
        StartStage(currentStage);
    }
    public void ToTitaleStage()
    {
        StartStage(0);
        var player = MainGame.GetPlayer();
        player.Show();
        player.ResetSize(new Size(40, 40));
        player.ResetPosition(stages[0].PlayerStartPosition);
    }
    public StageData GetStage(int stageNumber)
    {
        if (stageNumber < 0 || stageNumber >= stages.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(stageNumber));
        }
        return stages[stageNumber];
    }
    public void EnsureButtonsTopMost()
    {
        if (currentRetryButton != null && !currentRetryButton.IsDisposed)
        {
            currentRetryButton.EnsureTopMost();
        }
        if (currentStartButton != null && !currentStartButton.IsDisposed)
        {
            currentStartButton.EnsureTopMost();
        }
        if (currentToTitaleButton != null && !currentToTitaleButton.IsDisposed)
        {
            currentToTitaleButton.EnsureTopMost();
        }
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
        var nextStageNumber = currentStage + 1;
        if (nextStageNumber < stages.Count)
        {
            // プレイヤーを次のステージの開始位置に設定
            var nextStageData = stages[nextStageNumber];
            var player = MainGame.GetPlayer();
            if (player != null)
            {
                player.ResetPosition(nextStageData.PlayerStartPosition);
            }

            StartStage(nextStageNumber);
        }
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
    public Point? ToTitaleButtonPosition { get; set; }
    public bool IsTitleStage { get; set; }
}