using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class L1BossMovement : Entity
{
    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private Animator a;

    public GameObject mainCamera;

    [SerializeField] private CircleCollider2D triggerCircle;
    [SerializeField] private AnimationClip[] anim; // Some of the boss animations. Using it to access their lengths
    [SerializeField] private Transform MC;
    private Transform pulseCircle; // first circle in buff effect
    private Transform donutCircle; // second circle in buff effect
    [SerializeField] private Slider healthBar;
    [SerializeField] private Rigidbody2D Coin;
    [SerializeField] private int value;
    private Material DeathMaterial;

    #region Prefabs
    [SerializeField] GameObject sawPrefab;
    [SerializeField] GameObject landingSmokePrefab;
    [SerializeField] GameObject powerUpPrefab;
    #endregion

    #region Audio
    [SerializeField] AudioSource startJump;
    [SerializeField] AudioSource jumpLanding;
    [SerializeField] AudioSource swordSwing;
    [SerializeField] AudioSource powerUp;
    [SerializeField] AudioSource powerUpPulse;
    [SerializeField] AudioSource deathAudio;
    #endregion

    #region Animation states & bools
    private enum States { idle, walk, attack, jump, fear };
    
    // for animation control
    private bool idle = true;
    private bool walk = false;

    private bool attack = false; // Used to control animation states
    private bool attackInProgress = false; // Used as a flag in case of repeated sword attacks by the boss
    private float damage = 1f;

    private bool jump = false;
    private bool activateBuff = false;
    private bool buffRunning = false;
    private bool dead = false;
    #endregion

    #region For the jump animation
    private Vector2 snapshotMCPosition; // snapshot of MC's position during boss' jump
    private Vector2 jumpStartPosition; // boss' position before beginning jump
    private float jumpHeight = 5f; // Adjust the jump height as needed
    private float jumpDuration = 1.2f; // Adjust the duration of the jump as needed
    #endregion

    public SceneSwitch sceneSwitch;


    // Start is called before the first frame update
    protected override void Start()
    {
        sceneSwitch.canLeave = false;
        base.Start(); // Simply sets "CurrentHealth = maxHealth;"

        DeathMaterial = GetComponent<SpriteRenderer>().material;

        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        a = GetComponent<Animator>();

        pulseCircle = transform.Find("Pulse");
        donutCircle = transform.Find("Donut");

        StartCoroutine(follow_MC());
    }

    // Update is called once per frame
    protected override void Update()
    {
        if(MC != null)
        {
            // used for my Blend Tree
            Vector2 dir = MC.position - transform.position;
            dir.Normalize();
            a.SetFloat("dirX", dir.x);
            a.SetFloat("dirY", dir.y);

            if (idle && !attack)
            {
                a.SetInteger("state", (int)States.idle);
            }

            float movementSpeed = buffRunning ? 5.5f : 3.5f;
            if (!idle && !jump && !attack && !activateBuff && !dead)
            {
                transform.position = Vector2.MoveTowards(transform.position, MC.position, movementSpeed * Time.deltaTime);
            }
        }

        // If player dies, just go idle
        else
        {
            a.SetInteger("state", (int)States.idle);
            StopAllCoroutines();
        }

        // Boss death
        if (GetHealth() <= 0 && !dead)
        {
            StartCoroutine(BossDeath());
        }
    }

    private IEnumerator follow_MC()
    {
        while (true)
        {
            // Stay idle 0.2 seconds
            yield return new WaitForSeconds(0.2f);
            idle = false; // start walking

            walk = true;
            
            // walk animation gets activated after attack animation ends
            if (!attack)
            {
                a.SetInteger("state", (int)States.walk);
            }
            yield return new WaitForSeconds(Random.Range(2.5f, 5.5f)); // follow player for some time
            walk = false;

            // after walking for some time, do jump ability (if boss isn't dead)
            if (!dead && MC != null) { StartCoroutine(jumpFunc()); }

            // Control comes back here after the first 'yield' call runs in jumpFunc() coroutine.
            // This is why when the attack animation happened right before the jump animation, the attack animation triggered and finishes during the jump animation duration, and since after that attack=false and idle=true, the idle animation happens while jumping. This is why I added a 1s exit time duration from attack -> jump animations to fix this bug.
            yield return new WaitForSeconds((anim[2].length * 2) + jumpDuration);

            // while the power up is running, I don't want to spawn saws in. Also, saws have a 55% chance of being spawned
            if (!buffRunning && MC != null && !dead)
            {
                if (Random.Range(0f, 1f) <= 0.55f)
                {
                    loadSaws();
                }
                else
                {
                    buffRunning = true;
                    StartCoroutine(fearFunc());
                }
            }
            idle = true;
        }
    }

    #region Attack Logic

    // Same steps as 'OnTriggerEnter2D()'
    private void OnTriggerStay2D(Collider2D collision)
    {
        OnTriggerEnter2D(collision);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player") && !jump)
        {
            if (!attackInProgress)
            {
                attack = true; // Used to control animation states
                attackInProgress = true; // Used as a flag in case of repeated sword attacks by the boss
                a.SetInteger("state", (int)States.attack);

                StartCoroutine(attackFunc());
            }
        }
    }

    private IEnumerator attackFunc()
    {
        yield return new WaitForSeconds(anim[1].length);
        attackInProgress = false; // Reset the attack flag to let the next attack audio & animation play (if any)
    }

    // used by event trigger in animation window
    private void playSwordSwing()
    {
        swordSwing.Play();
        isPlayerHit();
    }

    private void isPlayerHit()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(triggerCircle.transform.position, triggerCircle.radius + 0.5f);

        foreach (Collider2D col in colliders)
        {
            if (col.gameObject.CompareTag("Player"))
            {
                col.gameObject.GetComponent<Player>().TakeDamage(damage);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            StartCoroutine(LetAttackAnimationFinish());
        }
    }
    private IEnumerator LetAttackAnimationFinish()
    {
        // if player quickly enters and exits boss' box collider, it first triggers 'OnTriggerEnter2D()' which plays the attack animation, but I have to add this delay when exiting or else the attack animation would instantly get interrupted by the walk/idle animation
        yield return new WaitForSeconds(anim[1].length);
        attack = false;

        // If the boss was walking before the attack, transition back to walking animation
        if (walk)
        {
            a.SetInteger("state", (int)States.walk);
        }
    }
    #endregion

    private IEnumerator fearFunc()
    {
        activateBuff = true;
        damage = 2f;
        a.SetInteger("state", (int)States.fear);
        loadPowerUp(); // power up animation
        powerUp.Play();
        yield return new WaitForSeconds(anim[3].length);
        StartCoroutine(ExpandAndContractCircles(pulseCircle, donutCircle));
        activateBuff = false;
    }

    private void loadPowerUp()
    {
        Vector3 powerUpOffset = new Vector3(-0.26f, 0.18f, 0);
        GameObject powerUp = Instantiate(powerUpPrefab, transform.position + powerUpOffset, Quaternion.identity);
        powerUp.transform.SetParent(transform);
        Destroy(powerUp, anim[5].length);
    }

    private IEnumerator ExpandAndContractCircles(Transform pulseC, Transform donutC)
    {
        float circleExpansionDuration = 0.12f;
        float pulseEffectDuration = 15f;
        float pulseStartTime = Time.time;
        Vector3 initialScale = Vector3.zero;
        Vector3 maxScale = new Vector3(10f, 10f, 0);
        Transform currentCircle = pulseC;
        pulseCircle.GetComponent<SpriteRenderer>().enabled = true;
        donutCircle.GetComponent<SpriteRenderer>().enabled = true;

        powerUpPulse.Play();

        // Run for 15 seconds ; also needed to add '&& !dead' or else the pulsating effect would keep going even after boss died
        while ((Time.time - pulseStartTime < pulseEffectDuration) && !dead ) 
        {
            // Gradually expand the current circle
            float startTime = Time.time;
            float elapsedTime = 0f;
            while (elapsedTime < circleExpansionDuration)
            {
                float t = elapsedTime / circleExpansionDuration;
                currentCircle.localScale = Vector3.Lerp(initialScale, maxScale, t);
                elapsedTime = Time.time - startTime;
                yield return null;
            }
            currentCircle.localScale = maxScale;

            // Switch to the other circle
            currentCircle = (currentCircle == pulseC) ? donutC : pulseC;

            // Instantly contract the circle we just switched to
            currentCircle.localScale = initialScale;

            // Set the sorting layer to make the circle we just switched to, render above
            SetSortingLayerOrder(currentCircle, 2);

            // Set the sorting layer to make the other circle render below
            Transform otherCircle = (currentCircle == donutC) ? pulseC : donutC;
            SetSortingLayerOrder(otherCircle, 1);
        }

        buffRunning = false;
        damage = 1f;
        powerUpPulse.Stop();
        pulseCircle.GetComponent<SpriteRenderer>().enabled = false;
        donutCircle.GetComponent<SpriteRenderer>().enabled = false;
    }

    private void SetSortingLayerOrder(Transform circle, int sortingOrder)
    {
        circle.GetComponent<SpriteRenderer>().sortingOrder = sortingOrder;
    }


    private IEnumerator jumpFunc()
    {
        // For the boss' jump animation, I want to first take a snapshot of the MC's position before jumping. I also need the jump duration as well as the elapsed time of the jump.
        // To make a parabolic jump from the boss' position to the snapshot of MC's position, I have to use a combination of the sine function and linear interpolation.
        // I'll explain each line and what its function is in the jump animation (I've commented the line #s)
        // line 1: Need a while loop to check if the time elapsed for the jump animation exceeds the jump duration (that I set)
        // line 2: Divide timeElapsed by jumpDuration, which will give me a percentage of how much of the jump duration has elapsed (will use this for jump height in sine function and linear interpolation).
        // line 3: The sine function goes from sin(pi*0) = sin(0) = 0 (start of jump), to sin(pi*1) = sin(pi) = 0 (landing). The halfway angle, or the highest point of the jump, is sin(pi*0.5) = sin(pi/2) = 1. This gives a parabolic path for my boss sprite. I multiply the resulting sine value by jumpHeight to give incremental increase/decrease in the y-value. If I want my jumpHeight = 4 for example, the height would start at sin(0)*4 = 0*4 = 0. Halfway (highest) point would be sin(pi/2)*4 = 1*4 = 4. Landing height would be sin(pi)*4 = 0*4 = 0.
        // line 4: Linear interpolation is used when you want to go from point A (start position) to point B (end position) on a straight line, advancing by a value determined by 't' (a value between 0 and 1). 't' is basically a percentage of how far you've come on the line between the 2 points. So if point A = (0,0) and B = (1,1), then when t = 0, you are 0% through the line, so it returns (0,0). When t = 0.5, you are 50% through the line, so it returns (0.5,0.5). When t = 1, you are 100% throught the line, so it returns (1,1). So the purpose of Vector2.Lerp() is to get the percentage of how far the boss has travelled the jump path. But we're not done. Since I want to have a parabolic jump, I first take the position of the boss from the jump path using linear interpolation, and then multiply the y-value (Vector2.up = (0,1)) by the yOffset.
        // line 5: I assign the position of the boss to be the calculated parabolic position
        // line 6: Increment timeElapsed by Time.deltaTime to be checked against jumpDuration in the while loop condition
        jump = true;
        a.SetInteger("state", (int)States.jump);
        startJump.Play();
        yield return new WaitForSeconds(anim[2].length);

        if(buffRunning) { powerUpPulse.Stop(); }

        // disable the fear circle sprites when jumping (looks better this way ig)
        pulseCircle.GetComponent<SpriteRenderer>().enabled = false;
        donutCircle.GetComponent<SpriteRenderer>().enabled = false;

        float timeElapsed = 0.0f; // time elapsed from the beginning of jump until the end
        snapshotMCPosition = MC.position;
        jumpStartPosition = transform.position;

        while (timeElapsed < jumpDuration) // line 1
        {
            float percentageElapsed = timeElapsed / jumpDuration; // line 2
            float yOffset = Mathf.Sin(Mathf.PI * percentageElapsed) * jumpHeight; // line 3
            Vector2 parabolicPosition = Vector2.Lerp(jumpStartPosition, snapshotMCPosition, percentageElapsed) + Vector2.up * yOffset; // line 4
            transform.position = parabolicPosition; // line 5

            timeElapsed += Time.deltaTime; // line 6
            yield return null; // Let the physics update. Just goes to next frame to render the boss' position incrementally.
        }

        // Trigger screen shake when the boss lands
        mainCamera.GetComponent<ScreenShake>().Shake();
        
        loadLandingSmoke(); // landing the jump animation
        
        // re-enable the circle sprites if 'fearRunning' is true
        if (buffRunning)
        {
            pulseCircle.GetComponent<SpriteRenderer>().enabled = true;
            donutCircle.GetComponent<SpriteRenderer>().enabled = true;
        }

        a.SetTrigger("land"); // Trigger landing animation
        jumpLanding.Play();
        yield return new WaitForSeconds(anim[2].length);
        
        if (buffRunning) { powerUpPulse.Play(); }

        jump = false;
    }

    private void loadLandingSmoke()
    {
        Vector3 smokeOffset = new Vector3(-0.31f, -0.76f, 0);
        GameObject landingSmoke = Instantiate(landingSmokePrefab, transform.position + smokeOffset, Quaternion.identity);
        Destroy(landingSmoke, anim[4].length);
    }
    private void loadSaws()
    {
        // Main camera's viewport goes from (0,0) (bottom left of screen) to (1,1) (top right of screen)
        // In this case, ViewportToWorldPoint() transforms the camera's viewport position to the saw's game world position
        Vector3 sawPos = Camera.main.ViewportToWorldPoint(new Vector3(Random.Range(1.1f, 1.4f), Random.Range(0f, 1f), 10f));
        Instantiate(sawPrefab, sawPos, Quaternion.identity);

        sawPos = Camera.main.ViewportToWorldPoint(new Vector3(Random.Range(1.4f, 1.6f), Random.Range(0f, 1f), 10f));
        Instantiate(sawPrefab, sawPos, Quaternion.identity);

        sawPos = Camera.main.ViewportToWorldPoint(new Vector3(Random.Range(1.7f, 1.9f), Random.Range(0f, 1f), 10f));
        Instantiate(sawPrefab, sawPos, Quaternion.identity);
    }

    
    private IEnumerator BossDeath()
    {
        dead = true;
        sceneSwitch.canLeave = true;
        rb.bodyType = RigidbodyType2D.Static;
        a.SetTrigger("death"); // show death animation
        deathAudio.Play();

        destroySaws();
        destroyChildren();

        yield return new WaitForSeconds(deathAudio.clip.length - 0.2f);
    }

    private IEnumerator OnBossDeath()
    {
        float timer = 0f;
        float val = 0f;

        while (timer < 1)
        {
            val = Mathf.Lerp(1f, 0f, timer / 1);
            timer += Time.deltaTime;
            DeathMaterial.SetFloat("_Fade", val);
            yield return null;
        }

        Rigidbody2D rbCoin = GameObject.Instantiate(Coin, transform.position, Quaternion.identity);
        rbCoin.gameObject.GetComponent<Coin>().value = value;

        DeathMaterial.SetFloat("_Fade", 1f);
        Destroy(gameObject); // Destroys boss gameobject
    }

    private void destroySaws()
    {
        // Find all active saw prefabs (if they exist) in the scene and destroy them
        GameObject[] sawsToDestroy = GameObject.FindGameObjectsWithTag("saw");

        foreach (GameObject saw in sawsToDestroy)
        {
            Destroy(saw);
        }
    }

    private void destroyChildren()
    {
        // Iterate through each child of the boss GameObject
        foreach (Transform child in transform)
        {
            // Destroy the child GameObject
            Destroy(child.gameObject);
        }
    }

    private void OnDestroy()
    {
        Destroy(healthBar.gameObject);
    }
}