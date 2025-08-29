using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// ロビー画面の管理クラス
/// ・参加人数の表示
/// ・オーナー専用UIと参加者用UIの切り替え
/// ・ゲーム開始ボタンの制御
/// </summary>
public class LobbyManager : UdonSharpBehaviour
{
    // --- 連携先 ---
    public GameManager gameManager;  // ゲーム全体を管理する GameManager

    // --- ロビー設定 ---
    public int minPlayers = 4;       // ゲーム開始に必要な最小人数

    // --- UI要素 ---
    public UnityEngine.UI.Text playerCountText; // 現在の参加人数表示
    public GameObject ownerUI;                   // オーナー専用UI（開始ボタン/設定ボタン）
    public GameObject waitingUI;                 // 参加者用UI（待機メッセージ）

    // =====================================================================
    // 初期化処理
    // =====================================================================
    private void Start()
    {
        // 同じオブジェクトに GameManager がアタッチされている想定で取得
        gameManager = this.GetComponent<GameManager>();
    }

    // =====================================================================
    // 毎フレーム更新（UI更新）
    // =====================================================================
    private void Update()
    {
        // 現在の参加人数を取得（VRChat API）
        int playerCount = VRCPlayerApi.GetPlayerCount();

        // UIに参加人数を表示
        playerCountText.text = "現在の参加人数: " + playerCount + "/8";

        // --- オーナーと参加者でUI切り替え ---
        if (Networking.IsOwner(gameObject))
        {
            // オーナー専用UIを表示
            ownerUI.SetActive(true);
            waitingUI.SetActive(false);

            // 開始ボタンの有効/無効を人数条件で制御
            ownerUI.transform.Find("StartButton").gameObject
                .SetActive(playerCount >= minPlayers);
        }
        else
        {
            // 参加者用UIを表示（待機メッセージなど）
            ownerUI.SetActive(false);
            waitingUI.SetActive(true);
        }
    }

    // =====================================================================
    // ボタンイベント：ゲーム開始
    // =====================================================================
    public void OnClickStartGame()
    {
        // オーナーのみが開始可能
        if (Networking.IsOwner(gameObject))
        {
            // GameManager の StartGame を呼び出してゲーム開始
            gameManager.StartGame();
        }
    }

    // =====================================================================
    // ボタンイベント：設定画面を開く
    // =====================================================================
    public void OnClickOpenSettings()
    {
        // オーナーのみが設定可能
        if (Networking.IsOwner(gameObject))
        {
            // 設定UIを開く処理（ここにUI展開やパネル表示の処理を入れる）
        }
    }
}
