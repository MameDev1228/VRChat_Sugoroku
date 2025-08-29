using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

/// <summary>
/// ゲーム全体を管理するメインクラス
/// ・プレイヤー登録/離脱管理
/// ・ターン制の進行
/// ・サイコロ・移動・物件購入・通行料の処理
/// ・ゲーム開始/終了処理
/// UdonBehaviourSyncMode.Manual にして、必要な箇所のみ同期
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class GameManager : UdonSharpBehaviour
{
    // --- 同期対象 ---
    [UdonSynced] public int currentTurnPlayerId; // 現在ターンのスロット番号
    [UdonSynced] public int roundCount;         // 現在のラウンド数

    public int maxRounds = 20;                  // 最大ラウンド数
    public int maxOwnedPerPlayer = 16;          // プレイヤーが持てる物件数上限

    public PlayerData[] playerList;             // プレイヤースロット（Inspectorで8個設定必須）
    public Property[] properties;               // ボード上のマス（物件）

    private System.Random rng = new System.Random(); // サイコロ用乱数

    // =====================================================================
    // 起動時処理
    // =====================================================================
    /// <summary>
    /// シーンロード時に全プレイヤースロットを初期化
    /// （PlayerData が Inspector で割り当てられていることが前提）
    /// </summary>
    public void Start()
    {
        for (int i = 0; i < playerList.Length; i++)
        {
            if (playerList[i] != null)
            {
                // スロットを空にして初期化
                playerList[i].ResetSlot(i, maxOwnedPerPlayer);
                playerList[i].RequestSerialization(); // 状態同期
            }
        }
        roundCount = 0; // 0 = ロビー中
    }

    // =====================================================================
    // プレイヤー参加/離脱時のイベントフック
    // =====================================================================
    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        // ホストのみがプレイヤー管理
        RegisterPlayer(player);
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        bool inGame = roundCount > 0; // すでにゲーム開始済みか
        UnregisterPlayer(player, inGame);
    }

    // =====================================================================
    // プレイヤー管理
    // =====================================================================
    /// <summary>
    /// プレイヤーを空きスロットに登録
    /// - 空きスロットを探す
    /// - PlayerData を初期化
    /// - 所持物件配列も初期化
    /// - オーナー権取得 + 同期
    /// </summary>
    public void RegisterPlayer(VRCPlayerApi player)
    {
        for (int i = 0; i < playerList.Length; i++)
        {
            var pd = playerList[i];
            if (pd != null && pd.IsVacant())
            {
                // 空きスロットの PlayerData のオーナー権を取得
                if (!Networking.IsOwner(pd.gameObject))
                    Networking.SetOwner(Networking.LocalPlayer, pd.gameObject);

                // プレイヤー情報設定
                pd.actorId = player.playerId;
                pd.playerName = player.displayName;
                pd.money = 1000;
                pd.position = 0;

                // 所持物件配列の初期化（保険）
                if (pd.ownedProperties == null || pd.ownedProperties.Length != maxOwnedPerPlayer)
                {
                    pd.ownedProperties = new int[maxOwnedPerPlayer];
                    for (int j = 0; j < pd.ownedProperties.Length; j++)
                        pd.ownedProperties[j] = -1;
                }

                pd.RequestSerialization(); // 変更を同期
                Debug.Log($"Join: {pd.playerName} → slot {i}");
                return;
            }
        }

        Debug.LogWarning("参加者上限に達しています。");
    }

    /// <summary>
    /// プレイヤーをスロットから削除
    /// - ゲーム中は actorId を -2 にしてデータ保持
    /// - ロビー中はスロットを空に戻す
    /// - オーナー権取得 + 同期
    /// </summary>
    public void UnregisterPlayer(VRCPlayerApi player, bool inGame)
    {
        for (int i = 0; i < playerList.Length; i++)
        {
            var pd = playerList[i];
            if (pd != null && pd.actorId == player.playerId)
            {
                if (!Networking.IsOwner(pd.gameObject))
                    Networking.SetOwner(Networking.LocalPlayer, pd.gameObject);

                if (inGame)
                {
                    pd.actorId = -2; // 離脱中マーク
                    pd.RequestSerialization();
                    Debug.Log($"Leave in game: {pd.playerName} (slot {i}) → keep data");
                }
                else
                {
                    pd.ResetSlot(i, maxOwnedPerPlayer); // 空に戻す
                    pd.RequestSerialization();
                    Debug.Log($"Leave in lobby: slot {i} cleared");
                }

                return;
            }
        }
    }

    // =====================================================================
    // ゲーム開始処理
    // =====================================================================
    /// <summary>
    /// ゲーム開始
    /// - 参加済みプレイヤーを初期化
    /// - ターン開始
    /// </summary>
    public void StartGame()
    {
        roundCount = 1;
        currentTurnPlayerId = 0;

        // 参加中プレイヤーのみ初期化
        for (int i = 0; i < playerList.Length; i++)
        {
            var p = playerList[i];
            if (p != null && p.actorId >= 0)
            {
                p.money = 1000;
                p.position = 0;
                for (int j = 0; j < p.ownedProperties.Length; j++)
                    p.ownedProperties[j] = -1;

                p.RequestSerialization(); // 同期
            }
        }

        Debug.Log("Game Start!");
        StartTurn();
    }

    // =====================================================================
    // ターン処理
    // =====================================================================
    private void StartTurn()
    {
        var player = playerList[currentTurnPlayerId];

        // 空スロット・離脱中スロットはスキップ
        if (player == null || player.actorId < 0)
        {
            EndTurn();
            return;
        }

        Debug.Log($"Player slot {player.playerId} のターン開始（{player.playerName}）");
        // ダイスUI表示や操作待ち処理
    }

    // =====================================================================
    // サイコロ処理
    // =====================================================================
    public void RollDice()
    {
        var p = playerList[currentTurnPlayerId];
        if (p == null || p.actorId < 0) { EndTurn(); return; }

        int dice = rng.Next(1, 7);
        Debug.Log($"出目: {dice}");
        MovePlayer(p, dice);
    }

    private void MovePlayer(PlayerData player, int steps)
    {
        player.position += steps;

        // ボードを一周したらボーナス
        if (player.position >= properties.Length)
        {
            player.position -= properties.Length;
            player.money += 200;

            if (!Networking.IsOwner(player.gameObject))
                Networking.SetOwner(Networking.LocalPlayer, player.gameObject);

            player.RequestSerialization(); // 同期
            Debug.Log($"周回ボーナス：{player.playerName} +200");
        }

        var landed = properties[player.position];
        Debug.Log($"{player.playerName} → {landed.name}");

        ResolveTile(player, landed); // マス到着後処理
        player.RequestSerialization(); // 同期
    }

    // =====================================================================
    // マス処理（購入・通行料・増資など）
    // =====================================================================
    private void ResolveTile(PlayerData player, Property prop)
    {
        if (prop.ownerId == -1) // 未所有
        {
            if (player.money >= prop.price) PurchaseProperty(player, prop);
        }
        else if (prop.ownerId != player.playerId) // 他人所有
        {
            PayToll(player, playerList[prop.ownerId], prop);
        }
        else
        {
            Debug.Log("自分の物件：増資チャンス（未実装）");
        }

        EndTurn(); // ターン終了
    }

    private void PurchaseProperty(PlayerData buyer, Property prop)
    {
        buyer.money -= prop.price;
        prop.ownerId = buyer.playerId; // スロット番号で所有者管理

        // 同期（所有者データ + 物件データ）
        if (!Networking.IsOwner(buyer.gameObject)) Networking.SetOwner(Networking.LocalPlayer, buyer.gameObject);
        if (!Networking.IsOwner(prop.gameObject)) Networking.SetOwner(Networking.LocalPlayer, prop.gameObject);

        for (int i = 0; i < buyer.ownedProperties.Length; i++)
        {
            if (buyer.ownedProperties[i] == -1)
            {
                buyer.ownedProperties[i] = prop.propertyId;
                break;
            }
        }

        buyer.RequestSerialization();
        prop.RequestSerialization();

        Debug.Log($"{buyer.playerName} が {prop.name} を購入！");
    }

    private void PayToll(PlayerData visitor, PlayerData owner, Property prop)
    {
        int toll = prop.toll;
        visitor.money -= toll;
        owner.money += toll;

        // 同期
        if (!Networking.IsOwner(visitor.gameObject)) Networking.SetOwner(Networking.LocalPlayer, visitor.gameObject);
        if (!Networking.IsOwner(owner.gameObject)) Networking.SetOwner(Networking.LocalPlayer, owner.gameObject);

        visitor.RequestSerialization();
        owner.RequestSerialization();

        Debug.Log($"{visitor.playerName} → {owner.playerName} に通行料 {toll}");
    }

    // =====================================================================
    // ターン終了処理
    // =====================================================================
    private void EndTurn()
    {
        currentTurnPlayerId++;
        if (currentTurnPlayerId >= playerList.Length)
        {
            currentTurnPlayerId = 0;
            roundCount++;
        }

        if (roundCount > maxRounds)
            EndGame();
        else
            StartTurn();
    }

    // =====================================================================
    // ゲーム終了処理
    // =====================================================================
    private void EndGame()
    {
        Debug.Log("ゲーム終了！資産集計…");

        int winnerSlot = -1;
        int maxWealth = int.MinValue;

        for (int i = 0; i < playerList.Length; i++)
        {
            var p = playerList[i];
            if (p == null) continue;

            int wealth = p.money;
            for (int k = 0; k < p.ownedProperties.Length; k++)
            {
                int pid = p.ownedProperties[k];
                if (pid >= 0) wealth += properties[pid].price;
            }

            Debug.Log($"slot {i} ({p.playerName}) 総資産 {wealth}");
            if (wealth > maxWealth)
            {
                maxWealth = wealth;
                winnerSlot = i;
            }
        }

        Debug.Log($"勝者: slot {winnerSlot}（{playerList[winnerSlot].playerName}） 総資産 {maxWealth}");
    }
}
