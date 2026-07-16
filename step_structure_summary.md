# GKR 協議事件（Step）結構說明

本文件說明 [fd/GKR_Backend/GkrService.cs](fd/GKR_Backend/GkrService.cs) 產生的 `GkrEvent` 事件序列結構，
以及 [frontend/src/pages/ChatPage.vue](frontend/src/pages/ChatPage.vue) 如何消費這些事件來驅動逐步（step-by-step）UI。

## 事件資料結構（GkrEvent）

定義於 [fd/GKR_Backend/GkrModels.cs](fd/GKR_Backend/GkrModels.cs)：

| 欄位 | 型別 | 說明 |
| --- | --- | --- |
| `ProtocolLayer` | int | 目前所在的電路層數 |
| `Round` | int | 目前層內的回合數 |
| `Role` | string? | 事件發送角色：`"Prover"` / `"Verifier"` / `"System"` |
| `Type` | string? | 事件類型（見下表） |
| `Message` | string? | 給人看的說明文字 |
| `Data` | object? | 該事件附帶的結構化資料（例如數值、隨機數） |

## 事件類型（Type）一覽

| Type | 發送方法 | 角色 | 觸發時機 |
| --- | --- | --- | --- |
| （無 / 預設）系統訊息 | `AddSystemEvent` | System | 協議啟動、輸入輸出摘要、KZG Setup、驗證成功提示等旁白訊息 |
| `CLAIM_VALUE` | `AddCommitmentEvent` | Prover | Prover 對輸入層送出 KZG 承諾（commitment） |
| `SEND_FIXED_VAR` | `AddVerifierEvent` | Verifier | Verifier 送出初始隨機挑戰點 `fixed_var` |
| `CLAIM_D` | `AddProverEvent` | Prover | Prover 回報 `D(fixed_var)` 的宣稱值 |
| `SEND_MASKSUM` | `AddProverEvent` | Prover | 每層開始時，Prover 送出遮罩多項式（mask polynomial）總和 |
| `SEND_RHO` | `AddVerifierEvent` | Verifier | Verifier 送出隨機數 `rho`，用於混合 mask 與原始 sumcheck |
| `SEND_S` | `AddVerifierEvent` | Verifier | 單輪 sumcheck 中，Verifier 送出隨機挑戰點 `s_i` |
| `CLAIM_G` | `AddProverEvent` | Prover | Prover 回報 `G_i(s_i)` 的宣稱值 |
| `SEND_MASKSUM_FINAL` | `AddProverEvent` | Prover | 抵達最後一層（輸入層前）時，Prover 送出最終 mask 總和 |
| `SUMCHECK_PASS` | `AddVerifierEvent` | Verifier | **單層** sumcheck 全部回合通過，Verifier 送出下一層隨機挑戰 `r_{layer+1}`（尚未到達整條鏈的終點，仍會進入下一層繼續驗證） |
| `SUMCHECK_PASS_FINAL` | `AddVerifierEvent` | Verifier | **整條 Sumcheck 鏈**（所有層）全部驗證通過的終點事件，代表 Verifier 可以信任 `D()`，之後進入 KZG 開放驗證階段 |
| （無 / 預設）驗證失敗訊息 | `AddVerifierEvent` | Verifier | Sumcheck 任一輪、中間層檢查、或最終檢查失敗時的訊息（例如 `"V: Sumcheck Failed!"`、`"V: intermediate check failed"`、`"V: final check failed"`） |

> `SUMCHECK_PASS` 與 `SUMCHECK_PASS_FINAL` 是兩個語意不同的事件：
> - `SUMCHECK_PASS`：每一層 sumcheck 結束時都會觸發一次（共 `totalLayers - 2` 次），代表「這一層」驗證通過。
> - `SUMCHECK_PASS_FINAL`：只會在整個協議最後一層驗證完成時觸發一次，代表「全部層」都驗證通過。

## 前端消費方式

[frontend/src/pages/ChatPage.vue](frontend/src/pages/ChatPage.vue) 將所有事件攤平為 `flattenedRounds`，並用以下邏輯判斷整體驗證是否成功：

```js
const verificationFailed = computed(() =>
  flattenedRounds.value.some(r =>
    r.verifier.toLowerCase().includes('failed') || r.prover.toLowerCase().includes('failed')
  )
);

const verificationSucceeded = computed(() =>
  !verificationFailed.value &&
  flattenedRounds.value.some(r => r.type === 'SUMCHECK_PASS_FINAL')
);
```

`verificationSucceeded` 只在出現 `SUMCHECK_PASS_FINAL`（整條鏈驗證完成）且沒有任何失敗訊息時才為 `true`；
單層通過的 `SUMCHECK_PASS` 事件不會觸發整體成功狀態，僅用於顯示每層進度。
