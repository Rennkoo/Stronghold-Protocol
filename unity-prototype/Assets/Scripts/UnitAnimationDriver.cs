using Spine;
using Spine.Unity;

// Queue recovery on Spine's clock: frame stalls and playback speed cannot end a clip early.
public sealed class UnitAnimationDriver {
    readonly SkeletonAnimation actor;
    readonly Animation idle, move, attack, skill;
    readonly bool moving;
    TrackEntry protectedSkill;
    bool dead;

    public UnitAnimationDriver(SkeletonAnimation actor, BenchmarkModel model, bool moving) {
        this.actor=actor; this.moving=moving;
        idle=Find(model.idle); move=Find(model.move) ?? idle;
        attack=Find(model.attack); skill=Find(model.skill);
    }
    Animation Find(string name) {
        return string.IsNullOrEmpty(name) ? null : actor.Skeleton.Data.FindAnimation(name);
    }
    public bool Attack() {
        if(dead || (protectedSkill!=null && !protectedSkill.IsComplete)) return false;
        return PlayOnce(attack);
    }
    public bool Skill() {
        if(dead || (protectedSkill!=null && !protectedSkill.IsComplete) || skill==null) return false;
        if(!PlayOnce(skill)) return false;
        protectedSkill=actor.AnimationState.GetCurrent(0);
        return true;
    }
    bool PlayOnce(Animation animation) {
        if(animation==null) return false;
        actor.AnimationState.SetAnimation(0,animation,false);
        var recovery=moving ? move : idle;
        if(recovery!=null) actor.AnimationState.AddAnimation(0,recovery,true,0);
        return true;
    }
    public void Die(string clip) {
        dead=true; protectedSkill=null;
        actor.AnimationState.ClearTracks();
        var animation=Find(clip);
        if(animation!=null) actor.AnimationState.SetAnimation(0,animation,false);
        else actor.timeScale=0;
    }
}
