using System;
using System.Collections.Generic;
using System.Linq;

namespace GKR_Backend.Services
{
    public class GkrService
    {
        // ⭐️ 改用 GkrEvent 串列
        private List<GkrEvent> _events;

        private int _currentLayer = 0;
        private int _currentRound = 1;

        public List<GkrEvent> RunGkr(GkrRequest request)
        {
            _events = new List<GkrEvent>();
            try
            {
                RunProtocolInternal(request);
            }
            catch (Exception ex)
            {
                AddSystemEvent($"Error: {ex.Message}");
                AddSystemEvent($"StackTrace: {ex.StackTrace}");
            }
            return _events;
        }

        private static Dictionary<string, int> Data(params (string key, int value)[] items)
        {
            return items.ToDictionary(item => item.key, item => item.value);
        }

        private void AddProverEvent(string msg, string type = "PROVER_MESSAGE", Dictionary<string, int>? data = null)
        {
            _events.Add(new GkrEvent { ProtocolLayer = _currentLayer, Round = _currentRound, Role = "Prover", Type = type, Message = msg, Data = data });
        }

        private void AddVerifierEvent(string msg, bool incrementRound = false, string type = "VERIFIER_MESSAGE", Dictionary<string, int>? data = null)
        {
            _events.Add(new GkrEvent { ProtocolLayer = _currentLayer, Round = _currentRound, Role = "Verifier", Type = type, Message = msg, Data = data });
            if (incrementRound) _currentRound++;
        }

        private void AddSystemEvent(string msg, string type = "SYSTEM_MESSAGE", Dictionary<string, int>? data = null)
        {
             _events.Add(new GkrEvent { ProtocolLayer = _currentLayer, Round = _currentRound, Role = "System", Type = type, Message = msg, Data = data });
        }


