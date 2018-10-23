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

    // ------------------------------
    #region // ComponentData

    /// <summary>
    /// 再生情報
    /// </summary>
    public struct PlayData : IComponentData
    {
        /// <summary>
        /// 現在の再生キーフレーム
        /// </summary>
        public float CurrentKeyFrame;

        /// <summary>
        /// アニメーションタイプ
        /// </summary>
        public AnimationInstancingSystem.AnimationType AnimationType;
    }

    #endregion // ComponentData


    /// <summary>
    /// AnimationInstancing System
    /// </summary>
    [ExecuteInEditMode]
    [UpdateAfter(typeof(EndFrameTransformSystem))]
    public class AnimationInstancingSystem : JobComponentSystem
    {
        // ------------------------------
        #region // Defines

        /// <summary>
        /// アニメーションタイプ
        /// </summary>
        public enum AnimationType
        {
            Run = 0,
            Slide,
            Wait,
        }

        /// <summary>
        /// コンストラクタ引数
        /// </summary>
        public class Parameter
        {
            public AnimationMesh[] AnimationMeshes;
        }

        /// <summary>
        /// 再生アニメーションデータ
        /// </summary>
        [Serializable]
        public class AnimationMesh
        {
            public Mesh Mesh;
            public Material AnimationMaterial;
        }

        /// <summary>
        /// Shaderに渡す情報
        /// </summary>
        class ComputeBuffers
        {
            public ComputeBuffer SendBuffer = null;
            public ComputeBuffer GPUInstancingArgsBuffer = null;
            public int CurrentInstance = -1;
        }

        /// <summary>
        /// Shaderに渡す再生情報
        /// </summary>
        struct SendData
        {
            /// <summary>
            /// 現在の再生キーフレーム
            /// </summary>
            public float CurrentKeyFrame;

            /// <summary>
            /// 座標(Shaderに渡す用)
            /// </summary>
            public float4x4 LocalToWorld;
        }

        #endregion // Defines

        // ------------------------------
        #region // Jobs

        /// <summary>
        /// アニメーションタイプごとに再生情報を振り分けるJob
        /// </summary>
        [BurstCompile]
        struct MapPlayDataJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<PlayData> PlayData;
            [ReadOnly] public NativeArray<LocalToWorld> LocalToWorlds;
            public NativeMultiHashMap<int, SendData>.Concurrent PlayDataMap;

            public void Execute(int index)
            {
                var playData = PlayData[index];
                var data = new SendData
                {
                    CurrentKeyFrame = playData.CurrentKeyFrame,
                    LocalToWorld = LocalToWorlds[index].Value,
                };
                PlayDataMap.Add((int)playData.AnimationType, data);
            }
        };

        /// <summary>
        /// アニメーション再生ジョブ
        /// </summary>
        [BurstCompile]
        struct PlayAnimationJob : IJobProcessComponentData<PlayData>
        {
            [ReadOnly] public float DeltaTime;
            [ReadOnly] public NativeArray<float> AnimationLengthList;
            [ReadOnly] public int MaxAnimationType;
            public void Execute(ref PlayData data)
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

                // TIPS. 今回の実装としては単純に上記の通りだが、
                // 例えば中で他のステータスを見るなりしてアニメーションを状況に応じて切り替えたり、
                // 再生を停止すると言ったことも出来るかと思われる。
            }
        }

        #endregion // Jobs

        // ------------------------------
        #region // Private Fields

        ComponentGroup _rendererGroup;

        // DrawMeshInstancedIndirectに渡す情報
        uint[] _GPUInstancingArgs = new uint[5] { 0, 0, 0, 0, 0 };

        // 再生アニメーションデータ
        AnimationMesh[] _animationMeshes;
        // Shaderに渡す情報
        ComputeBuffers[] _computeBuffers;

        // アニメーションの再生時間を保持
        NativeArray<float> _animationLengthList;

        // ComponentDataArray コピー用
        NativeArray<LocalToWorld> _localToWorlds;
        NativeArray<PlayData> _playData;

        int _maxAnimationType;
        int _playDataBufferID;

        #endregion // Private Fields


        // ----------------------------------------------------
        #region // Public Methods

        public AnimationInstancingSystem(Parameter data)
        {
            this._animationMeshes = data.AnimationMeshes;

            int animLengthID = Shader.PropertyToID("_Length");
            int maxAnimationNum = this._animationMeshes.Length;
            this._computeBuffers = new ComputeBuffers[maxAnimationNum];
            this._animationLengthList = new NativeArray<float>(maxAnimationNum, Allocator.Persistent);
            for (int i = 0; i < maxAnimationNum; ++i)
            {
                this._computeBuffers[i] = new ComputeBuffers();
                this._animationLengthList[i] = this._animationMeshes[i].AnimationMaterial.GetFloat(animLengthID);
            }

            this._maxAnimationType = Enum.GetNames(typeof(AnimationType)).Length;
            this._playDataBufferID = Shader.PropertyToID("_PlayDataBuffer");
        }

        #endregion // Public Methods

        // ----------------------------------------------------
        #region // Protected Methods

        protected override void OnCreateManager() => this._rendererGroup = GetComponentGroup(ComponentType.Create<PlayData>(), ComponentType.Create<LocalToWorld>());

        protected override void OnDestroyManager()
        {
            this.DisposeBuffers();
            foreach (var buffer in this._computeBuffers)
            {
                if (buffer.SendBuffer != null) { buffer.SendBuffer.Release(); }
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
            this._playData = new NativeArray<PlayData>(groupLength, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            var playDataMap = new NativeMultiHashMap<int, SendData>(100000, Allocator.TempJob);

            // ----------------------------------------------
            handle = new CopyComponentData<LocalToWorld>
            {
                Source = this._rendererGroup.GetComponentDataArray<LocalToWorld>(),
                Results = this._localToWorlds,
            }.Schedule(groupLength, 32, handle);

            handle = new CopyComponentData<PlayData>
            {
                Source = this._rendererGroup.GetComponentDataArray<PlayData>(),
                Results = this._playData,
            }.Schedule(groupLength, 32, handle);

            // ----------------------------------------------
            handle = new MapPlayDataJob
            {
                LocalToWorlds = this._localToWorlds,
                PlayData = this._playData,
                PlayDataMap = playDataMap.ToConcurrent(),
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
                var buffer = new NativeArray<SendData>(groupLength, Allocator.Temp);
                SendData data; NativeMultiHashMapIterator<int> it; int animLength = 0;
                if (!playDataMap.TryGetFirstValue(i, out data, out it)) { continue; }
                do
                {
                    buffer[animLength] = data;
                    ++animLength;
                } while (playDataMap.TryGetNextValue(out data, ref it));

                // 再生アニメーションデータの設定
                var renderer = this._animationMeshes[i];
                var computeBuffers = this._computeBuffers[i];
                if (computeBuffers.CurrentInstance <= 0 || computeBuffers.CurrentInstance != animLength)
                {
                    // 初回 or アニメーションタイプに応じた再生情報の要素数に変更があったらバッファを初期化
                    if (computeBuffers.SendBuffer != null) { computeBuffers.SendBuffer.Release(); }
                    computeBuffers.SendBuffer = new ComputeBuffer(animLength, Marshal.SizeOf(typeof(SendData)));
                    if (computeBuffers.GPUInstancingArgsBuffer != null) { computeBuffers.GPUInstancingArgsBuffer.Release(); }
                    computeBuffers.GPUInstancingArgsBuffer = new ComputeBuffer(1, this._GPUInstancingArgs.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
                    computeBuffers.CurrentInstance = animLength;
                }

                // 再生情報 HACK: もうちょっと効率的なやり方がありそう感
                computeBuffers.SendBuffer.SetData(buffer.Slice(0, animLength).ToArray());
                renderer.AnimationMaterial.SetBuffer(this._playDataBufferID, computeBuffers.SendBuffer);

                // メッシュ情報とか
                this._GPUInstancingArgs[0] = (uint)renderer.Mesh.GetIndexCount(0);
                this._GPUInstancingArgs[1] = (uint)animLength;
                this._GPUInstancingArgs[2] = (uint)renderer.Mesh.GetIndexStart(0);
                this._GPUInstancingArgs[3] = (uint)renderer.Mesh.GetBaseVertex(0);
                computeBuffers.GPUInstancingArgsBuffer.SetData(this._GPUInstancingArgs);

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
            if (this._playData.IsCreated) { this._playData.Dispose(); }
        }

        #endregion // Private Methods
    }
}
