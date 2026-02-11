using System;
using System.Collections.Generic;
using System.Linq;

namespace GKR_Backend.Services
{
    public class GkrService
    {
        // 用來儲存輸出紀錄
        private List<string> _logs;

        public List<string> RunGkr(GkrRequest request)
        {
            _logs = new List<string>();
            try
            {
                RunProtocolInternal(request);
            }
            catch (Exception ex)
            {
                _logs.Add($"Error: {ex.Message}");
                _logs.Add($"StackTrace: {ex.StackTrace}");
            }
            return _logs;
        }

        // 輔助 Log 函式，取代 Console.WriteLine
        private void Log(string msg) => _logs.Add(msg);
        private void LogWrite(string msg)
        {
            if (_logs.Count > 0)
                _logs[_logs.Count - 1] += msg;
            else
                _logs.Add(msg);
        }

        private void RunProtocolInternal(GkrRequest req)
        {
            int mod = req.Mod;
            int[][] rawCircuit = req.Circuit;
            int[] inputs = req.Inputs;

            // 1. 建構電路結構
            // 注意：前端傳來的 rawCircuit 通常不包含 Input Layer，我們需要手動加上去
            // rawCircuit[0] 是最上層 (Output)，rawCircuit[最後] 是接到 Input 的層

            int totalLayers = rawCircuit.Length + 1; // +1 是因為還有 Input Layer
            Node[][] circuit = new Node[totalLayers][];

            Log($"Setting up Circuit with Mod: {mod}, Layers: {totalLayers}");

            // A. 初始化每一層的 Nodes
            for (int i = 0; i < totalLayers; i++)
            {
                if (i == totalLayers - 1) // Input Layer
                {
                    circuit[i] = new Node[inputs.Length];
                    for (int j = 0; j < inputs.Length; j++)
                    {
                        circuit[i][j] = new Node(inputs[j], j);
                    }
                    Log($"Layer {i} (Input): {inputs.Length} gates initialized with values.");
                }
                else // Compute Layers (從前端傳來的)
                {
                    int[] layerConfig = rawCircuit[i];
                    circuit[i] = new Node[layerConfig.Length];
                    for (int j = 0; j < layerConfig.Length; j++)
                    {
                        circuit[i][j] = new Node(j);
                        circuit[i][j].set_sign(layerConfig[j]); // 0:+, 1:*
                    }
                    Log($"Layer {i}: {layerConfig.Length} gates initialized.");
                }
            }

            // B. 自動連接電路 (Binary Tree Structure)
            // 假設 Layer i 的第 j 個 Gate，連接到 Layer i+1 的 2*j (Left) 和 2*j+1 (Right)
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
                        Log($"Warning: Layer {i} Gate {j} cannot connect to children (Out of bounds). Check input size.");
                    }
                }
            }

            // C. 計算電路數值
            int[] gateNum = new int[totalLayers];
            int[] bitsLen = new int[totalLayers];

            for (int i = 0; i < totalLayers; i++)
            {
                gateNum[i] = circuit[i].Length;
                bitsLen[i] = (gateNum[i] == 1) ? 1 : (int)Math.Ceiling(Math.Log(gateNum[i], 2));
            }

            for (int i = totalLayers - 1; i >= 0; i--)
            {
                for (int j = 0; j < circuit[i].Length; j++)
                {
                    circuit[i][j].calculate_value();
                }
            }

            // 2. 執行 GKR 流程 (邏輯直接從原 Program.cs 搬移)
            Prover prover = new Prover(totalLayers, gateNum, bitsLen, mod, circuit);
            Verifier verifier = new Verifier(mod);

            int[] fixed_var = new int[bitsLen[0]];
            var claimed_D = prover.claimed_D();

            string outputVals = "";
            for (int i = 0; i < gateNum[0]; i++) outputVals += claimed_D(IntToBinary(i, bitsLen[0])) + " ";
            Log($"P: send D() and the circuit outputs: {outputVals}");

            for (int i = 0; i < fixed_var.Length; i++) fixed_var[i] = verifier.pickRandom();
            Log($"V: send fixed_var = " + string.Join(", ", fixed_var));

            int claimed = claimed_D(fixed_var);
            Log($"P: claimed D(fixed_var) = {claimed}");

            for (int now_layer = 0; now_layer < totalLayers - 1; now_layer++)
            {
                int maskSum = prover.maskSum(now_layer, fixed_var);
                Log($"P: send maskSum = {maskSum}");
                int rho = verifier.pickRandom();
                Log($"V: send rho = {rho}");

                claimed = Mod(claimed + Mod(rho * maskSum, mod), mod);
                Log(" sum check start ");

                for (int i = 0; i < bitsLen[now_layer + 1] * 2; i++)
                {
                    var G = prover.make_G(fixed_var, now_layer, rho);
                    // Log($"P: send G{i}"); // Optional detail
                    // Log($"V: Verifying G{i}(0) + G{i}(1) = claimed");

                    int term = Mod((long)G(0) + (long)G(1), mod);
                    if (term != claimed)
                    {
                        Log("V: sum check failed");
                        return;
                    }

                    int s = verifier.pickRandom();
                    Log($"V: send s{i} = {s}");
                    fixed_var = fixed_var.Append(s).ToArray();
                    claimed = G(s);
                    Log($"P: claimed G{i}(s{i}) = {claimed}");

                    // 最後一次 sumcheck 的最後一輪
                    if (now_layer == totalLayers - 2 && i == bitsLen[now_layer + 1] * 2 - 1)
                    {
                        Log("V: construct input layer poly");
                        var input_poly = verifier.make_input(circuit[totalLayers - 1], bitsLen[totalLayers - 1]);
                        maskSum = prover.maskSum(now_layer, fixed_var);
                        Log($"P: send maskSum with fixed_var = {maskSum}");
                        claimed = Mod(claimed - Mod(rho * maskSum, mod), mod);
                        Log("V: claimed - rho * maskSum");

                        Log("V: sum check final check ");

                        int[] a = fixed_var.Take(bitsLen[totalLayers - 2]).ToArray();
                        int[] b = fixed_var.Skip(bitsLen[totalLayers - 2]).Take(bitsLen[totalLayers - 1]).ToArray();
                        int[] c = fixed_var.Skip(bitsLen[totalLayers - 2] + bitsLen[totalLayers - 1]).Take(bitsLen[totalLayers - 1]).ToArray();

                        long final_addPolyVal = AddPoly(totalLayers - 2, circuit, mod)(a, b, c);
                        long final_mulPolyVal = MulPoly(totalLayers - 2, circuit, mod)(a, b, c);
                        long final_part1 = Mod(final_addPolyVal * Mod(input_poly(b) + input_poly(c), mod), mod);
                        long final_part2 = Mod(final_mulPolyVal * Mod(input_poly(b) * input_poly(c), mod), mod);
                        term = Mod(final_part1 + final_part2, mod);

                        if (claimed != term) { Log($"V: final check failed (Claimed {claimed} != Term {term})"); return; }
                        Log(" sum check passed ");
                        Log(" Verifier can trust D() ");
                        break;
                    }

                    if (i == bitsLen[now_layer + 1] * 2 - 1) // Sumcheck 最後一輪
                    {
                        var claimed_poly = prover.make_q(now_layer, fixed_var);
                        Log($"P: send claimed_poly q{now_layer + 1}");
                        maskSum = prover.maskSum(now_layer, fixed_var);
                        Log($"P: send maskSum = {maskSum}");
                        claimed = Mod(claimed - Mod(rho * maskSum, mod), mod);

                        // Final Check logic
                        int[] a = fixed_var.Take(bitsLen[now_layer]).ToArray();
                        int[] b = fixed_var.Skip(bitsLen[now_layer]).Take(bitsLen[now_layer + 1]).ToArray();
                        int[] c = fixed_var.Skip(bitsLen[now_layer] + bitsLen[now_layer + 1]).Take(bitsLen[now_layer + 1]).ToArray();

                        long addPolyVal = AddPoly(now_layer, circuit, mod)(a, b, c);
                        long mulPolyVal = MulPoly(now_layer, circuit, mod)(a, b, c);
                        long part1 = Mod(addPolyVal * Mod(claimed_poly(0) + claimed_poly(1), mod), mod);
                        long part2 = Mod(mulPolyVal * Mod(claimed_poly(0) * claimed_poly(1), mod), mod);
                        term = Mod(part1 + part2, mod);

                        if (claimed != term) { Log("V: intermediate check failed"); return; }

                        Log(" sum check passed ");
                        int random_var = verifier.pickRandom();
                        Log($"V: send r{now_layer + 1} = {random_var}");
                        claimed = claimed_poly(random_var);

                        var l_poly = prover.make_l(now_layer, fixed_var);
                        Array.Resize(ref fixed_var, bitsLen[now_layer + 1]);
                        for (int j = 0; j < fixed_var.Length; j++)
                        {
                            fixed_var[j] = l_poly(random_var)[j];
                        }
                    }
                }
            }
        }

        // ==========================================
        // 以下為複製自 Program.cs 的靜態方法與類別
        // 稍微調整修飾詞以符合 Class 結構
        // ==========================================

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

        // 內部類別 (需設為 public 或不用調整，因為都在 Service 內)
        class Node
        {
            public int? value;
            public int sign; // 0: + , 1: -
            public Node left;
            public Node right;
            public int index;

            public Node(int index)
            {
                this.index = index;
                this.value = null;
                this.left = null;
                this.right = null;
            }
            public Node(int value, int index)
            {
                this.index = index;
                this.value = value;
                this.left = null;
                this.right = null;
            }
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
                this.layer = layer;
                this.gateNum = gateNum;
                this.bitsLen = bitsLen;
                this.mod = mod;
                this.circuit = circuit;
                rand = new Random();
                Vs = new Func<int[], int>[layer];
                funs = new Func<int[], int[], int[], int>[layer - 1];
                maskedPolys = new Func<int[], int>[layer - 1];
                for (int i = 0; i <= layer - 1; i++) Vs[i] = make_V(i);
                for (int i = 0; i < layer - 1; i++) funs[i] = make_f(i);
                for (int i = 0; i < layer - 1; i++) maskedPolys[i] = make_H(bitsLen[i] + bitsLen[i + 1] + bitsLen[i + 1]);
            }
            public int W(int now_lawer, int index) => (int)circuit[now_lawer][index].value;

            public Func<int[], int> make_V(int layer)                           //創建層多項式
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
                    long s = 0;
                    long val1 = 0; long val2 = 0;
                    var g = funs[nowLayer];
                    int[] parameter = new int[bitsLen[nowLayer] + bitsLen[nowLayer + 1] + bitsLen[nowLayer + 1]];
                    for (int i = 0; i < fixed_var.Length; i++) parameter[i] = fixed_var[i];
                    parameter[fixed_var.Length] = z;
                    for (int i = 0; i < Math.Pow(2, parameter.Length - fixed_var.Length - 1); i++)
                    {
                        int[] restBits = IntToBinary(i, parameter.Length - fixed_var.Length - 1);
                        for (int j = 0; j < restBits.Length; j++)
                        {
                            parameter[fixed_var.Length + 1 + j] = restBits[j];
                        }
                        val1 += g(
                            parameter.Take(bitsLen[nowLayer]).ToArray(),
                            parameter.Skip(bitsLen[nowLayer]).Take(bitsLen[nowLayer + 1]).ToArray(),
                            parameter.Skip(bitsLen[nowLayer] + bitsLen[nowLayer + 1]).Take(bitsLen[nowLayer + 1]).ToArray()
                            );
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
                    for (int i = 0; i < l.Length; i++)
                    {
                        long val = (long)b[i] * (1 - z) + (long)c[i] * z;
                        l[i] = Mod(val, mod);
                    }
                    return l;
                };
            }
            public Func<int, int> make_q(int layer, int[] fixed_var)
            {
                return (int z) =>
                {
                    int s = 0;
                    var l = make_l(layer, fixed_var);
                    var V_next = Vs[layer + 1];
                    s = V_next(l(z));
                    return s;
                };
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
                    for (int j = 0; j < restBits.Length; j++)
                        parameter[fixed_var.Length + j] = restBits[j];
                    s = Mod(s + maskedPolys[layer](parameter), mod);
                }
                return (int)s;
            }
            public int pickRandom() => rand.Next(mod);
            public Func<int[], int> make_H(int numVars)
            {
                long constantTerm = pickRandom();
                long[] coeffA = new long[numVars];
                long[] coeffB = new long[numVars];
                for (int i = 0; i < numVars; i++)
                {
                    coeffA[i] = pickRandom();
                    coeffB[i] = pickRandom();
                }
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
            public Verifier(int mod)
            {
                this.mod = mod;
                rand = new Random();
            }
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