        private void RunProtocolInternal(GkrRequest req)
        {
            int mod = req.Mod;
            int[][] rawCircuit = req.Circuit;
            int[] inputs = req.Inputs;

            int totalLayers = rawCircuit.Length + 1;
            Node[][] circuit = new Node[totalLayers][];

            _currentLayer = 0; 
            _currentRound = 1; 
            AddSystemEvent($"嘿，大家好！我們要開始 GKR 協議了。模數是 {mod}，總共有 {totalLayers} 層電路。");

            for (int i = 0; i < totalLayers; i++)
            {
                if (i == totalLayers - 1)
                {
                    circuit[i] = new Node[inputs.Length];
                    for (int j = 0; j < inputs.Length; j++)
                    {
                        circuit[i][j] = new Node(inputs[j], j);
                    }
                    AddSystemEvent($"第 {i} 層是輸入層，有 {inputs.Length} 個輸入值：{string.Join(", ", inputs)}");
                }
                else
                {
                    int[] layerConfig = rawCircuit[i];
                    circuit[i] = new Node[layerConfig.Length];
                    for (int j = 0; j < layerConfig.Length; j++)
                    {
                        circuit[i][j] = new Node(j);
                        circuit[i][j].set_sign(layerConfig[j]);
                    }
                    AddSystemEvent($"第 {i} 層有 {layerConfig.Length} 個閘門，配置是：{string.Join(", ", layerConfig)}（0=加法，1=乘法）");
                }
            }

            for (int i = 0; i < totalLayers - 1; i++)
            {
                for (int j = 0; j < circuit[i].Length; j++)
                {
                    int leftIdx = j * 2;
                    int rightIdx = j * 2 + 1;

                    if (leftIdx < circuit[i + 1].Length && rightIdx < circuit[i + 1].Length)
                    {
                        circuit[i][j].set_left(circuit[i + 1][leftIdx]);
                        circuit[i][j].set_right(circuit[i + 1][rightIdx]);
                    }
                    else
                    {
                        // ⭐️ 加上防呆機制：如果找不到節點，丟出明確的錯誤，而不是讓程式 NullReferenceException
                        throw new Exception($"電路結構不合法！第 {i} 層的第 {j} 個 Gate 找不到下一層的輸入（需要 Index {leftIdx} 與 {rightIdx}，但下一層只有 {circuit[i + 1].Length} 個）。請確保輸入矩陣是嚴格的二元樹結構 (例如: 1 -> 2 -> 4)。");
                    }
                }
            }

            int[] gateNum = new int[totalLayers];
            int[] bitsLen = new int[totalLayers];

            for (int i = 0; i < totalLayers; i++)
            {
                gateNum[i] = circuit[i].Length;
                bitsLen[i] = (gateNum[i] == 1) ? 1 : (int)Math.Ceiling(Math.Log(gateNum[i], 2));
            }

            AddSystemEvent($"計算每層的值... 從最底層開始往上算。");

            for (int i = totalLayers - 1; i >= 0; i--)
            {
                for (int j = 0; j < circuit[i].Length; j++)
                {
                    circuit[i][j].calculate_value();
                }
            }

            Prover prover = new Prover(totalLayers, gateNum, bitsLen, mod, circuit);
            Verifier verifier = new Verifier(mod);

            int[] fixed_var = new int[bitsLen[0]];
            var claimed_D = prover.claimed_D();

            _currentLayer = 0;
            _currentRound = 1;

            string outputVals = "";
            for (int i = 0; i < gateNum[0]; i++) outputVals += claimed_D(IntToBinary(i, bitsLen[0])) + " ";
            AddProverEvent($"先發送整個電路的輸出值：{outputVals}。這是用 D 多項式算出來的，每個輸入對應的結果。");

            for (int i = 0; i < fixed_var.Length; i++) fixed_var[i] = verifier.pickRandom();
            AddVerifierEvent($"我隨機選了一些固定變數：{string.Join(", ", fixed_var)}，用來測試 D 多項式。");

            int claimed = claimed_D(fixed_var);
            AddProverEvent($"用這些固定變數算 D 多項式的值，結果是 {claimed}。這就是聲稱的答案！", "CLAIM_VALUE", Data(("claimed", claimed)));
            
            _currentRound++; 

            for (int now_layer = 0; now_layer < totalLayers - 1; now_layer++)
            {
                _currentLayer = now_layer + 1; // 為了對齊前端視覺，把 Sumcheck 定義在下一層
                _currentRound = 1;         

                AddSystemEvent($"現在進入第 {now_layer + 1} 層的 Sumcheck！Prover 要證明他的聲稱是對的。");

                int maskSum = prover.maskSum(now_layer, fixed_var);
                AddProverEvent($"計算了 maskSum（遮罩和），這是把所有相關的多項式加起來，結果是 {maskSum}。");

                int rho = verifier.pickRandom();
                AddVerifierEvent($"我隨機選了一個 rho = {rho}，用來檢查 Sumcheck。", incrementRound: true, type: "SEND_RHO", data: Data(("rho", rho))); 

                claimed = Mod(claimed + Mod(rho * maskSum, mod), mod);
                AddSystemEvent($"更新聲稱值：原來的 {claimed - Mod(rho * maskSum, mod)} 加上 rho * maskSum = {rho} * {maskSum} = {Mod(rho * maskSum, mod)}，結果是 {claimed}。");
                
                for (int i = 0; i < bitsLen[now_layer + 1] * 2; i++)
                {
                    var G = prover.make_G(fixed_var, now_layer, rho);

                    int g0 = G(0);
                    int g1 = G(1);
                    int sumTerm = Mod((long)g0 + (long)g1, mod);
                    AddProverEvent($"計算了 G 多項式在 0 和 1 的值：G(0) = {g0}, G(1) = {g1}，加起來是 {sumTerm}。");

                    if (sumTerm != claimed)
                    {
                        AddVerifierEvent($"檢查失敗！G(0) + G(1) = {sumTerm}，但聲稱的是 {claimed}，不一樣啊！");
                        return;
                    }

                    int s = verifier.pickRandom();
                    AddVerifierEvent($"檢查通過！G(0)+G(1) 等於聲稱值。現在我隨機選 s{i} = {s}，繼續下一輪。", type: "SEND_S", data: Data(("s", s), ("sIndex", i)));
                    
                    fixed_var = fixed_var.Append(s).ToArray();
                    claimed = G(s);
                    
                    AddProverEvent($"用 s{i} = {s} 算 G 多項式的值，得到新的聲稱 {claimed}。", "CLAIM_VALUE", Data(("claimed", claimed), ("fromS", s), ("sIndex", i)));
                    _currentRound++; 

                    if (now_layer == totalLayers - 2 && i == bitsLen[now_layer + 1] * 2 - 1)
                    {
                        AddSystemEvent($"最後一輪！要檢查輸入層的多項式。");

                        var input_poly = verifier.make_input(circuit[totalLayers - 1], bitsLen[totalLayers - 1]);
                        
                        maskSum = prover.maskSum(now_layer, fixed_var);
                        AddProverEvent($"最後的 maskSum（考慮所有固定變數）= {maskSum}。");
                        
                        claimed = Mod(claimed - Mod(rho * maskSum, mod), mod);
                        AddSystemEvent($"最終聲稱值：原來的 {claimed + Mod(rho * maskSum, mod)} 減去 rho * maskSum = {rho} * {maskSum} = {Mod(rho * maskSum, mod)}，結果是 {claimed}。");

                        int[] a = fixed_var.Take(bitsLen[totalLayers - 2]).ToArray();
                        int[] b = fixed_var.Skip(bitsLen[totalLayers - 2]).Take(bitsLen[totalLayers - 1]).ToArray();
                        int[] c = fixed_var.Skip(bitsLen[totalLayers - 2] + bitsLen[totalLayers - 1]).Take(bitsLen[totalLayers - 1]).ToArray();

                        long final_addPolyVal = AddPoly(totalLayers - 2, circuit, mod)(a, b, c);
                        long final_mulPolyVal = MulPoly(totalLayers - 2, circuit, mod)(a, b, c);
                        long inputB = input_poly(b);
                        long inputC = input_poly(c);
                        long final_part1 = Mod(final_addPolyVal * Mod(inputB + inputC, mod), mod);
                        long final_part2 = Mod(final_mulPolyVal * Mod(inputB * inputC, mod), mod);
                        long finalTerm = Mod(final_part1 + final_part2, mod);

                        AddVerifierEvent($"最終檢查：加法多項式值 {final_addPolyVal} * (輸入B {inputB} + 輸入C {inputC}) = {final_part1}，乘法多項式值 {final_mulPolyVal} * (輸入B * 輸入C = {inputB * inputC}) = {final_part2}，加起來是 {finalTerm}。");

                        if (claimed != finalTerm) { 
                            AddVerifierEvent($"最終檢查失敗！聲稱 {claimed} 不等於計算結果 {finalTerm}。"); 
                            return; 
                        }
                        AddVerifierEvent("太棒了！最終檢查通過，Verifier 相信 D() 是正確的！");
                        break;
                    }

                    if (i == bitsLen[now_layer + 1] * 2 - 1) 
                    {
                        var claimed_poly = prover.make_q(now_layer, fixed_var);
                        AddProverEvent($"發送了聲稱的多項式 q{now_layer + 1}，它的值在 0 和 1 會是 q(0)={claimed_poly(0)}, q(1)={claimed_poly(1)}。");
                        
                        maskSum = prover.maskSum(now_layer, fixed_var);
                        AddProverEvent($"還有 maskSum = {maskSum}。");
                        
                        claimed = Mod(claimed - Mod(rho * maskSum, mod), mod);
                        AddSystemEvent($"更新聲稱：減去 rho * maskSum = {rho} * {maskSum} = {Mod(rho * maskSum, mod)}，新聲稱是 {claimed}。");

                        int[] a = fixed_var.Take(bitsLen[now_layer]).ToArray();
                        int[] b = fixed_var.Skip(bitsLen[now_layer]).Take(bitsLen[now_layer + 1]).ToArray();
                        int[] c = fixed_var.Skip(bitsLen[now_layer] + bitsLen[now_layer + 1]).Take(bitsLen[now_layer + 1]).ToArray();

                        long addPolyVal = AddPoly(now_layer, circuit, mod)(a, b, c);
                        long mulPolyVal = MulPoly(now_layer, circuit, mod)(a, b, c);
                        long q0 = claimed_poly(0);
                        long q1 = claimed_poly(1);
                        long part1 = Mod(addPolyVal * Mod(q0 + q1, mod), mod);
                        long part2 = Mod(mulPolyVal * Mod(q0 * q1, mod), mod);
                        long intermediateTerm = Mod(part1 + part2, mod);

                        AddVerifierEvent($"中間檢查：加法多項式 {addPolyVal} * (q(0)+q(1)={q0 + q1}) = {part1}，乘法多項式 {mulPolyVal} * (q(0)*q(1)={q0 * q1}) = {part2}，總和 {intermediateTerm}。");

                        if (claimed != intermediateTerm) { 
                            AddVerifierEvent("中間檢查失敗！"); 
                            return; 
                        }

                        int random_var = verifier.pickRandom();
                        AddVerifierEvent($"中間檢查通過！選下一個隨機數 r{now_layer + 1} = {random_var}。", incrementRound: true);
                        
                        claimed = claimed_poly(random_var);
                        AddProverEvent($"用 r{now_layer + 1} 算 q 多項式，得到新聲稱 {claimed}。", "CLAIM_VALUE", Data(("claimed", claimed), ("r", random_var), ("layer", now_layer + 1)));

                        var l_poly = prover.make_l(now_layer, fixed_var);
                        Array.Resize(ref fixed_var, bitsLen[now_layer + 1]);
                        for (int j = 0; j < fixed_var.Length; j++)
                        {
                            fixed_var[j] = l_poly(random_var)[j];
                        }
                        AddSystemEvent($"更新固定變數：用 l 多項式算出新的變數 {string.Join(", ", fixed_var)}。");
                    }
                }
            }
        }

