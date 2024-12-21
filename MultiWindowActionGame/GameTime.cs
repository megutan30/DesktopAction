using System.Diagnostics;

public class GameTime
{
    private static Stopwatch stopwatch = new Stopwatch();
    private static long lastTime = 0;
    private static bool isPaused = false;  // ポーズフラグを追加

    public static float DeltaTime { get; private set; }
    public static float TotalTime { get; private set; }

    public static void Start()
    {
        stopwatch.Start();
    }

    public static void SetPaused(bool paused)
    {
        isPaused = paused;
        if (!paused)
        {
            // ポーズ解除時に前回の時間を現在の時間に更新
            lastTime = stopwatch.ElapsedMilliseconds;
        }
    }

    public static void Update()
    {
        long currentTime = stopwatch.ElapsedMilliseconds;
        // ポーズ中はDeltaTimeを0にする
        DeltaTime = isPaused ? 0 : (currentTime - lastTime) / 1000f;
        TotalTime = currentTime / 1000f;
        if (!isPaused)
        {
            lastTime = currentTime;
        }
    }
}