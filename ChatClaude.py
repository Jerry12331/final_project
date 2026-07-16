"""
第一階段任務二:提示詞工程實踐
互動式對話腳本 — 可以直接在 Terminal 打字跟 Claude 對話,方便測試不同 prompt

使用方式:
    python chat_claude.py

指令:
    輸入你的問題,按 Enter 送出
    輸入 exit 或 quit 結束程式
    輸入 clear 清空對話紀錄(重新開始一個新對話)
"""

import os
from dotenv import load_dotenv
from anthropic import Anthropic

load_dotenv()

client = Anthropic(api_key=os.getenv("ANTHROPIC_API_KEY"))

MODEL = "claude-sonnet-4-6"
MAX_TOKENS = 1000


def chat():
    # conversation_history 記錄目前對話中的所有訊息
    # 之所以要自己維護這個 list,是因為 API 本身沒有記憶,
    # 每次呼叫都要把完整對話歷史一起送過去,Claude 才會記得前面聊過什麼
    conversation_history = []

    print("=" * 50)
    print("Claude 互動式對話測試(輸入 exit 結束,clear 清空對話)")
    print("=" * 50)

    while True:
        user_input = input("\n你:")

        if user_input.strip().lower() in ["exit", "quit"]:
            print("再見!")
            break

        if user_input.strip().lower() == "clear":
            conversation_history = []
            print("對話紀錄已清空,重新開始。")
            continue

        if not user_input.strip():
            continue

        # 把使用者這次輸入的內容加進對話紀錄
        conversation_history.append({"role": "user", "content": user_input})

        try:
            response = client.messages.create(
                model=MODEL,
                max_tokens=MAX_TOKENS,
                messages=conversation_history
            )

            answer = response.content[0].text
            print(f"\nClaude:{answer}")

            # 把 Claude 的回應也加進對話紀錄,下一輪才能記得
            conversation_history.append({"role": "assistant", "content": answer})

            # 順便顯示這次用了多少 token,方便你觀察用量、抓成本感覺
            usage = response.usage
            print(f"\n[本次用量 — 輸入: {usage.input_tokens} tokens, 輸出: {usage.output_tokens} tokens]")

        except Exception as e:
            print(f"發生錯誤:{e}")
            # 發生錯誤時,把剛剛加進去的使用者訊息移除,避免下次對話帶著錯誤狀態
            conversation_history.pop()


if __name__ == "__main__":
    chat()