        private static int[] IntToBinary(int n, int len)
        {
            int[] bits = new int[len];
            for (int i = 0; i < len; i++) bits[i] = (n >> i) & 1;
            return bits;
        }

        private static int Mod(long a, int mod)
        {
            int res = (int)(a % mod);
            if (res < 0) res += mod;
            return res;
        }

        private static Func<int[], int[], int[], int> AddPoly(int layer, Node[][] circuit, int mod)
        {
            return (int[] a, int[] b, int[] c) =>
            {
                long s = 0;
                for (int i = 0; i < circuit[layer].Length; i++)
                {
                    if (circuit[layer][i].sign == 0)
                    {
                        long term = 1;
                        int[] index = IntToBinary(i, a.Length);
                        for (int j = 0; j < index.Length; j++)
                        {
                            long val = (index[j] == 1) ? a[j] : (1 - a[j]);
                            term = Mod(term * val, mod);
                        }
                        index = IntToBinary(circuit[layer][i].left.index, b.Length);
                        for (int j = 0; j < index.Length; j++)
                        {
                            long val = (index[j] == 1) ? b[j] : (1 - b[j]);
                            term = Mod(term * val, mod);
                        }
                        index = IntToBinary(circuit[layer][i].right.index, c.Length);
                        for (int j = 0; j < index.Length; j++)
                        {
                            long val = (index[j] == 1) ? c[j] : (1 - c[j]);
                            term = Mod(term * val, mod);
                        }
                        s = Mod(s + term, mod);
                    }
                }
                return (int)s;
            };
        }

