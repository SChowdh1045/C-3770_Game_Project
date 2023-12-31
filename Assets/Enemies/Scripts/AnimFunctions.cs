using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimFunctions : MonoBehaviour
{
    public Enemy enemy;
    public Animator anim;
    public EnemiesDead enemyDead;

    public void Awake()
    {
        enemy = GetComponentInParent<Enemy>();
        anim = GetComponent<Animator>();
        enemyDead = GameObject.FindGameObjectWithTag("EnemiesDead").GetComponent<EnemiesDead>();
    }

    public void OnAttack()
    {
        Debug.Log(enemy.CheckDirection() - 1);
        List<GameObject> entities = enemy.hitboxes[enemy.CheckDirection()-1].hitEnimies;

        enemy.EnemyAttack(entities);
    }

    public void OnWalkSound()
    {
        enemy.enemySM.CurrentEnemyState.AnimationTriggerEvent(enemy.WalkSound);
    }

    public void OnAttackSound()
    {
        enemy.enemySM.CurrentEnemyState.AnimationTriggerEvent(enemy.AttackSound);
    }

    public void OnDead()
    {
        anim.speed = 0f;
        enemy.IsDead = true;
        enemyDead.enemies.Remove(enemy);
    }

    public void CheckAttack()
    {
        if (!enemy.IsStrike)
        {
            enemy.enemySM.ChangeState(enemy.ChaseState);
        }
    }

    public void RangedAttack()
    {
        enemy.enemySM.CurrentEnemyState.RangedAttack(enemy.Projectile);
    }

    public void OnExplode()
    {
        Destroy(this.gameObject);
    }

    public void CheckIsReady()
    {
        ShadowImp_Ability shadowAb = GetComponentInParent<ShadowImp_Ability>();
        if(shadowAb.cooldown <= 0f)
            shadowAb.isReady = true;
    }

    public void CheckHeavyAttack()
    {
        enemy.ability.OnAbility();
    }
}
