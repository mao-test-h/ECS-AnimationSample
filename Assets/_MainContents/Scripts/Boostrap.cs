namespace MainContents
{
    using UnityEngine;

    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Transforms;

    using MainContents.ECS;

    using UnityRandom = UnityEngine.Random;
    using AnimationType = MainContents.ECS.AnimationInstancingSystem.AnimationType;

    public sealed class Boostrap : MonoBehaviour
    {
        // ------------------------------
        #region // Private Members(Editable)

#pragma warning disable 0649

        /// <summary>
        /// 最大Entity数
        /// </summary>
        [SerializeField] int _maxObjectNum;

        /// <summary>
        /// 再生アニメーションデータ
        /// </summary>
        [SerializeField] AnimationInstancingSystem.AnimationMesh[] _animationMeshes;

#pragma warning restore 0649

        #endregion // Private Members(Editable)


        // ----------------------------------------------------
        #region // Unity Events

        /// <summary>
        /// MonoBehaviour.Start
        /// </summary>
        void Start()
        {
            World.Active = new World("Sample World");
            var entityManager = World.Active.CreateManager<EntityManager>();
            World.Active.CreateManager(typeof(EndFrameTransformSystem));
            World.Active.CreateManager(typeof(AnimationInstancingSystem), new AnimationInstancingSystem.Parameter { AnimationMeshes = this._animationMeshes });
            ScriptBehaviourUpdateOrder.UpdatePlayerLoop(World.Active);

            var archetype = entityManager.CreateArchetype(
                ComponentType.Create<PlayData>(),
                ComponentType.Create<Prefab>(),
                ComponentType.Create<Position>(),
                ComponentType.Create<Rotation>(),
                ComponentType.Create<LocalToWorld>());

            // create prefab entity
            var prefabEntity = entityManager.CreateEntity(archetype);
            for (int i = 0; i < this._maxObjectNum; ++i)
            {
                var entity = entityManager.Instantiate(prefabEntity);
                //entityManager.SetComponentData(entity, new Position { Value = new float3(i * 3f, 0f, 0f) });
                entityManager.SetComponentData(entity, new Position { Value = this.GetRandomPosition() });
                entityManager.SetComponentData(entity, new Rotation { Value = Quaternion.Euler(-90f, 0, 0) });
                entityManager.SetComponentData(entity, new PlayData { CurrentKeyFrame = 0, AnimationType = (AnimationType)(i % 3) });
            }
        }

        /// <summary>
        /// MonoBehaviour.OnDestroy
        /// </summary>
        void OnDestroy()
        {
            World.DisposeAllWorlds();
        }

        /// <summary>
        /// ランダムな位置の取得
        /// </summary>
        float3 GetRandomPosition()
        {
            float3 boundSize = new float3(64f);
            var halfX = boundSize.x / 2;
            var halfY = boundSize.y / 2;
            var halfZ = boundSize.z / 2;
            return new float3(
                UnityRandom.Range(-halfX, halfX),
                UnityRandom.Range(-halfY, halfY),
                UnityRandom.Range(-halfZ, halfZ));
        }

        #endregion // Unity Events
    }
}
