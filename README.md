# ECS-AnimationSample
PureECS アニメーション検証

- Unity2018.3.0b6
- Entities 0.0.12-preview.19

内容を簡単に説明すると、GPU Instancingで描画されるモデルを頂点シェーダーでTextureに書き込まれたアニメーションデータを参照する形で動かし、それに伴うアニメーションの振り分けや再生キーフレームの管理などをECSで対応すると言ったサンプル。

シェーダーでのアニメーションについては「[sugi-cho/Animation-Texture-Baker](https://github.com/sugi-cho/Animation-Texture-Baker)」を使わせて頂きました。  
技術解説についてはテラシュールブログさんでも取り上げられているので詳細は「[こちら](http://tsubakit1.hateblo.jp/entry/2017/09/03/225713)」を参照。

**※以下のGIFでは配管工が動いてますが、実際にプロジェクトのサンプルで動くモデルは[ローポリユニティちゃん](http://unity-chan.com/contents/news/%E3%80%90unite2016tokyo%E3%80%91%E3%83%AD%E3%83%BC%E3%83%9D%E3%83%AA%E3%83%A6%E3%83%8B%E3%83%86%E3%82%A3%E3%81%A1%E3%82%83%E3%82%93%E5%85%AC%E9%96%8B%EF%BC%81%E3%80%90livemodeling%E3%80%91/)となります。**  

![sample](https://user-images.githubusercontent.com/17098415/47253650-d5136f80-d490-11e8-8af6-fdb463c1ceb5.gif)


# License

![dark_silhouette](https://user-images.githubusercontent.com/17098415/47374576-3f464180-d729-11e8-9b0b-c20d84b5ad88.jpg)

この作品は[ユニティちゃんライセンス条項](http://unity-chan.com/contents/license_jp/)の元に提供されています
