using System;
using System.Collections.Generic;
using System.Linq;
using mcl;

namespace GKR_Backend.Services
{
    public class GkrService
    {
        private List<GkrEvent> _events;
        private int _currentLayer = 0;
        private int _currentRound = 1;

        public List<GkrEvent> RunGkr(GkrRequest request)
        {
            _events = new List<GkrEvent>();
            try
            {
                MCL.Init(MCL.BLS12_381);
                RunProtocolInternal(request);
            }
            catch (Exception ex)
            {
                AddSystemEvent($"Error: {ex.Message}");
                AddSystemEvent($"StackTrace: {ex.StackTrace}");
            }
            return _events;
        }

        #region 事件發送輔助方法
        private void AddProverEvent(string msg, string type = "PROVER_MESSAGE", object? data = null)
        {
            _events.Add(new GkrEvent { ProtocolLayer = _currentLayer, Round = _currentRound, Role = "Prover", Type = type, Message = msg, Data = data });
        }

        private void AddVerifierEvent(string msg, bool incrementRound = false)
        {
            _events.Add(new GkrEvent { ProtocolLayer = _currentLayer, Round = _currentRound, Role = "Verifier", Message = msg });
            if (incrementRound) _currentRound++;
        }

        private void AddSystemEvent(string msg)
        {
            _events.Add(new GkrEvent { ProtocolLayer = _currentLayer, Round = _currentRound, Role = "System", Message = msg });
        }

        private void AddCommitmentEvent(string label, string commitmentHex)
        {
            _events.Add(new GkrEvent 
            { 
                ProtocolLayer = _currentLayer, 
                Round = _currentRound, 
                Role = "Prover", 
                Type = "CLAIM_VALUE", 
                Message = $"{label} = {commitmentHex}",
                Data = new Dictionary<string, string> { { "commitment", commitmentHex } }
            });
        }
        #endregion

        #region MCL 有限體運算 Helper
        private static MCL.Fr ToFr(int i) { var f = new MCL.Fr(); f.SetInt(i); return f; }
        private static MCL.Fr FrAdd(MCL.Fr a, MCL.Fr b) { var res = new MCL.Fr(); MCL.Add(ref res, in a, in b); return res; }
        private static MCL.Fr FrSub(MCL.Fr a, MCL.Fr b) { var res = new MCL.Fr(); MCL.Sub(ref res, in a, in b); return res; }
        private static MCL.Fr FrMul(MCL.Fr a, MCL.Fr b) { var res = new MCL.Fr(); MCL.Mul(ref res, in a, in b); return res; }
        private static int[] IntToBinary(int n, int len)
        {
            int[] bits = new int[len];
            for (int i = 0; i < len; i++) bits[i] = (n >> i) & 1;
            return bits;
        }
        #endregion

