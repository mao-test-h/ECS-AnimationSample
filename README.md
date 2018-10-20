# ECS-AnimationSample
PureECS アニメーション検証

- Unity2018.3.0b6
- Entities 0.0.12-preview.19

内容を簡単に説明すると、GPU Instancingで描画されるモデルを頂点シェーダーでTextureに書き込まれたアニメーションデータを参照する形で動かし、それに伴うアニメーションの振り分けや再生キーフレームの管理などをECSで対応すると言ったサンプル。

※ちなみに表示モデルである旧Unityおじさん(配管工)はライセンスが見えなかったのでプロジェクトには含んでおりません。動かすには自前でインポートする必要があります。  
→ただ、含まれていないと色々と確認しにくいかと思われるのでいずれ再配布を考慮してモデルを差し替える予定。

シェーダーでのアニメーションについては「[sugi-cho/Animation-Texture-Baker](https://github.com/sugi-cho/Animation-Texture-Baker)」を使わせて頂きました。  
技術解説についてはテラシュールブログさんでも取り上げられているので詳細は「[こちら](http://tsubakit1.hateblo.jp/entry/2017/09/03/225713)」を参照。

![sample](https://user-images.githubusercontent.com/17098415/47253650-d5136f80-d490-11e8-8af6-fdb463c1ceb5.gif)
