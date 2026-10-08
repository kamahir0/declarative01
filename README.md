# POLY / GRID — 宣言的なゲーム設計の実験

「人はゲームをどこまで宣言的に設計できるか」を、同じパズルのUnity版とReact版で検証するプロジェクトです。

6×6の盤面を9個のテトロミノで埋めます。ピースを選び、移動・回転して配置し、重複・不足なく完成したら提出します。提出成功後はピースが順番に発光します。

![React版の画面](React/docs/images/initial.png)

## React版

React + TypeScript + Vite、Motion for React、CSS / SVGを使用します。正規状態は`useReducer`で保持し、純粋なReducerとルール関数で更新・判定します。バックエンドは不要です。

macOSでは、Node.jsをインストールしたうえで **[React/Start.command](React/Start.command)をFinderからダブルクリック**すると、依存パッケージの準備、開発サーバーの起動、ブラウザを開くところまで自動で行います。終了はTerminalで`Control+C`です。

ターミナルから起動する場合:

```sh
cd React
npm ci
npm run dev
```

対応するNode.jsのバージョン、操作方法、テストは[React版README](React/README.md)、設計上の判断は[React版の設計の観察](React/docs/architecture.md)を参照してください。

## Unity版

Unity **6000.6.4f1**で`Unity/`を開き、`Assets/Puzzle/Scenes/PolyGrid.unity`を開いてPlayします。純粋なDomain、UseCase、Repository、Presenter、Unity Viewで構成し、R3とDOTweenを使用します。

起動・操作・検証方法は[Unity版README](Unity/README.md)、設計は[Unity版アーキテクチャ](Unity/docs/architecture.md)、同梱する依存関係は[第三者ライブラリ・フォント](Unity/THIRD_PARTY.md)を参照してください。

## 共通のルールと比較

両版の盤面・ピース形状・色・左下原点の座標は共通です。重なりを含む仮置きを許容し、完成・占有状況・提出可否を状態から導出します。[動作確認用の解](Unity/docs/sample-solution.md)も共通です。

React版は左右回転と累積回転数を使用し、論理的な4方向とアニメーションの角度を分けています。UIフレームワークによって減った状態同期の処理と、回転表示・非同期処理・入力に残る複雑さを比較します。宣言的な表現のためだけの仕組みは作らず、命令的な処理が素朴な場面ではその使用を許容します。
