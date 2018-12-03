namespace MainContents
{
    using UnityEngine;

    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Transforms;

    using MainContents.ECS;

    using UnityRandom = UnityEngine.Random;
    using ConstructorParameter = MainContents.ECS.AnimationInstancingSystem.ConstructorParameter;

    public sealed class Bootstrap : MonoBehaviour
    {
        // ------------------------------
        #region // Private Members(Editable)

        /// <summary>
        /// 最大Entity数
        /// </summary>
        [SerializeField] int _maxObjectNum = 10000;

        /// <summary>
        /// 再生アニメーションデータ
        /// </summary>
        [SerializeField] AnimationMesh[] _animationMeshes = null;

        /// <summary>
        /// ランダムな位置取得時の表示領域
        /// </summary>
        [SerializeField] float _randomBoundSize = 64f;

        #endregion // Private Members(Editable)


        // ----------------------------------------------------
        #region // Unity Events

        /// <summary>
        /// MonoBehaviour.Start
        /// </summary>
        void Start()
        {
            // World Settings
            World.Active = new World("Sample World");
            var entityManager = World.Active.CreateManager<EntityManager>();
            World.Active.CreateManager(typeof(EndFrameTransformSystem));
            World.Active.CreateManager(typeof(AnimationInstancingSystem), new ConstructorParameter { AnimationMeshes = this._animationMeshes });
            ScriptBehaviourUpdateOrder.UpdatePlayerLoop(World.Active);

            // Create Archetypes
            var archetype = entityManager.CreateArchetype(
                // アニメーションの再生情報
                ComponentType.Create<AnimationPlayData>(),
                // Prefab Entity
                ComponentType.Create<Prefab>(),
                // Transforms
                ComponentType.Create<Position>(), ComponentType.Create<Rotation>(), ComponentType.Create<LocalToWorld>());

            // Create Prefab Entity
            var prefabEntity = entityManager.CreateEntity(archetype);
            for (int i = 0; i < this._maxObjectNum; ++i)
            {
                var entity = entityManager.Instantiate(prefabEntity);

                // 座標の初期設定
                entityManager.SetComponentData(
                    entity,
                    new Position
                    {
                        Value = this.GetRandomPosition()
                    });
                entityManager.SetComponentData(
                    entity,
                    new Rotation
                    {
                        Value = Quaternion.Euler(-90f, 0, 0)
                    });

                // アニメーション再生情報の設定
                // → AnimationTypeはとりあえずは順番に割り当てておく
                entityManager.SetComponentData(
                    entity,
                    new AnimationPlayData
                    {
                        CurrentKeyFrame = 0,
                        AnimationType = (AnimationType)(i % 3)
                    });
            }
        }

        /// <summary>
        /// MonoBehaviour.OnDestroy
        /// </summary>
        void OnDestroy()
        {
            World.DisposeAllWorlds();
        }

        #endregion // Unity Events

        // ----------------------------------------------------
        #region // Private Methods

        /// <summary>
        /// ランダムな位置の取得
        /// </summary>
        float3 GetRandomPosition()
        {
            float3 boundSize = new float3(this._randomBoundSize);
            var halfX = boundSize.x / 2;
            var halfY = boundSize.y / 2;
            var halfZ = boundSize.z / 2;
            return new float3(
                UnityRandom.Range(-halfX, halfX),
                UnityRandom.Range(-halfY, halfY),
                UnityRandom.Range(-halfZ, halfZ));
        }

        #endregion // Private Methods
    }
}
