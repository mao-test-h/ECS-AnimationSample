namespace MainContents.ECS
{
    using System;
    using UnityEngine;
    using Unity.Entities;

    // ------------------------------
    #region // ComponentData

    /// <summary>
    /// 再生情報
    /// </summary>
    public struct AnimationPlayData : IComponentData
    {
        /// <summary>
        /// 現在の再生キーフレーム
        /// </summary>
        public float CurrentKeyFrame;

        /// <summary>
        /// アニメーションタイプ
        /// </summary>
        public AnimationType AnimationType;
    }

    #endregion // ComponentData

    // ------------------------------
    #region // Enums

    /// <summary>
    /// アニメーションタイプ
    /// </summary>
    public enum AnimationType
    {
        Run = 0,
        Slide,
        Wait,
    }

    #endregion // Enums

    // ------------------------------
    #region // Animation Data

    /// <summary>
    /// 再生アニメーションデータ
    /// </summary>
    [Serializable]
    public sealed class AnimationMesh
    {
        public Mesh Mesh;
        public Material AnimationMaterial;
    }

    #endregion // Animation Data
}

namespace MainContents.ECS
{
    using System;
    using System.Runtime.InteropServices;

    using UnityEngine;

    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Transforms;
    using Unity.Jobs;
    using Unity.Burst;
    using Unity.Collections;

    /// <summary>
    /// Animation Instancing System
    /// </summary>
    [ExecuteInEditMode]
    [UpdateAfter(typeof(EndFrameTransformSystem))]
    public sealed class AnimationInstancingSystem : JobComponentSystem
    {
        // ------------------------------
        #region // Defines

        /// <summary>
        /// コンストラクタ引数
        /// </summary>
        public sealed class ConstructorParameter
        {
            /// <summary>
            /// 各種再生アニメーションデータ
            /// </summary>
            public AnimationMesh[] AnimationMeshes;
        }

        /// <summary>
        /// Materialに渡す情報
        /// </summary>
        sealed class SendBuffers
        {
            /// <summary>
            /// Shaderに渡すアニメーションの再生情報
            /// </summary>
            public ComputeBuffer SendPlayBuffer = null;

            /// <summary>
            /// Graphics.DrawMeshInstancedIndirect -> bufferWithArgs
            /// </summary>
            public ComputeBuffer GPUInstancingArgsBuffer = null;

            /// <summary>
            /// 現在のインスタンス数
            /// </summary>
            public int CurrentInstance = -1;
        }

        /// <summary>
        /// Shaderに渡すアニメーションの再生情報
        /// </summary>
        /// <remarks>「Custom/TextureAnimPlayer-InstancingIndirect -> playData」が該当</remarks>
        struct SendPlayData
        {
            /// <summary>
            /// 現在の再生キーフレーム
            /// </summary>
            public float CurrentKeyFrame;

            /// <summary>
            /// モデル変換行列
            /// </summary>
            public float4x4 LocalToWorld;
        }

        #endregion // Defines

        // ------------------------------
        #region // Jobs

        /// <summary>
        /// アニメーションタイプ毎に再生情報を振り分けるJob
        /// </summary>
        [BurstCompile]
        struct MapAnimationPlayDataJob : IJobParallelFor
        {
            // 再生情報
            [ReadOnly] public NativeArray<AnimationPlayData> AnimationPlayData;
            // モデル変換行列
            [ReadOnly] public NativeArray<LocalToWorld> LocalToWorlds;

            // 振り分けた結果を格納するHashMap
            public NativeMultiHashMap<int, SendPlayData>.Concurrent SendPlayDataMap;

            public void Execute(int index)
            {
                var playData = AnimationPlayData[index];
                var data = new SendPlayData
                {
                    CurrentKeyFrame = playData.CurrentKeyFrame,
                    LocalToWorld = LocalToWorlds[index].Value,
                };
                SendPlayDataMap.Add((int)playData.AnimationType, data);
            }
        };

        /// <summary>
        /// 再生情報の更新
        /// </summary>
        [BurstCompile]
        struct PlayAnimationJob : IJobProcessComponentData<AnimationPlayData>
        {
            // Time.deltaTime
            [ReadOnly] public float DeltaTime;
            // 各アニメーションの再生時間
            [ReadOnly] public NativeArray<float> AnimationLengthList;
            // 最大アニメーションタイプ数
            [ReadOnly] public int MaxAnimationType;

