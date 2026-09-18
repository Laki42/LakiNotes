# Laki Notes 0.1.4 — Beat Saber 1.40.8 / 1.40.8_7379

Multiplayer+ 6.4.2のローカルNote装飾を再有効化しました。Solo / Practice / Small Notes / Pro Modeは従来の実装を維持します。通常Multiplayer / BeatTogether用のInstallerは引き続き登録しません。

ゲーム終了後、ZIP内Plugins/Laki.dllを既存の同名DLLと置き換えてください。診断版ではなく通常版です。UserData/Laki.jsonを削除する必要はありません。既存設定とSecret Pityを継続利用します。

今回の実機比較では、Lakiなしでも曲開始に失敗し、AutoPauseStealthを退避すると復旧し、Laki 0.1.3を戻してもプレイできました。0.1.4の確認もAutoPauseStealthを退避した同じ構成で行ってください。Lakiから他MODの停止・書き換えは行いません。

Mod Settings → Laki Notes:

| 設定 | 動作 |
|---|---|
| Enable Laki Notes | 全体のON/OFF。OFFはSessionを登録しません |
| Enable Effects | 正常Cutの成功Effect。OFFでもNote装飾は表示 |
| Enable Rare Laki Notes | 通常抽選のSuper/Secretを有効化。Forceは独立 |
| Enable in Multiplayer | Multiplayer+のローカル装飾。OFFはbinding前に除外 |
| Test Mode | Off / Force Laki / Force Super Laki / Force Secret Laki |

確実に確認するにはEnable Laki Notes、Enable in Multiplayer、Enable EffectsをONにし、Force Lakiを選んでからMultiplayer+の曲を開始してください。既存の有効Noteの中央付近から1個を選び、選択・Visual準備・イベント登録に成功したときだけOffへ戻します。通常抽選は40%なので、Test Mode Offでは毎曲表示されません。

通常抽選は出現40%、種類75% / 20% / 5%とSecret Pityを維持します。Forceは通常抽選を通らず、Pity増加・リセットを行いません。Settingsへ戻るとConfigのOffへ同期します。設定の保存はBSIPA Generated Configです。

NoteData、Note位置・回転・移動・タイミング、collider、Cut判定、score、combo、energy、通信を変更しません。専用ローカルVisualのみを追加します。正常CutのみSuccess Effect、Bad Cut/Missではなし。Small Notes補正もVisual側だけです。

Multiplayer+の確認対象は実DLL 6.4.2.0です。未知の版・判定不能時は装飾を停止します（Soloにも影響し得ます）。Ghost Notes / Disappearing Arrows / Zen、未対応のNoteや譜面構造、Replay等は従来の安全判定で表示しません。

Release: warning 0 / error 0。自動検証12,283 assertions成功、実DLL監査成功。0.1.4のMultiplayer+実描画・他端末からの確認・完走は未実施です。

ビルド方法はREADME.mdを参照してください。後日のユーザー実機確認ではMultiplayer+での装飾動作を確認できました。Cut Effectと各Modifierの組み合わせすべてを確認済みという意味ではありません。

