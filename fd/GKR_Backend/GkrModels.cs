using System.Collections.Generic;

namespace GKR_Backend
{
    public class GkrRequest
    {
        public int[][]? Circuit { get; set; }
        public int[]? Inputs { get; set; }

        // 明確連線：[layer][gate][0=left, 1=right]
        // 若為 null 則退回二元樹預設（2j, 2j+1）
        public int[][][]? Connections { get; set; }

        // Hidden witness values
        public int[]? HiddenValues { get; set; }

        public int Mod { get; set; } = 97;
    }

    public class GkrEvent
    {
        public int ProtocolLayer { get; set; }
        public int Round { get; set; }
        public string? Role { get; set; }
        public string? Type { get; set; }
        public string? Message { get; set; }
        
        // ⭐️ 將這裡改成 object?，前端才能接收彈性的 JSON (如 Commitment 資料)
        public object? Data { get; set; } 
    }

    public class GkrResponse
    {
        public List<GkrEvent>? Log { get; set; }
    }
}