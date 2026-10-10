using System;
using UnityEngine;
using Spine.Unity;

public static class AnimationValidation {
    public static void Run() {
        var models=JsonUtility.FromJson<BenchmarkModels>(Resources.Load<TextAsset>("models").text).models;
        foreach(var model in models) {
            if(string.IsNullOrEmpty(model.skill) || model.skill==model.idle || model.skill==model.attack) continue;
            var skeleton=Resources.Load<TextAsset>(model.path+"/skeleton");
            skeleton.name="skeleton.skel";
            var material=new Material(Shader.Find("Spine/Skeleton"));
            var atlas=SpineAtlasAsset.CreateRuntimeInstance(Resources.Load<TextAsset>(model.path+"/atlas"),Resources.LoadAll<Texture2D>(model.path),material,true);
            var data=SkeletonDataAsset.CreateRuntimeInstance(skeleton,atlas,true,.003125f);
            var actor=SkeletonAnimation.NewSkeletonAnimationGameObject(data);
            try {
                var driver=new UnitAnimationDriver(actor,model,false);
                Require(driver.Skill(),"Skill starts");
                var track=actor.AnimationState.GetCurrent(0);
                Require(!driver.Attack(),"Attack cannot interrupt skill");
                Require(!driver.Skill(),"Repeated skill cannot restart current skill");
                actor.AnimationState.Update(track.Animation.Duration+.01f);
                actor.AnimationState.Apply(actor.Skeleton);
                actor.AnimationState.Update(.02f);
                actor.AnimationState.Apply(actor.Skeleton);
                Require(actor.AnimationState.GetCurrent(0).Animation.Name==model.idle,"Recovery uses animation clock");
                Require(driver.Attack(),"Attack allowed after skill completion");
                driver.Die("__missing_death_clip__");
                Require(!driver.Attack() && !driver.Skill() && actor.timeScale==0,"Dead units cannot restart attacks");
                Debug.Log("ANIMATION VALIDATION PASSED: "+model.id);
                return;
            } finally {
                UnityEngine.Object.DestroyImmediate(actor.gameObject);
                UnityEngine.Object.DestroyImmediate(data);
                UnityEngine.Object.DestroyImmediate(atlas);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }
        throw new Exception("No distinct skill model available for animation validation");
    }
    static void Require(bool value,string message) {if(!value)throw new Exception(message);}
}
