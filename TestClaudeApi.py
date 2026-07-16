"""
第一階段任務一:LLM 工具與 API 調用研究
最小測試腳本 — 呼叫 Anthropic (Claude) API,送出一句話並取得回應

使用前準備:
1. pip install anthropic python-dotenv --break-system-packages
2. 到 https://console.anthropic.com 申請 API Key
3. 在同一資料夾建立 .env 檔案,內容寫: ANTHROPIC_API_KEY=你的key
"""

import os
from dotenv import load_dotenv
from anthropic import Anthropic

# 從 .env 讀取 API Key,避免把金鑰寫死在程式碼裡(基本資安習慣)
load_dotenv()

client = Anthropic(api_key=os.getenv("ANTHROPIC_API_KEY"))


def ask_claude(prompt: str, model: str = "claude-sonnet-4-6", max_tokens: int = 1000) -> str:
    """
    最基本的 API 呼叫函式:送出一段文字,拿到 Claude 的回應文字

    Args:
        prompt: 想問 Claude 的問題
        model: 使用的模型名稱
        max_tokens: 回應的最大 token 數量

    Returns:
        Claude 回應的文字內容
    """
    response = client.messages.create(
        model=model,
        max_tokens=max_tokens,
        messages=[
            {"role": "user", "content": prompt}
        ]
    )

    # response.content 是一個 list,裡面可能包含多個內容區塊
    # 一般文字回應只會有一個 type="text" 的區塊
    return response.content[0].text


if __name__ == "__main__":
    test_prompt = "請用一句話介紹你自己,並說明你能幫我做什麼。"

    print(f"發送給 Claude 的問題:{test_prompt}\n")
    print("等待回應中...\n")

    try:
        answer = ask_claude(test_prompt)
        print("Claude 的回應:")
        print(answer)
    except Exception as e:
        print(f"發生錯誤:{e}")
        print("請檢查:1) API Key 是否正確設定在 .env  2) 網路連線是否正常")