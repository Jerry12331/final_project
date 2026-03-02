using System.Collections.Generic;

namespace GKR_Backend
{
    public class GkrRequest
    {
        public int[][] Circuit { get; set; }
        public int[] Inputs { get; set; }
        public int Mod { get; set; } = 97;
    }

    // ⭐️ 這是我們新增加的結構化事件模型
    public class GkrEvent
    {
        public int ProtocolLayer { get; set; } // 0, 1, 2...
        public int Round { get; set; }         // 1, 2, 3...
        public string Role { get; set; }       // "Prover", "Verifier", 或 "System"
        public string Message { get; set; }    // 具體的對話內容
    }

    public class GkrResponse
    {
        // ⭐️ 把原本的 List<string> 改成 List<GkrEvent>
        public List<GkrEvent> Log { get; set; }
    }
}