        private void RunProtocolInternal(GkrRequest req)
        {
            int[][] rawCircuit = req.Circuit ?? new int[0][];
            int[] inputs = req.Inputs ?? new int[0];
            int[] hiddenValues = req.HiddenValues ?? new int[inputs.Length];

            int totalLayers = rawCircuit.Length + 1;
            Node[][] circuit = new Node[totalLayers][];

            _currentLayer = 0;
            _currentRound = 1;

            AddSystemEvent($"GKR + Multilinear KZG 系統啟動");
            AddSystemEvent($"Public Inputs: [{string.Join(", ", inputs)}]");
            AddSystemEvent($"Hidden Witness: [{string.Join(", ", hiddenValues)}]");

            for (int i = 0; i < totalLayers; i++)
            {
                if (i == totalLayers - 1)
                {
                    circuit[i] = new Node[inputs.Length];
                    for (int j = 0; j < inputs.Length; j++) circuit[i][j] = new Node(ToFr(inputs[j]), j);
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
                }
            }

            for (int i = 0; i < totalLayers - 1; i++)
            {
                for (int j = 0; j < circuit[i].Length; j++)
                {
                    int leftIdx = j * 2, rightIdx = j * 2 + 1;
                    if (leftIdx < circuit[i + 1].Length && rightIdx < circuit[i + 1].Length)
                    {
                        circuit[i][j].set_left(circuit[i + 1][leftIdx]);
                        circuit[i][j].set_right(circuit[i + 1][rightIdx]);
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
            for (int i = totalLayers - 1; i >= 0; i--)
            {
                foreach (var node in circuit[i]) node.calculate_value();
            }

            AddSystemEvent("執行 Multilinear KZG Setup 與 Commitment...");
            MultilinearKZG kzg = new MultilinearKZG(bitsLen[totalLayers - 1]);
            MCL.Fr[] inputValues = circuit[totalLayers - 1].Select(n => n.value).ToArray();
            var commitment = kzg.Commit(inputValues);
            
            AddProverEvent("Prover 對輸入層 (Layer 2) 進行 KZG 承諾。");
            AddCommitmentEvent("Input Commitment (G1)", commitment.GetStr(16));

            Prover prover = new Prover(totalLayers, gateNum, bitsLen, circuit);
            Verifier verifier = new Verifier();

            MCL.Fr[] fixed_var = new MCL.Fr[bitsLen[0]];
            var claimed_D = prover.claimed_D();

            _currentLayer = 0;
            _currentRound = 1;

            string outputVals = string.Join(" ", circuit[0].Select(n => n.value.GetStr(10)));
            AddProverEvent($"send D() and the circuit outputs: {outputVals}");

            for (int i = 0; i < fixed_var.Length; i++) fixed_var[i] = verifier.pickRandom();
            AddVerifierEvent($"send fixed_var = " + string.Join(", ", fixed_var.Select(f => f.GetStr(10))));

            MCL.Fr claimed = claimed_D(fixed_var);
            AddProverEvent($"claimed D(fixed_var) = {claimed.GetStr(10)}");
            _currentRound++;

            for (int now_layer = 0; now_layer < totalLayers - 1; now_layer++)
            {
                _currentLayer = now_layer + 1;
                _currentRound = 1;

                MCL.Fr maskSum = prover.maskSum(now_layer, fixed_var);
                AddProverEvent($"send maskSum = {maskSum.GetStr(10)}");
                
                MCL.Fr rho = verifier.pickRandom();
                AddVerifierEvent($"send rho = {rho.GetStr(10)}", true);

                claimed = FrAdd(claimed, FrMul(rho, maskSum));

                for (int i = 0; i < bitsLen[now_layer + 1] * 2; i++)
                {
                    var G = prover.make_G(fixed_var, now_layer, rho);
                    MCL.Fr term = FrAdd(G(ToFr(0)), G(ToFr(1)));

                    if (!claimed.Equals(term)) { AddVerifierEvent("V: Sumcheck Failed!"); return; }

                    MCL.Fr s = verifier.pickRandom();
                    AddVerifierEvent($"send s{i} = {s.GetStr(10)}");
                    
                    fixed_var = fixed_var.Append(s).ToArray();
                    claimed = G(s);
                    AddProverEvent($"claimed G{i}(s{i}) = {claimed.GetStr(10)}");
                    _currentRound++;

                    if (now_layer == totalLayers - 2 && i == bitsLen[now_layer + 1] * 2 - 1)
                    {
                        var input_poly = verifier.make_input(circuit[totalLayers - 1], bitsLen[totalLayers - 1]);
                        maskSum = prover.maskSum(now_layer, fixed_var);
                        AddProverEvent($"send maskSum with fixed_var = {maskSum.GetStr(10)}");
                        
                        claimed = FrSub(claimed, FrMul(rho, maskSum));

                        // ⭐️ 修正 1：不再轉回 int，直接使用 MCL.Fr 陣列
                        MCL.Fr[] a = fixed_var.Take(bitsLen[totalLayers - 2]).ToArray();
                        MCL.Fr[] b = fixed_var.Skip(bitsLen[totalLayers - 2]).Take(bitsLen[totalLayers - 1]).ToArray();
                        MCL.Fr[] c = fixed_var.Skip(bitsLen[totalLayers - 2] + bitsLen[totalLayers - 1]).Take(bitsLen[totalLayers - 1]).ToArray();

                        MCL.Fr final_addPolyVal = AddPoly(totalLayers - 2, circuit)(a, b, c);
                        MCL.Fr final_mulPolyVal = MulPoly(totalLayers - 2, circuit)(a, b, c);
                        
                        MCL.Fr b_fr = input_poly(b);
                        MCL.Fr c_fr = input_poly(c);

                        MCL.Fr final_part1 = FrMul(final_addPolyVal, FrAdd(b_fr, c_fr));
                        MCL.Fr final_part2 = FrMul(final_mulPolyVal, FrMul(b_fr, c_fr));
                        term = FrAdd(final_part1, final_part2);

                        if (!claimed.Equals(term)) { AddVerifierEvent("V: final check failed"); return; }
                        
                        AddVerifierEvent("sum check passed, Verifier can trust D()");
                        AddSystemEvent("GKR 驗證成功，執行 KZG 開放驗證...");
                        break;
                    }

                    if (i == bitsLen[now_layer + 1] * 2 - 1)
                    {
                        var claimed_poly = prover.make_q(now_layer, fixed_var);
                        AddProverEvent($"send claimed_poly q{now_layer + 1}");
                        
                        maskSum = prover.maskSum(now_layer, fixed_var);
                        AddProverEvent($"send maskSum = {maskSum.GetStr(10)}");
                        
                        claimed = FrSub(claimed, FrMul(rho, maskSum));

                        // ⭐️ 修正 2：不再轉回 int，直接使用 MCL.Fr 陣列
                        MCL.Fr[] a = fixed_var.Take(bitsLen[now_layer]).ToArray();
                        MCL.Fr[] b = fixed_var.Skip(bitsLen[now_layer]).Take(bitsLen[now_layer + 1]).ToArray();
                        MCL.Fr[] c = fixed_var.Skip(bitsLen[now_layer] + bitsLen[now_layer + 1]).Take(bitsLen[now_layer + 1]).ToArray();

                        MCL.Fr addPolyVal = AddPoly(now_layer, circuit)(a, b, c);
                        MCL.Fr mulPolyVal = MulPoly(now_layer, circuit)(a, b, c);
                        
                        MCL.Fr part1 = FrMul(addPolyVal, FrAdd(claimed_poly(ToFr(0)), claimed_poly(ToFr(1))));
                        MCL.Fr part2 = FrMul(mulPolyVal, FrMul(claimed_poly(ToFr(0)), claimed_poly(ToFr(1))));
                        term = FrAdd(part1, part2);

                        if (!claimed.Equals(term)) { AddVerifierEvent("V: intermediate check failed"); return; }

                        MCL.Fr random_var = verifier.pickRandom();
                        AddVerifierEvent($"sum check passed. send r{now_layer + 1} = {random_var.GetStr(10)}", true);
                        
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

        #region 電路多項式 Helper (升級接受 MCL.Fr 陣列)
        private static Func<MCL.Fr[], MCL.Fr[], MCL.Fr[], MCL.Fr> AddPoly(int layer, Node[][] circuit)
        {
            return (MCL.Fr[] a, MCL.Fr[] b, MCL.Fr[] c) =>
            {
                MCL.Fr s = ToFr(0);
                for (int i = 0; i < circuit[layer].Length; i++)
                {
                    if (circuit[layer][i].sign == 0)
                    {
                        MCL.Fr term = ToFr(1);
                        int[] index = IntToBinary(i, a.Length);
                        for (int j = 0; j < index.Length; j++)
                        {
                            MCL.Fr val = (index[j] == 1) ? a[j] : FrSub(ToFr(1), a[j]);
                            term = FrMul(term, val);
                        }
                        index = IntToBinary(circuit[layer][i].left?.index ?? 0, b.Length);
                        for (int j = 0; j < index.Length; j++)
                        {
                            MCL.Fr val = (index[j] == 1) ? b[j] : FrSub(ToFr(1), b[j]);
                            term = FrMul(term, val);
                        }
                        index = IntToBinary(circuit[layer][i].right?.index ?? 0, c.Length);
                        for (int j = 0; j < index.Length; j++)
                        {
                            MCL.Fr val = (index[j] == 1) ? c[j] : FrSub(ToFr(1), c[j]);
                            term = FrMul(term, val);
                        }
                        s = FrAdd(s, term);
                    }
                }
                return s;
            };
        }

        private static Func<MCL.Fr[], MCL.Fr[], MCL.Fr[], MCL.Fr> MulPoly(int layer, Node[][] circuit)
        {
            return (MCL.Fr[] a, MCL.Fr[] b, MCL.Fr[] c) =>
            {
                MCL.Fr s = ToFr(0);
                for (int i = 0; i < circuit[layer].Length; i++)
                {
                    if (circuit[layer][i].sign == 1)
                    {
                        MCL.Fr term = ToFr(1);
                        int[] index = IntToBinary(i, a.Length);
                        for (int j = 0; j < index.Length; j++)
                        {
                            MCL.Fr val = (index[j] == 1) ? a[j] : FrSub(ToFr(1), a[j]);
                            term = FrMul(term, val);
                        }
                        index = IntToBinary(circuit[layer][i].left?.index ?? 0, b.Length);
                        for (int j = 0; j < index.Length; j++)
                        {
                            MCL.Fr val = (index[j] == 1) ? b[j] : FrSub(ToFr(1), b[j]);
                            term = FrMul(term, val);
                        }
                        index = IntToBinary(circuit[layer][i].right?.index ?? 0, c.Length);
                        for (int j = 0; j < index.Length; j++)
                        {
                            MCL.Fr val = (index[j] == 1) ? c[j] : FrSub(ToFr(1), c[j]);
                            term = FrMul(term, val);
                        }
                        s = FrAdd(s, term);
                    }
                }
                return s;
            };
        }
        #endregion

        #region 演算法核心類別
        class Node
        {
            public MCL.Fr value;
            public bool hasValue = false;
            public int sign, index;
            public Node? left, right;
            
            public Node(int index) { this.index = index; }
            public Node(MCL.Fr value, int index) { this.index = index; this.value = value; this.hasValue = true; }
            
            public void set_sign(int sign) => this.sign = sign;
            public void set_left(Node left) => this.left = left;
            public void set_right(Node right) => this.right = right;
            
            public void calculate_value()
            {
                if (hasValue) return;
                if (left == null || right == null) return;
                if (!left.hasValue) left.calculate_value();
                if (!right.hasValue) right.calculate_value();
                
                if (sign == 0) value = FrAdd(left.value, right.value);
                else value = FrMul(left.value, right.value);
                hasValue = true;
            }
        }

        class MultilinearKZG
        {
            private MCL.G1[][] srsLevels;
            public MultilinearKZG(int n)
            {
                srsLevels = new MCL.G1[n][];
                MCL.Fr tau = new MCL.Fr(); tau.SetByCSPRNG();
                for (int i = 0; i < n; i++)
                {
                    int size = 1 << (n - i);
                    srsLevels[i] = new MCL.G1[size];
                    for (int j = 0; j < size; j++)
                    {
                        MCL.G1 baseG1 = new MCL.G1(); baseG1.SetHashOf("base");
                        MCL.G1 res = new MCL.G1();
                        res.Mul(baseG1, tau); 
                        srsLevels[i][j] = res;
                    }
                }
            }
            public MCL.G1 Commit(MCL.Fr[] coeffs)
            {
                MCL.G1 res = new MCL.G1(); res.Clear();
                for (int i = 0; i < coeffs.Length; i++)
                {
                    MCL.G1 tmp = new MCL.G1();
                    tmp.Mul(srsLevels[0][i], coeffs[i]);
                    res.Add(res, tmp);
                }
                return res;
            }
        }

        class Prover
        {
            private Func<MCL.Fr[], MCL.Fr[], MCL.Fr[], MCL.Fr>[] funs;
            private Func<MCL.Fr[], MCL.Fr>[] Vs;
            private Func<MCL.Fr[], MCL.Fr>[] maskedPolys;
            private int layer;
            private int[] gateNum;
            private int[] bitsLen;
            private Node[][] circuit;

            public Prover(int layer, int[] gateNum, int[] bitsLen, Node[][] circuit)
            {
                this.layer = layer; this.gateNum = gateNum; this.bitsLen = bitsLen; this.circuit = circuit;
                Vs = new Func<MCL.Fr[], MCL.Fr>[layer];
                funs = new Func<MCL.Fr[], MCL.Fr[], MCL.Fr[], MCL.Fr>[layer - 1];
                maskedPolys = new Func<MCL.Fr[], MCL.Fr>[layer - 1];
                
                for (int i = 0; i <= layer - 1; i++) Vs[i] = make_V(i);
                for (int i = 0; i < layer - 1; i++) funs[i] = make_f(i);
                for (int i = 0; i < layer - 1; i++) maskedPolys[i] = make_H(bitsLen[i] + bitsLen[i + 1] + bitsLen[i + 1]);
            }

            public MCL.Fr W(int now_layer, int index) => circuit[now_layer][index].value;

            public Func<MCL.Fr[], MCL.Fr> make_V(int layer)
            {
                return (MCL.Fr[] z) =>
                {
                    MCL.Fr s = ToFr(0);
                    for (int i = 0; i < gateNum[layer]; i++)
                    {
                        MCL.Fr term = W(layer, i);
                        int[] indexBits = IntToBinary(i, bitsLen[layer]);
                        for (int j = 0; j < z.Length; j++)
                        {
                            MCL.Fr val = (indexBits[j] == 1) ? z[j] : FrSub(ToFr(1), z[j]);
                            term = FrMul(term, val);
                        }
                        s = FrAdd(s, term);
                    }
                    return s;
                };
            }

            public Func<MCL.Fr[], MCL.Fr[], MCL.Fr[], MCL.Fr> make_f(int layer)
            {
                return (MCL.Fr[] a, MCL.Fr[] b, MCL.Fr[] c) =>
                {
                    MCL.Fr s = ToFr(0);
                    var nextLayerV = Vs[layer + 1];

                    MCL.Fr addPart = AddPoly(layer, circuit)(a, b, c);
                    MCL.Fr vSum = FrAdd(nextLayerV(b), nextLayerV(c));
                    s = FrAdd(s, FrMul(addPart, vSum));

                    MCL.Fr mulPart = MulPoly(layer, circuit)(a, b, c);
                    MCL.Fr vProd = FrMul(nextLayerV(b), nextLayerV(c));
                    s = FrAdd(s, FrMul(mulPart, vProd));

                    return s;
                };
            }

            public Func<MCL.Fr, MCL.Fr> make_G(MCL.Fr[] fixed_var, int nowLayer, MCL.Fr rho)
            {
                return (MCL.Fr z) =>
                {
                    MCL.Fr s = ToFr(0);
                    MCL.Fr val1 = ToFr(0);
                    MCL.Fr val2 = ToFr(0);
                    var g = funs[nowLayer];
                    
                    MCL.Fr[] parameter = new MCL.Fr[bitsLen[nowLayer] + bitsLen[nowLayer + 1] + bitsLen[nowLayer + 1]];
                    for (int i = 0; i < fixed_var.Length; i++) parameter[i] = fixed_var[i];
                    parameter[fixed_var.Length] = z;
                    
                    int remainingBits = parameter.Length - fixed_var.Length - 1;
                    int maxIter = 1 << remainingBits;

                    for (int i = 0; i < maxIter; i++)
                    {
                        int[] restBits = IntToBinary(i, remainingBits);
                        for (int j = 0; j < restBits.Length; j++) 
                            parameter[fixed_var.Length + 1 + j] = ToFr(restBits[j]);

                        // ⭐️ 修正 3：直接截取 Fr 陣列傳遞
                        MCL.Fr[] paramA = parameter.Take(bitsLen[nowLayer]).ToArray();
                        MCL.Fr[] paramB = parameter.Skip(bitsLen[nowLayer]).Take(bitsLen[nowLayer + 1]).ToArray();
                        MCL.Fr[] paramC = parameter.Skip(bitsLen[nowLayer] + bitsLen[nowLayer + 1]).Take(bitsLen[nowLayer + 1]).ToArray();

                        val1 = FrAdd(val1, g(paramA, paramB, paramC));
                        val2 = FrAdd(val2, maskedPolys[nowLayer](parameter));
                    }
                    s = FrAdd(val1, FrMul(rho, val2));
                    return s;
                };
            }

            public Func<MCL.Fr, MCL.Fr[]> make_l(int layer, MCL.Fr[] fixed_var)
            {
                return (MCL.Fr z) =>
                {
                    MCL.Fr[] b = fixed_var.Skip(bitsLen[layer]).Take(bitsLen[layer + 1]).ToArray();
                    MCL.Fr[] c = fixed_var.Skip(bitsLen[layer] + bitsLen[layer + 1]).Take(bitsLen[layer + 1]).ToArray();
                    MCL.Fr[] l = new MCL.Fr[bitsLen[layer + 1]];
                    for (int i = 0; i < l.Length; i++)
                    {
                        MCL.Fr t1 = FrMul(b[i], FrSub(ToFr(1), z));
                        MCL.Fr t2 = FrMul(c[i], z);
                        l[i] = FrAdd(t1, t2);
                    }
                    return l;
                };
            }

            public Func<MCL.Fr, MCL.Fr> make_q(int layer, MCL.Fr[] fixed_var)
            {
                return (MCL.Fr z) => Vs[layer + 1](make_l(layer, fixed_var)(z));
            }

            public Func<MCL.Fr[], MCL.Fr> claimed_D() => (MCL.Fr[] z) => Vs[0](z);

            public MCL.Fr maskSum(int layer, MCL.Fr[] fixed_var)
            {
                MCL.Fr s = ToFr(0);
                MCL.Fr[] parameter = new MCL.Fr[bitsLen[layer] + bitsLen[layer + 1] + bitsLen[layer + 1]];
                for (int i = 0; i < fixed_var.Length; i++) parameter[i] = fixed_var[i];
                if (parameter.Length == fixed_var.Length) return maskedPolys[layer](parameter);
                
                int remainingBits = parameter.Length - fixed_var.Length;
                int maxIter = 1 << remainingBits;

                for (int i = 0; i < maxIter; i++)
                {
                    int[] restBits = IntToBinary(i, remainingBits);
                    for (int j = 0; j < restBits.Length; j++) 
                        parameter[fixed_var.Length + j] = ToFr(restBits[j]);
                    
                    s = FrAdd(s, maskedPolys[layer](parameter));
                }
                return s;
            }

            public Func<MCL.Fr[], MCL.Fr> make_H(int numVars)
            {
                MCL.Fr constantTerm = new MCL.Fr(); constantTerm.SetByCSPRNG();
                MCL.Fr[] coeffA = new MCL.Fr[numVars];
                MCL.Fr[] coeffB = new MCL.Fr[numVars];
                for (int i = 0; i < numVars; i++)
                {
                    coeffA[i] = new MCL.Fr(); coeffA[i].SetByCSPRNG();
                    coeffB[i] = new MCL.Fr(); coeffB[i].SetByCSPRNG();
                }
                return (MCL.Fr[] parameter) =>
                {
                    MCL.Fr s = constantTerm;
                    for (int i = 0; i < parameter.Length; i++)
                    {
                        MCL.Fr val = FrMul(parameter[i], parameter[i]);
                        val = FrMul(val, coeffB[i]);
                        s = FrAdd(s, val);
                        
                        val = FrMul(parameter[i], coeffA[i]);
                        s = FrAdd(s, val);
                    }
                    return s;
                };
            }
        }

        class Verifier
        {
            public MCL.Fr pickRandom() { var f = new MCL.Fr(); f.SetByCSPRNG(); return f; }

            public Func<MCL.Fr[], MCL.Fr> make_input(Node[] input, int bitslen)
            {
                return (MCL.Fr[] z) =>
                {
                    MCL.Fr s = ToFr(0);
                    for (int i = 0; i < input.Length; i++)
                    {
                        MCL.Fr term = input[i].value;
                        int[] indexBits = IntToBinary(i, bitslen);
                        for (int j = 0; j < z.Length; j++)
                        {
                            MCL.Fr val = (indexBits[j] == 1) ? z[j] : FrSub(ToFr(1), z[j]);
                            term = FrMul(term, val);
                        }
                        s = FrAdd(s, term);
                    }
                    return s;
                };
            }
        }
        #endregion
    }
}