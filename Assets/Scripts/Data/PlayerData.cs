using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

/// <summary>
/// プレイヤー情報を管理するクラス
/// ・所持金、位置、所有物件などゲーム進行に必要なデータを保持
/// ・UdonSynced を使ってネットワーク上で同期
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)] // 手動同期：必要なタイミングで RequestSerialization() を呼ぶ
public class PlayerData : UdonSharpBehaviour
{
    // =====================================================================
    // プレイヤーID・接続状態
    // =====================================================================

    [UdonSynced] public int playerId;  // スロット番号（0..7）※GameManagerの配列インデックスと一致
    [UdonSynced] public int actorId;   // VRCPlayerApi.playerId。-1=空きスロット、-2=離脱保持など

    // =====================================================================
    // プレイヤー情報
    // =====================================================================

    [UdonSynced] public string playerName;  // プレイヤー名（VRC表示名）
    [UdonSynced] public int position;      // ボード上の位置（マス番号）
    [UdonSynced] public int money;         // 所持金
    [UdonSynced] public int[] ownedProperties; // 所有している物件のID配列（-1=空き）

    // =====================================================================
    // スロット初期化処理
    // =====================================================================
    /// <summary>
    /// スロットを空に初期化する
    /// </summary>
    /// <param name="slotIndex">スロット番号（playerId）</param>
    /// <param name="ownedSize">所持物件配列の長さ</param>
    public void ResetSlot(int slotIndex, int ownedSize)
    {
        playerId = slotIndex;
        actorId = -1;             // 空きスロット
        playerName = "";
        position = 0;
        money = 0;

        if (ownedProperties == null || ownedProperties.Length != ownedSize)
            ownedProperties = new int[ownedSize];

        for (int i = 0; i < ownedProperties.Length; i++)
            ownedProperties[i] = -1; // 空き初期化
    }

    // =====================================================================
    // 空き判定
    // =====================================================================
    /// <summary>
    /// このスロットが空きかどうかを返す
    /// </summary>
    /// <returns>true = 空きスロット, false = 使用中</returns>
    public bool IsVacant()
    {
        return actorId == -1;
    }
}