        private static Func<int[], int[], int[], int> MulPoly(int layer, Node[][] circuit, int mod)
        {
            return (int[] a, int[] b, int[] c) =>
            {
                long s = 0;
                for (int i = 0; i < circuit[layer].Length; i++)
                {
                    if (circuit[layer][i].sign == 1)
                    {
                        long term = 1;
                        int[] index = IntToBinary(i, a.Length);
                        for (int j = 0; j < index.Length; j++)
                        {
                            long val = (index[j] == 1) ? a[j] : (1 - a[j]);
                            term = Mod(term * val, mod);
                        }
                        index = IntToBinary(circuit[layer][i].left.index, b.Length);
                        for (int j = 0; j < index.Length; j++)
                        {
                            long val = (index[j] == 1) ? b[j] : (1 - b[j]);
                            term = Mod(term * val, mod);
                        }
                        index = IntToBinary(circuit[layer][i].right.index, c.Length);
                        for (int j = 0; j < index.Length; j++)
                        {
                            long val = (index[j] == 1) ? c[j] : (1 - c[j]);
                            term = Mod(term * val, mod);
                        }
                        s = Mod(s + term, mod);
                    }
                }
                return (int)s;
            };
        }

        class Node
        {
            public int? value;
            public int sign;
            public Node left;
            public Node right;
            public int index;

