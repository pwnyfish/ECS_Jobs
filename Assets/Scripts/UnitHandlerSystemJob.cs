using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Rendering;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Physics;
using Unity.Jobs;

public class UnitHandlerJobSystem: JobComponentSystem
{
    public GameObject targetObject = null;
    TargetMove leader;
    float separationBuildUp;


    private struct EntityWithPosition
    {
        public Entity entity;
        public float3 position;
        public PhysicsVelocity velocity;
    }

    protected override void OnCreate()
    {
        leader = GameObject.FindObjectOfType(typeof(TargetMove)) as TargetMove;
    }
    [RequireComponentTag(typeof(Skeleton))]
    [BurstCompile]
    private struct UnitHandlerJob : IJobForEachWithEntity<Translation,PhysicsVelocity>
    {
        
        [DeallocateOnJobCompletion][ReadOnly] public NativeArray<EntityWithPosition> targetArray;
        public EntityCommandBuffer.Concurrent entityCommandBuffer;
        public Vector3 tempTarget;
        public float separationBuildUp;
        public float arrivalRadius;
        public bool leaderMoving;
        float unitSize;
        float unitSpace;
        float allUnitsSpaces;
        float arrivalSpace;

        public void Execute(Entity entity, int index, [ReadOnly] ref Translation skeletonTranslation,ref PhysicsVelocity velocity)
        {
            Skeleton unit;
            float slowingDistance = 10;
            float moveSpeed = 8;
            float separation_radius_min = 5;
            float separation_radius_max = 10;
            int areaUnitsCount = 0;
            
            float3 sepVelocity = new float3(0, 0, 0);
            float3 alignVelocity = new float3(0, 0, 0);
            float3 arriveVelocity = new float3(0, 0, 0);
            float3 velocitySum = new float3(0, 0, 0);

            //Code to run on all entities with the "Skeleton" tag
            NativeList<Entity> closestTargetsEntityList = new NativeList<Entity>(Allocator.Temp);
            NativeList<float3> closestTargetsPositionList = new NativeList<float3>(Allocator.Temp);
            NativeList<Vector3> closestTargetsVelocityList = new NativeList<Vector3>(Allocator.Temp);
            float3 skeletonPosition = skeletonTranslation.Value;
            float3 closestTargetPosition = float3.zero;

            float velocity_magnitude = Vector3.Magnitude(velocity.Linear) / moveSpeed;
            float separation_radius = math.lerp(separation_radius_min, separation_radius_max, velocity_magnitude);
            unit.unitStatus = UnitStatus.Idle;

            for (int i=0; i < targetArray.Length; i++)
            {
                EntityWithPosition targetEntity = targetArray[i];

            //Cycle through all other skeletons units to find the ones in neighbour distance

                if (entity != null)
                {
                    if (entity != targetEntity.entity)
                    {
                        if (math.lengthsq(targetEntity.position - skeletonPosition) < (separation_radius * separation_radius))
                        {
                            closestTargetsEntityList.Add(targetEntity.entity);
                            closestTargetsPositionList.Add(targetEntity.position);
                            closestTargetsVelocityList.Add(targetEntity.velocity.Angular);
                        }
                    }
                }
            }

            sepVelocity = doSeparation(closestTargetsEntityList, closestTargetsPositionList, skeletonPosition, moveSpeed, velocity.Linear);

            arriveVelocity = doArrival(tempTarget, skeletonPosition, moveSpeed, velocity.Linear);
            velocitySum = (sepVelocity + arriveVelocity + alignVelocity) * new float3(1, 0, 1);

            if (!(velocitySum.Equals(float3.zero)))
            {

                velocity.Linear += math.normalize(velocitySum);
            }


            closestTargetsEntityList.Dispose();
            closestTargetsPositionList.Dispose();
            closestTargetsVelocityList.Dispose();
        }

        public Vector3 doSeparation(NativeList<Entity> closestTargetsEntityList, NativeList<float3> closestTargetsPositionList, float3 skeletonPosition, float speed, Vector3 velocity)
        {
            
            Vector3 desiredSepVelocity = Vector3.zero;
            float distance = 3;

            //Calc separation force
            Vector3 separationForce = Vector3.zero;
            Vector3 steeringForce;
            Vector3 sepForce = Vector3.zero;
            if (closestTargetsEntityList.Length != 0)
            {

                for (int i = 0; i < closestTargetsEntityList.Length; i++)
                {
                    //Debug.Log(closestTargetsEntityList.Length);
                    sepForce = skeletonPosition - closestTargetsPositionList[i];
                    sepForce *= 1 - Mathf.Min(sepForce.sqrMagnitude / (distance * distance), 1);
                    separationForce += sepForce;
                }
            }
            else
            {
                return Vector3.zero;
            }

            separationForce /= closestTargetsEntityList.Length;
            desiredSepVelocity = separationForce.normalized * speed;
            steeringForce = desiredSepVelocity - velocity;
            return steeringForce;
        }





        public Vector3 doArrival(Vector3 targetPosition, Vector3 position, float speed, Vector3 velocity)
        {
            float slowingDistance = 10;
            Vector3 desiredArriveVelocity = Vector3.zero;


            // Calculate stopping factor
            float targetDistance = (targetPosition - position).magnitude;
            float stoppingFactor;

            if (slowingDistance > 0)
            {
                stoppingFactor = Mathf.Clamp(targetDistance / slowingDistance, 0.0f, 1.0f);
            }
            else
            {
                stoppingFactor = Mathf.Clamp(targetDistance, 0.0f, 1.0f);
            }

            desiredArriveVelocity = (targetPosition - position).normalized * speed * stoppingFactor;

            // Calculate steering force
            Vector3 steeringForce = desiredArriveVelocity - velocity;
            return steeringForce;
        }


    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        Vector3 tempTarget = new Vector3(0, 0, 0);

        EntityQuery targetQuery = GetEntityQuery(typeof(Skeleton), ComponentType.ReadOnly<Translation>());
        NativeArray<Entity> targetEntityArray = targetQuery.ToEntityArray(Allocator.TempJob);
        NativeArray<Translation> targetTranslationArray = targetQuery.ToComponentDataArray<Translation>(Allocator.TempJob);
        NativeArray<EntityWithPosition> targetArray = new NativeArray<EntityWithPosition>(targetEntityArray.Length, Allocator.TempJob);

        tempTarget = leader.transform.position;

        for (int i=0; i < targetEntityArray.Length; i++)
        {
            targetArray[i] = new EntityWithPosition
            {
                entity = targetEntityArray[i],
                position = targetTranslationArray[i].Value
            };
        }
        targetEntityArray.Dispose();
        targetTranslationArray.Dispose();

        UnitHandlerJob unitHandlerJob = new UnitHandlerJob
        {
            targetArray = targetArray,
            tempTarget = tempTarget,
            leaderMoving = leader.moving,
        };

        JobHandle jobHandle = unitHandlerJob.Schedule(this, inputDeps);

        return jobHandle;
    }
}