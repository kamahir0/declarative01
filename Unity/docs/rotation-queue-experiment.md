# 左右回転と演出キューの追加実験

## ベースライン

変更前はコミット `49805e3`、タグ `unity-baseline-before-rotation-queue` に保存した。Unity/Reactのフォルダ構成と、Unity版の既存36テストを含む。比較は次で行える。

```sh
git diff unity-baseline-before-rotation-queue -- Unity
```

React版の実装は変更していない。今回のUnity版はQで左回転、Rで右回転を行う。

## 結果と振る舞い

1回の回転を0.2秒のDOTweenで再生する。時間はTime.timeScaleに依存しない。回転入力は再生中でもUseCaseが即時に処理し、受理された方向と到達先の表示値をFIFOへ保存する。アニメーション完了を待ってから次の入力を処理する方式にはしていない。

Q→R→Rなら論理状態はその場で左→元の向き→右へ進む。表示はその3回を0.2秒ずつ再生する。右回転4回で論理状態が元に戻っても、演出4回は消さない。キューは画面全体で一つなので、Setして別ピースを選んだ場合も回転の入力順を保つ。

回転中のMove / Setも元の操作条件で受け付ける。回転の到達先は入力時のスナップショットを使い、最後に最新Renderの位置・回転・所属へ整合させる。Moveそのものの全履歴を再生する仕様は追加していない。

Restartはキューと実行中Tweenを破棄してから初期状態をRenderする。元の提出フェーズの制限は維持しており、Submitting / Celebrating中のRestartは受け付けない。持ちピースへの返却ではそのピースの古い演出を無効にする。再選択しても古い演出は復活しない。非活性化・破棄時にもTweenを止め、後から完了コールバックが古いキューを再開しない。

Submitは元の完成条件で受け付ける。成功演出は回転キューが空になるまで待ち、回転とGlowが同時に姿勢を更新することを防ぐ。

## 新しく必要になった状態と責務

| 所有者 | 追加した状態・責務 |
|---|---|
| Domain / UseCase | 回転方向（±1）。GameRules.SpinとSpinUseCaseが同じ盤面内判定で左右に対応 |
| Presenter | 成功した回転ごとにQueueRotationを呼び、その後に最新状態をRender。Restart・終了時に中断を依頼 |
| RotationPlayback | FIFO、実行中の命令とTween、排出完了を待つTask。完了時に次の命令を開始。画面全体の再生順を管理 |
| PieceView | 予約済み回転数、最新Renderの目標値、回転Tween、演出の世代番号。回転中の姿勢を守り、完了後に正規セル集合へ一致させる |
| PuzzleView | 入力とキューの接続、終了時の中断、Glow開始前のキュー待ち。Q/RはInputActionのイベントをR3で受け取り、同一フレームの入力順も保持 |

GameState、PuzzleState、InteractionState、ViewStateの定義、Repository、Composition Rootの依存構成は変えていない。演出用の可変状態をGameStateへ追加していない。

## 命令的にどの程度素朴に書けたか

FIFOの処理は70行の新規クラスに収まった。基本は「追加する」「先頭を取り出す」「0.2秒のTweenを開始する」「完了時に次へ進む」。独自の演出DSLや、演出用のUseCase・Repository・大きな状態機械は導入していない。

キューそのものよりも、既存のRenderとの調停と終了処理に注意が必要だった。最新Renderを即適用すると未再生の回転が消えるため、PieceViewで目標値を保持する必要がある。また、返却後の再選択では同じPieceViewが再利用されるため、世代番号で古い命令を区別した。中断時は先に実行中命令を外し、その後でTweenをKillすることで、OnKillが次の演出を始めることを防いでいる。

これは実装上の観察であり、Reactより必ず少ないコードで書けることを示す比較ではない。React側の回転演出を今回のFIFO仕様へ変更した比較は行っていない。

## Render(ViewState)との関係

Renderは「その時点の正規状態から導出した最終表示」を引き続き指定する。ボタン、提出可否、進捗、重なり表示は直ちに更新する。ピースの姿勢だけは、予約された回転がある間、指定された目標に時間をかけて追いつく。キューが空なら、従来どおり任意のViewStateから姿勢を描画できる。

回転の再生順序はViewStateだけでは復元できない。たとえば入力なしと右回転4回は同じ最終状態になるが、今回の仕様では異なる演出が必要になる。その履歴をGameStateやViewStateに入れる代わりに、成功した状態遷移に付随する命令として演出キューへ渡した。

したがって「正規状態から最終表示を導出する」設計は維持できたが、「途中の表示も最新状態だけで決まる」性質はこの仕様では維持していない。宣言的な目標値と命令的な時間軸を、PresenterとViewの境界で調停する実装となった。全面的なアーキテクチャ変更は不要だった。

## 検証と変更規模

2026-10-09、Unity 6000.6.4f1 / macOSでEditMode 34件、PlayMode 13件、計47件成功。既存36件に11件を追加した。詳細は [検証記録](verification.md) を参照。Playerビルドは未実施。

ベースラインとの差分は20ファイル。以下はGitの追加・削除行数で、移動・書き換えも含む。シーンの数値はUnityのシリアライズによる差分で、ゲームロジックの行数とは分けている。

| 分類 | ファイル数 | 追加 / 削除 |
|---|---:|---:|
| 実行コード（C#） | 8（新規1） | 173 / 15行 |
| テスト（C#） | 3（新規1） | 236 / 0行 |
| PlayModeテストasmdef | 1 | 2 / 2行 |
| PolyGridシーン | 1 | 353 / 63行 |
| 新規スクリプトのmeta | 2 | 4 / 0行 |
| README・設計・検証・本レポート | 5 | 文書更新 |

変更した実行コードは`Domain/Values.cs`、`Domain/GameRules.cs`、`Application/UseCases.cs`、`Presentation/PuzzlePresenter.cs`、`Unity/PieceView.cs`、`Unity/PuzzleView.cs`、`Unity/PuzzleSceneLayout.cs`、新規`Unity/RotationPlayback.cs`。テストは`GameRulesTests.cs`、`UseCaseAndPresenterTests.cs`、新規`RotationQueueTests.cs`とPlayModeテストasmdef。シーンでは左回転ボタンとQ/R表記、参照、ボタン配置を更新した。フォント本体とマスターデータは変更していない。