            public Node(int index) { this.index = index; }
            public Node(int value, int index) { this.index = index; this.value = value; }
            public void set_sign(int sign) => this.sign = sign;
            public void set_left(Node left) => this.left = left;
            public void set_right(Node right) => this.right = right;
            public void calculate_value()
            {
                if (left == null && right == null) return;
                if (left.value == null) left.calculate_value();
                if (right.value == null) right.calculate_value();
                if (sign == 0) value = left.value + right.value;
                else if (sign == 1) value = left.value * right.value;
            }
        }

        class Prover
        {
            private Func<int[], int[], int[], int>[] funs;
            private Func<int[], int>[] Vs;
            private Func<int[], int>[] maskedPolys;
            private int layer;
            private int[] gateNum;
            private int[] bitsLen;
            private int mod;
            private Node[][] circuit;
            private Random rand;

            public Prover(int layer, int[] gateNum, int[] bitsLen, int mod, Node[][] circuit)
            {
                this.layer = layer; this.gateNum = gateNum; this.bitsLen = bitsLen; this.mod = mod; this.circuit = circuit;
                rand = new Random();
                Vs = new Func<int[], int>[layer];
                funs = new Func<int[], int[], int[], int>[layer - 1];
                maskedPolys = new Func<int[], int>[layer - 1];
                for (int i = 0; i <= layer - 1; i++) Vs[i] = make_V(i);
                for (int i = 0; i < layer - 1; i++) funs[i] = make_f(i);
                for (int i = 0; i < layer - 1; i++) maskedPolys[i] = make_H(bitsLen[i] + bitsLen[i + 1] + bitsLen[i + 1]);
            }
            public int W(int now_lawer, int index) => (int)circuit[now_lawer][index].value;

            public Func<int[], int> make_V(int layer)
            {
                return (int[] z) =>
                {
                    long s = 0;
                    for (int i = 0; i < gateNum[layer]; i++)
                    {
                        long term = W(layer, i);
                        int[] indexBits = IntToBinary(i, bitsLen[layer]);
                        for (int j = 0; j < z.Length; j++)
                        {
                            long val = (indexBits[j] == 1) ? z[j] : (1 - z[j]);
                            term = Mod(term * val, mod);
                        }
                        s = Mod(s + term, mod);
                    }
                    return (int)s;
                };
            }

