// TODO

namespace MediaPipeMotionCapture.Visualization
{
    /// <summary>
    /// 手の骨格接続定義。MediaPipe Hand の 21 ランドマークに基づく。
    /// </summary>
    public static class HandConnections
    {
        public static readonly (int, int)[] Connections = new (int, int)[]
        {
            // 手首 → 各指根元 (5本)
            (0, 1),   // 手首 → 親指CMC
            (0, 5),   // 手首 → 人差し指MCP
            (0, 9),   // 手首 → 中指MCP
            (0, 13),  // 手首 → 薬指MCP
            (0, 17),  // 手首 → 小指MCP

            // 親指 (3本)
            (1, 2),   // CMC → MCP
            (2, 3),   // MCP → IP
            (3, 4),   // IP → TIP

            // 人差し指 (3本)
            (5, 6),   // MCP → PIP
            (6, 7),   // PIP → DIP
            (7, 8),   // DIP → TIP

            // 中指 (3本)
            (9, 10),  // MCP → PIP
            (10, 11), // PIP → DIP
            (11, 12), // DIP → TIP

            // 薬指 (3本)
            (13, 14), // MCP → PIP
            (14, 15), // PIP → DIP
            (15, 16), // DIP → TIP

            // 小指 (3本)
            (17, 18), // MCP → PIP
            (18, 19), // PIP → DIP
            (19, 20), // DIP → TIP
        };
    }
}
