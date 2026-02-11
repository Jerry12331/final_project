namespace GKR_Backend
{
    // 對應前端傳來的 JSON
    public class GkrRequest
    {
        // 對應 [[0,1], [1,0]]：0代表加法, 1代表乘法
        public int[][] Circuit { get; set; }

        // 對應 [3, 5, 2, 7]
        public int[] Inputs { get; set; }

        // 預設 Mod，前端沒傳就用 97
        public int Mod { get; set; } = 97;
    }

    // 回傳給前端的格式
    public class GkrResponse
    {
        public List<string> Log { get; set; }
    }
}