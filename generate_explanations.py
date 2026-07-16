"""
批次生成 GKR Protocol 步驟解釋模板
針對 6 種事件類型（SEND_RHO / SEND_S / CLAIM_VALUE / SUMCHECK_PASS /
SUMCHECK_PASS_FINAL / OPENING），各自生成 brief / standard / detailed
三種詳細程度的解釋文字,存成 JSON 供前端查表使用。

執行方式：
    python generate_explanations.py

輸出：
    explanation_templates.json
"""

import os
import json
from dotenv import load_dotenv
from anthropic import Anthropic

load_dotenv()
client = Anthropic(api_key=os.getenv("ANTHROPIC_API_KEY"))

MODEL = "claude-sonnet-4-6"

# ---------------------------------------------------------------
# 基礎知識庫：從你現有的 ChatPage.vue switch 邏輯整理出來的正確技術內容
# 這是「事實依據」，AI 生成時必須以此為準，不能自己捏造技術細節
# ---------------------------------------------------------------
BASE_KNOWLEDGE = {
    "SEND_RHO": {
        "name": "Verifier 送出隨機挑戰數 ρ",
        "fact": (
            "Verifier 送出隨機挑戰數 ρ（rho）給 Prover，要求對方回答多項式在此點的值。"
            "隨機性讓 Prover 無法預先偽造答案；若 Prover 說謊，被挑穿的機率極高"
            "（由有限體大小決定）。這是 Sumcheck 協議每一輪的標準流程之一。"
        ),
    },
    "SEND_S": {
        "name": "Verifier 選取隨機點 s",
        "fact": (
            "Verifier 選取隨機點 s，要求 Prover 提供下一層電路在 s 的求值，以便繼續驗證。"
            "每一層都需要新的隨機點，形成一條從輸出層延伸到輸入層的驗證鏈，"
            "最終收斂於可公開驗證的輸入。"
        ),
    },
    "CLAIM_VALUE": {
        "name": "Prover 提出計算結果",
        "fact": (
            "Prover 提出計算結果（Claim）。Verifier 將此結果與先前隨機挑戰進行一致性檢查。"
            "若 Prover 提出的值與電路實際值不符，Sumcheck 協議會在後續步驟將矛盾揭穿，"
            "Prover 無法逃脫。"
        ),
    },
    "SUMCHECK_PASS": {
        "name": "單層 Sumcheck 過關，轉往下一層",
        "fact": (
            "這一層（now_layer）的 Sumcheck 跑完最後一輪，且中間檢查通過。"
            "代表 Verifier 驗證完「這一層電路值可以化約成下一層電路值的多項式求和」是正確的，"
            "於是挑一個隨機點 r，把驗證『轉移』到下一層繼續，逐層往輸入層推進。"
            "注意：這只代表『這一層』過關，不代表整個協議已經驗證完成。"
        ),
    },
    "SUMCHECK_PASS_FINAL": {
        "name": "整條驗證鏈全部通過，協議成功",
        "fact": (
            "發生在跑到最後一層（輸入層）、且最終等式成立時。代表整條從輸出層到輸入層的"
            "Sumcheck 鏈全部驗證通過，Verifier 可以信任最初 Prover 宣告的輸出值 D() 是對的，"
            "接著會進入 KZG commitment 開放驗證階段。這是整個 GKR 協議『真正』驗證成功的時刻，"
            "跟前面每一層各自的 SUMCHECK_PASS（單層過關）語意不同，不可混淆。"
        ),
    },
    "OPENING": {
        "name": "GKR 驗證啟動",
        "fact": (
            "Prover 宣告電路的輸出值，GKR 協議正式開始。Verifier 將從輸出層逐層向下驗證到輸入層。"
            "GKR 協議的核心是把『電路計算的正確性』轉化為『一系列多項式求和問題』，"
            "讓驗證計算量大幅縮短。"
        ),
    },
}

LEVEL_INSTRUCTIONS = {
    "brief": "用一句話（20字以內）講重點，給已經懂 GKR 協議的人快速確認目前在做什麼，不要解釋原理。",
    "standard": "用 2-3 句話說明「現在在做什麼」以及「為什麼要做這步」，程度跟給資工系學生看的教學說明差不多。",
    "detailed": (
        "完整說明「現在在做什麼」「為什麼要做這步」，並額外提供一個生活化的類比"
        "（例如用日常情境比喻密碼學概念），適合完全沒有密碼學背景的初學者閱讀。"
    ),
}


def generate_explanation(type_key: str, fact: str, level: str) -> str:
    """呼叫 API，針對指定事件類型與詳細程度，生成解釋文字"""
    prompt = f"""你是一位密碼學助教，正在為 GKR Protocol（零知識證明協議）互動視覺化工具撰寫步驟說明文字。

以下是這個步驟的正確技術事實，你的解釋內容「必須」完全基於這些事實，不可以自己添加或改變任何技術細節：
---
{fact}
---

請針對這個步驟，撰寫解釋文字。要求：{LEVEL_INSTRUCTIONS[level]}

請直接輸出解釋文字本身，不要加上「好的」「以下是」之類的開頭語，不要用 Markdown 標題。"""

    # detailed 版本需要更多空間寫完類比說明，避免像之前一樣句子被硬生生截斷
    token_limit = 1000 if level == "detailed" else 400

    response = client.messages.create(
        model=MODEL,
        max_tokens=token_limit,
        messages=[{"role": "user", "content": prompt}],
    )
    return response.content[0].text.strip()


def main():
    result = {}
    total = len(BASE_KNOWLEDGE) * len(LEVEL_INSTRUCTIONS)
    count = 0

    for type_key, info in BASE_KNOWLEDGE.items():
        result[type_key] = {}
        for level in LEVEL_INSTRUCTIONS:
            count += 1
            print(f"[{count}/{total}] 生成中: {type_key} - {level} ...")
            try:
                text = generate_explanation(type_key, info["fact"], level)
                result[type_key][level] = text
            except Exception as e:
                print(f"  發生錯誤: {e}")
                result[type_key][level] = None

    output_path = "explanation_templates.json"
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False, indent=2)

    print(f"\n完成！已儲存至 {output_path}")
    print("建議：打開檔案人工檢查一次內容，確認技術描述沒有被 AI 誤解或講錯。")


if __name__ == "__main__":
    main()