            public void Execute(ref AnimationPlayData data)
            {
                // 最終フレームまで再生したらアニメーションを切り替える
                int currentAnimType = (int)data.AnimationType;
                if (data.CurrentKeyFrame >= this.AnimationLengthList[currentAnimType])
                {
                    int nextType = currentAnimType + 1;
                    data.AnimationType = (AnimationType)(nextType % this.MaxAnimationType);
                    data.CurrentKeyFrame = 0f;
                    return;
                }

                // フレームを進めるだけ。
                data.CurrentKeyFrame += this.DeltaTime;

                // TIPS.
                // 今回の実装としては上記の通り単純に順番通りに再生していくだけの物となるが、
                // 例えばゲームを実装する上ではEntityのステータスなどを取得し、
                // それを条件式で見てその時に応じたアニメーションに切り替えたりすると言った事も出来るかと思われる。
                // → e.g. 体力が入っているComponentDataも取得/参照して残り体力に応じたアニメーションに切り替えるなど。
            }
        }

        #endregion // Jobs

        // ------------------------------
        #region // Private Fields

        /// <summary>
        /// 描画対象のEntityのComponentGroup
        /// </summary>
        ComponentGroup _rendererGroup;

        /// <summary>
        /// DrawMeshInstancedIndirectに渡す情報
        /// </summary>
        uint[] _GPUInstancingArgs = new uint[5] { 0, 0, 0, 0, 0 };

        /// <summary>
        /// 再生アニメーションデータ
        /// </summary>
        AnimationMesh[] _animationMeshes;

        /// <summary>
        /// Materialに渡す情報
        /// </summary>
        SendBuffers[] _sendBuffers;

        /// <summary>
        /// 各アニメーションの再生時間
        /// </summary>
        NativeArray<float> _animationLengthList;

        /// <summary>
        /// 最大アニメーションタイプ数
        /// </summary>
        int _maxAnimationType;

        /// <summary>
        /// 「Custom/TextureAnimPlayer-InstancingIndirect -> _PlayDataBuffer」のID
        /// </summary>
        int _playDataBufferID;

        // ComponentDataArray コピー用
        NativeArray<LocalToWorld> _localToWorlds;
        NativeArray<AnimationPlayData> _animationPlayData;

        #endregion // Private Fields


        // ----------------------------------------------------
        #region // Public Methods

        public AnimationInstancingSystem(ConstructorParameter data)
        {
            this._animationMeshes = data.AnimationMeshes;

            // 最大アニメーション数分のバッファを確保
            int maxAnimationNum = this._animationMeshes.Length;
            this._sendBuffers = new SendBuffers[maxAnimationNum];
            this._animationLengthList = new NativeArray<float>(maxAnimationNum, Allocator.Persistent);

            // バッファの初期化及び各アニメーションの再生時間を保持
            int animLengthID = Shader.PropertyToID("_Length");
            for (int i = 0; i < maxAnimationNum; ++i)
            {
                this._sendBuffers[i] = new SendBuffers();
                this._animationLengthList[i] = this._animationMeshes[i].AnimationMaterial.GetFloat(animLengthID);
            }

            this._maxAnimationType = Enum.GetNames(typeof(AnimationType)).Length;
            this._playDataBufferID = Shader.PropertyToID("_PlayDataBuffer");
        }

        #endregion // Public Methods

        // ----------------------------------------------------
        #region // Protected Methods

        protected override void OnCreateManager()
        {
            this._rendererGroup = GetComponentGroup(
                ComponentType.Create<AnimationPlayData>(),
                ComponentType.Create<LocalToWorld>());
        }