            public Func<int[], int[], int[], int> make_f(int layer)
            {
                return (int[] a, int[] b, int[] c) =>
                {
                    long s = 0;
                    var nextLayerV = Vs[layer + 1];

                    long addPart = AddPoly(layer, circuit, mod)(a, b, c);
                    long vSum = Mod((long)nextLayerV(b) + nextLayerV(c), mod);
                    s = Mod(s + (addPart * vSum), mod);

                    long mulPart = MulPoly(layer, circuit, mod)(a, b, c);
                    long vProd = Mod((long)nextLayerV(b) * nextLayerV(c), mod);
                    s = Mod(s + (mulPart * vProd), mod);

                    return (int)s;
                };
            }
            public Func<int, int> make_G(int[] fixed_var, int nowLayer, int rho)
            {
                return (int z) =>
                {
                    long s = 0; long val1 = 0; long val2 = 0;
                    var g = funs[nowLayer];
                    int[] parameter = new int[bitsLen[nowLayer] + bitsLen[nowLayer + 1] + bitsLen[nowLayer + 1]];
                    for (int i = 0; i < fixed_var.Length; i++) parameter[i] = fixed_var[i];
                    parameter[fixed_var.Length] = z;
                    for (int i = 0; i < Math.Pow(2, parameter.Length - fixed_var.Length - 1); i++)
                    {
                        int[] restBits = IntToBinary(i, parameter.Length - fixed_var.Length - 1);
                        for (int j = 0; j < restBits.Length; j++) parameter[fixed_var.Length + 1 + j] = restBits[j];
                        val1 += g(parameter.Take(bitsLen[nowLayer]).ToArray(), parameter.Skip(bitsLen[nowLayer]).Take(bitsLen[nowLayer + 1]).ToArray(), parameter.Skip(bitsLen[nowLayer] + bitsLen[nowLayer + 1]).Take(bitsLen[nowLayer + 1]).ToArray());
                        val1 = Mod(val1, mod);
                        val2 += maskedPolys[nowLayer](parameter);
                        val2 = Mod(val2, mod);
                    }
                    s = Mod(val1 + Mod(rho * val2, mod), mod);
                    return (int)s;
                };
            }
            public Func<int, int[]> make_l(int layer, int[] fixed_var)
            {
                return (int z) =>
                {
                    int[] b = fixed_var.Skip(bitsLen[layer]).Take(bitsLen[layer + 1]).ToArray();
                    int[] c = fixed_var.Skip(bitsLen[layer] + bitsLen[layer + 1]).Take(bitsLen[layer + 1]).ToArray();
                    int[] l = new int[bitsLen[layer + 1]];
                    for (int i = 0; i < l.Length; i++) l[i] = Mod((long)b[i] * (1 - z) + (long)c[i] * z, mod);
                    return l;
                };
            }
            public Func<int, int> make_q(int layer, int[] fixed_var)
            {
                return (int z) => Vs[layer + 1](make_l(layer, fixed_var)(z));
            }
            public Func<int[], int> claimed_D() => (int[] z) => Vs[0](z);
            public int maskSum(int layer, int[] fixed_var)
            {
                long s = 0;
                int[] parameter = new int[bitsLen[layer] + bitsLen[layer + 1] + bitsLen[layer + 1]];
                for (int i = 0; i < fixed_var.Length; i++) parameter[i] = fixed_var[i];
                if (parameter.Length == fixed_var.Length) return maskedPolys[layer](parameter);
                for (int i = 0; i < Math.Pow(2, parameter.Length - fixed_var.Length); i++)
                {
                    int[] restBits = IntToBinary(i, parameter.Length - fixed_var.Length);
                    for (int j = 0; j < restBits.Length; j++) parameter[fixed_var.Length + j] = restBits[j];
                    s = Mod(s + maskedPolys[layer](parameter), mod);
                }
                return (int)s;
            }
            public int pickRandom() => rand.Next(mod);
            public Func<int[], int> make_H(int numVars)
            {
                long constantTerm = pickRandom();
                long[] coeffA = new long[numVars]; long[] coeffB = new long[numVars];
                for (int i = 0; i < numVars; i++) { coeffA[i] = pickRandom(); coeffB[i] = pickRandom(); }
                return (int[] parameter) =>
                {
                    long s = constantTerm;
                    for (int i = 0; i < parameter.Length; i++)
                    {
                        long val = Mod((long)parameter[i] * parameter[i], mod);
                        val = Mod(val * coeffB[i], mod);
                        s = Mod(s + val, mod);
                        val = Mod((long)parameter[i] * coeffA[i], mod);
                        s = Mod(s + val, mod);
                    }
                    return (int)s;
                };
            }
        }

        class Verifier
        {
            private Random rand;
            private int mod;
            public Verifier(int mod) { this.mod = mod; rand = new Random(); }
            public int pickRandom() => rand.Next(mod);
            public Func<int[], int> make_input(Node[] input, int bitslen)
            {
                return (int[] z) =>
                {
                    long s = 0;
                    for (int i = 0; i < input.Length; i++)
                    {
                        long term = (int)input[i].value;
                        int[] indexBits = IntToBinary(i, bitslen);
                        for (int j = 0; j < z.Length; j++)
                        {
                            long val = (indexBits[j] == 1) ? z[j] : (1 - z[j]);
                            term = Mod(term * val, mod);
                        }
                        s = Mod(s + term, mod);
                    }
                    return (int)s;
                };
            }
        }
    }
}