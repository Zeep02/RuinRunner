using System;
using UnityEngine;

namespace RuinRunner
{
    public sealed class ExperienceOrb : MonoBehaviour
    {
        private PlayerProgression target;
        private int amount;
        private Vector2 drift;
        private Func<bool> canCollect;
        private float collectionDelay;
        private bool chestBurst;
        private float settleY;
        private SpriteRenderer view;
        private Sprite[] frames;
        private float animationTime;
        private float homingSpeed = 4f;
        private float echoTimer;
        private bool collected;
        public bool IsCollected => collected;
        public static string SizeForAmount(int value) => value <= 5 ? "Small" : value <= 14 ? "Medium" : "Large";

        public static ExperienceOrb Create(Vector2 position, int experience, PlayerProgression progression, Func<bool> collectionQuery)
        {
            if (experience <= 0) return null;
            var orbObject = new GameObject("ExperienceOrb");
            orbObject.transform.position = position;
            var orb = orbObject.AddComponent<ExperienceOrb>();
            var visual = new GameObject("AnimatedOrb"); visual.transform.SetParent(orbObject.transform, false);
            orb.view = visual.AddComponent<SpriteRenderer>(); orb.view.sortingOrder = 14;
            orb.frames = IndividualArt.Frames("Feedback/Experience/" + SizeForAmount(experience), 6);
            orb.animationTime = UnityEngine.Random.value * .6f;
            orb.view.sprite = orb.frames[Mathf.FloorToInt(orb.animationTime * 10f) % orb.frames.Length];
            orb.target = progression;
            orb.amount = experience;
            orb.canCollect = collectionQuery;
            orb.drift = UnityEngine.Random.insideUnitCircle * 1.8f + Vector2.up * 1.8f;
            return orb;
        }

        public static void CreateBurst(Vector2 position, int totalExperience, int count, PlayerProgression progression, Func<bool> canCollect = null)
        {
            if (totalExperience <= 0) return;
            var rewards = SplitBurst(totalExperience, count);
            count = rewards.Length;
            for (var index = 0; index < count; index++)
            {
                var orb = Create(position + Vector2.up * .45f, rewards[index], progression, canCollect ?? (() => true));
                var angle = Mathf.Lerp(35f, 145f, count <= 1 ? .5f : index / (count - 1f));
                var speed = UnityEngine.Random.Range(4.8f, 7.2f);
                orb.drift = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * speed;
                orb.collectionDelay = UnityEngine.Random.Range(.55f, .85f);
                orb.chestBurst = true;
                orb.settleY = position.y + .2f;
            }
        }

        // Different denominations, never extra XP or zero-value objects.
        public static int[] SplitBurst(int total, int requestedCount)
        {
            if (total <= 0) return Array.Empty<int>();
            var count = Mathf.Clamp(requestedCount, 1, total);
            var result = new int[count]; var remaining = total;
            for (var i = 0; i < count - 1; i++)
            {
                var slotsLeft = count - i;
                result[i] = Mathf.Clamp(Mathf.RoundToInt(remaining / (float)slotsLeft * (i % 2 == 0 ? .5f : 1f)), 1, remaining - slotsLeft + 1);
                remaining -= result[i];
            }
            result[count - 1] = remaining;
            return result;
        }

        private void Update()
        {
            if (collected || Time.deltaTime <= 0f) return;
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }

            animationTime += Time.deltaTime;
            view.sprite = frames[Mathf.FloorToInt(animationTime * 10f) % frames.Length];
            view.transform.localPosition = Vector3.up * (.045f * Mathf.Sin(animationTime * 4f));

            if (collectionDelay > 0f)
            {
                collectionDelay -= Time.deltaTime;
                ScatterFromChest();
                return;
            }

            if (canCollect != null && !canCollect())
            {
                transform.position += (Vector3)(drift * Time.deltaTime);
                drift = Vector2.Lerp(drift, Vector2.zero, 3f * Time.deltaTime);
                homingSpeed = 4f;
                return;
            }

            // Nothing left to fight here, so nothing is lost by walking off: it comes to the hero from anywhere.
            homingSpeed = Mathf.MoveTowards(homingSpeed, 16f, 22f * Time.deltaTime);
            if (((Vector2)(target.transform.position - transform.position)).sqrMagnitude < 25f)
            {
                echoTimer -= Time.deltaTime;
                if (echoTimer <= 0f)
                {
                    echoTimer = .045f;
                    TransientSpriteEffect.Create(view.sprite, view.transform.position, Vector3.one * .85f,
                        new Color(.85f, 1f, .55f, .4f), .16f, 13);
                }
            }
            transform.position = Vector2.MoveTowards(transform.position, target.transform.position, homingSpeed * Time.deltaTime);

            if (Vector2.Distance(transform.position, target.transform.position) <= .42f) Collect();
        }

        private void ScatterFromChest()
        {
            transform.position += (Vector3)(drift * Time.deltaTime);
            drift.y -= 12f * Time.deltaTime;
            if (chestBurst && transform.position.y < settleY)
            {
                var position = transform.position;
                position.y = settleY;
                transform.position = position;
                drift.y = Mathf.Abs(drift.y) * .3f;
                drift.x *= .8f;
            }
        }

        private void Collect()
        {
            if (collected) return;
            collected = true;
            var point = target.transform.position;
            TransientSpriteEffect.Create(view.sprite, point, Vector3.one * 1.15f, new Color(1f, 1f, .8f, .8f), .18f, 16, finalScale: 1.7f);
            var spark = IndividualArt.Sprite("Feedback/Experience/Small/02");
            for (var i = 0; i < 3; i++)
            {
                var angle = i * Mathf.PI * 2f / 3f + .4f;
                TransientSpriteEffect.Create(spark, point, Vector3.one * .55f, new Color(.85f, 1f, .65f, .85f), .23f, 16,
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.2f);
            }
            target.AddExperience(amount);
            Destroy(gameObject);
        }
    }
}