        protected override void OnDestroyManager()
        {
            this.DisposeBuffers();
            foreach (var buffer in this._sendBuffers)
            {
                if (buffer.SendPlayBuffer != null) { buffer.SendPlayBuffer.Release(); }
                if (buffer.GPUInstancingArgsBuffer != null) { buffer.GPUInstancingArgsBuffer.Release(); }
            }
            this._animationLengthList.Dispose();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            this.DisposeBuffers();
            var handle = inputDeps;

            // ----------------------------------------------
            // Allocate Memory
            var groupLength = this._rendererGroup.CalculateLength();
            this._localToWorlds = new NativeArray<LocalToWorld>(groupLength, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            this._animationPlayData = new NativeArray<AnimationPlayData>(groupLength, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);


            // ----------------------------------------------
            // CopyComponentData
            handle = new CopyComponentData<LocalToWorld>
            {
                Source = this._rendererGroup.GetComponentDataArray<LocalToWorld>(),
                Results = this._localToWorlds,
            }.Schedule(groupLength, 32, handle);
            handle = new CopyComponentData<AnimationPlayData>
            {
                Source = this._rendererGroup.GetComponentDataArray<AnimationPlayData>(),
                Results = this._animationPlayData,
            }.Schedule(groupLength, 32, handle);


            // ----------------------------------------------
            // アニメーションタイプ毎に再生情報を振り分けていく
            // FIXME: 今回の実装でNativeMultiHashMapで確保しているメモリはサンプルのために適当。
            //        → ここらの仕様は最大描画数などを考慮した上で、どれくらい必要なのかすり合わせた方が良いかと思われる。
            var playDataMap = new NativeMultiHashMap<int, SendPlayData>(1000000, Allocator.TempJob);
            handle = new MapAnimationPlayDataJob
            {
                LocalToWorlds = this._localToWorlds,
                AnimationPlayData = this._animationPlayData,
                SendPlayDataMap = playDataMap.ToConcurrent(),
            }.Schedule(groupLength, 32, handle);


            // ----------------------------------------------
            // 再生情報の更新
            handle = new PlayAnimationJob
            {
                DeltaTime = Time.deltaTime,
                AnimationLengthList = this._animationLengthList,
                MaxAnimationType = this._maxAnimationType,
            }.Schedule(this, handle);
            handle.Complete();


            // ----------------------------------------------
            // GPU Instancing
            for (int i = 0; i < this._maxAnimationType; ++i)
            {
                // アニメーションタイプに応じた再生情報の取得
                var buffer = new NativeArray<SendPlayData>(groupLength, Allocator.Temp);
                SendPlayData sendPlayData; NativeMultiHashMapIterator<int> it; int instanceCount = 0;
                // ※ iの値はAnimationTypeに該当
                if (!playDataMap.TryGetFirstValue(i, out sendPlayData, out it)) { continue; }
                do
                {
                    // 同一のアニメーションが再生されているインスタンスの再生情報をbufferに確保していく。
                    buffer[instanceCount] = sendPlayData;
                    ++instanceCount;
                } while (playDataMap.TryGetNextValue(out sendPlayData, ref it));


                // Materialに対し再生するアニメーションデータなど(ComputeBuffer)を設定していく。
                var renderer = this._animationMeshes[i];
                var computeBuffers = this._sendBuffers[i];
                // 初回 or 同一のアニメーションが再生されているインスタンス数に変更があったらバッファを初期化
                if (computeBuffers.CurrentInstance <= 0 || computeBuffers.CurrentInstance != instanceCount)
                {
                    if (computeBuffers.SendPlayBuffer != null) { computeBuffers.SendPlayBuffer.Release(); }
                    computeBuffers.SendPlayBuffer = new ComputeBuffer(instanceCount, Marshal.SizeOf(typeof(SendPlayData)));
                    if (computeBuffers.GPUInstancingArgsBuffer != null) { computeBuffers.GPUInstancingArgsBuffer.Release(); }
                    computeBuffers.GPUInstancingArgsBuffer = new ComputeBuffer(1, this._GPUInstancingArgs.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
                    computeBuffers.CurrentInstance = instanceCount;
                }

                // 再生情報の設定
                // HACK: もうちょっと効率的なやり方がありそう感
                computeBuffers.SendPlayBuffer.SetData(buffer.Slice(0, instanceCount).ToArray());
                renderer.AnimationMaterial.SetBuffer(this._playDataBufferID, computeBuffers.SendPlayBuffer);

                // 「Graphics.DrawMeshInstancedIndirect -> bufferWithArgs」の設定
                this._GPUInstancingArgs[0] = (uint)renderer.Mesh.GetIndexCount(0);
                this._GPUInstancingArgs[1] = (uint)instanceCount;
                this._GPUInstancingArgs[2] = (uint)renderer.Mesh.GetIndexStart(0);
                this._GPUInstancingArgs[3] = (uint)renderer.Mesh.GetBaseVertex(0);
                computeBuffers.GPUInstancingArgsBuffer.SetData(this._GPUInstancingArgs);

                // 描画
                Graphics.DrawMeshInstancedIndirect(
                    renderer.Mesh,
                    0,
                    renderer.AnimationMaterial,
                    new Bounds(Vector3.zero, 1000000 * Vector3.one),
                    computeBuffers.GPUInstancingArgsBuffer);

                buffer.Dispose();
            }
            playDataMap.Dispose();
            return handle;
        }

        #endregion // Protected Methods

        // ----------------------------------------------------
        #region // Private Methods

        void DisposeBuffers()
        {
            if (this._localToWorlds.IsCreated) { this._localToWorlds.Dispose(); }
            if (this._animationPlayData.IsCreated) { this._animationPlayData.Dispose(); }
        }

        #endregion // Private Methods
    }
}
