using System.Collections.Generic;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Light combos drawn with the whole body. The body is empty-handed so any weapon of the family can be held; each frame records
    /// where the hand is, and the blade turns by a curve off the aim. Right views are drawn, left views are their mirror image.
    /// Hands were found in the frames by the install step recorded in Tools/Art/Staging/PixelLab/20261105-melee-combo, in the hero's 48x64 source pixels.</summary>
    public static class MeleeComboFrames
    {
        sealed class Clip
        {
            public readonly string Name; public readonly Vector2[][] Hands; public readonly int[][] Contacts;
            public Clip(string name, Vector2[][] hands, int[][] contacts) { Name = name; Hands = hands; Contacts = contacts; }
        }

        sealed class Step
        {
            public readonly Clip Clip; public readonly float[] Blade, ContactProgress; public readonly float Seconds, Lunge; public readonly bool Dust;
            public Step(string clip, float[] blade, float[] contactProgress, float seconds, float lunge, bool dust)
            { Clip = Clips[clip]; Blade = blade; ContactProgress = contactProgress; Seconds = seconds; Lunge = lunge; Dust = dust; }
        }

        static readonly Dictionary<string, Clip> Clips = new Dictionary<string, Clip>
        {
            ["UpCut"]=new Clip("UpCut",new[]{new[]{new Vector2(28.5f,36.5f),new Vector2(29.5f,32.5f),new Vector2(30.5f,33.5f),new Vector2(31.5f,32.5f),new Vector2(33.5f,35.5f),new Vector2(33.5f,26.5f),new Vector2(34.5f,28.5f),new Vector2(31.5f,34.5f),new Vector2(28.5f,36.5f)},new[]{new Vector2(30.5f,34.5f),new Vector2(29.5f,35.5f),new Vector2(28.5f,36.5f),new Vector2(33.5f,34.5f),new Vector2(32.5f,26.5f),new Vector2(33.5f,27.5f),new Vector2(31.5f,28.5f),new Vector2(30.5f,31.5f),new Vector2(30.5f,34.5f)}},new[]{new[]{6},new[]{6}}),
            ["DownChop"]=new Clip("DownChop",new[]{new[]{new Vector2(28.3f,36.3f),new Vector2(31.3f,32.3f),new Vector2(31.1f,26.0f),new Vector2(28.8f,20.7f),new Vector2(27.4f,21.9f),new Vector2(28.8f,21.4f),new Vector2(32.5f,27.5f),new Vector2(30.5f,36.5f),new Vector2(28.5f,36.5f)},new[]{new Vector2(30.5f,34.5f),new Vector2(31.5f,31.5f),new Vector2(30.5f,28.5f),new Vector2(28.5f,29.5f),new Vector2(28.0f,34.0f),new Vector2(28.5f,29.5f),new Vector2(29.5f,31.5f),new Vector2(30.5f,36.5f),new Vector2(30.5f,34.5f)}},new[]{new[]{6},new[]{6}}),
            ["Spin"]=new Clip("Spin",new[]{new[]{new Vector2(28.5f,36.5f),new Vector2(32.5f,35.5f),new Vector2(35.5f,30.5f),new Vector2(33.5f,31.5f),new Vector2(32.5f,32.5f),new Vector2(32.5f,34.5f),new Vector2(33.5f,33.5f),new Vector2(34.5f,29.5f),new Vector2(28.5f,36.5f)},new[]{new Vector2(30.5f,34.5f),new Vector2(32.5f,33.5f),new Vector2(35.5f,33.5f),new Vector2(36.5f,29.5f),new Vector2(36.5f,29.5f),new Vector2(36.5f,31.5f),new Vector2(33.5f,34.5f),new Vector2(29.5f,34.5f),new Vector2(30.5f,34.5f)}},new[]{new[]{3},new[]{3}}),
            ["CrossLunge"]=new Clip("CrossLunge",new[]{new[]{new Vector2(28.5f,36.5f),new Vector2(26.5f,35.5f),new Vector2(29.5f,30.5f),new Vector2(29.5f,31.5f),new Vector2(29.5f,31.5f),new Vector2(28.5f,31.5f),new Vector2(32.5f,33.5f),new Vector2(32.5f,33.5f),new Vector2(29.5f,36.5f),new Vector2(28.5f,36.5f),new Vector2(31.5f,34.5f),new Vector2(32.5f,34.5f),new Vector2(28.5f,36.5f)},new[]{new Vector2(30.5f,34.5f),new Vector2(31.5f,33.5f),new Vector2(32.5f,31.5f),new Vector2(33.5f,30.5f),new Vector2(32.5f,31.5f),new Vector2(28.5f,30.5f),new Vector2(27.5f,29.5f),new Vector2(27.5f,27.5f),new Vector2(28.5f,27.5f),new Vector2(28.5f,26.5f),new Vector2(28.5f,28.5f),new Vector2(30.5f,36.5f),new Vector2(30.5f,34.5f)}},new[]{new[]{6,9},new[]{6,9}}),
            ["QuickA"]=new Clip("QuickA",new[]{new[]{new Vector2(28.5f,36.5f),new Vector2(29.5f,36.5f),new Vector2(30.5f,35.5f),new Vector2(31.5f,34.5f),new Vector2(31.5f,34.5f),new Vector2(30.5f,30.5f),new Vector2(29.5f,35.5f),new Vector2(32.5f,34.5f),new Vector2(28.5f,36.5f)},new[]{new Vector2(30.5f,34.5f),new Vector2(30.5f,34.5f),new Vector2(29.5f,37.5f),new Vector2(27.5f,37.5f),new Vector2(26.5f,36.5f),new Vector2(26.5f,36.5f),new Vector2(28.5f,36.5f),new Vector2(30.5f,34.5f),new Vector2(30.5f,34.5f)}},new[]{new[]{7},new[]{7}}),
            ["QuickB"]=new Clip("QuickB",new[]{new[]{new Vector2(28.5f,36.5f),new Vector2(29.5f,36.5f),new Vector2(31.5f,36.5f),new Vector2(32.5f,32.5f),new Vector2(32.5f,31.5f),new Vector2(32.5f,31.5f),new Vector2(32.5f,34.5f),new Vector2(30.5f,36.5f),new Vector2(28.5f,36.5f)},new[]{new Vector2(30.5f,34.5f),new Vector2(30.5f,33.5f),new Vector2(29.5f,35.5f),new Vector2(30.5f,33.5f),new Vector2(30.5f,27.5f),new Vector2(31.5f,30.5f),new Vector2(30.5f,33.5f),new Vector2(29.5f,36.5f),new Vector2(30.5f,34.5f)}},new[]{new[]{4},new[]{5}}),
            ["DashThrust"]=new Clip("DashThrust",new[]{new[]{new Vector2(28.5f,36.5f),new Vector2(29.5f,35.5f),new Vector2(31.5f,35.5f),new Vector2(34.5f,32.5f),new Vector2(34.5f,32.5f),new Vector2(34.5f,32.5f),new Vector2(31.5f,34.5f),new Vector2(31.5f,36.5f),new Vector2(28.5f,36.5f)},new[]{new Vector2(30.5f,34.5f),new Vector2(31.5f,31.5f),new Vector2(30.5f,35.5f),new Vector2(25.5f,35.5f),new Vector2(28.5f,38.5f),new Vector2(30.5f,31.5f),new Vector2(30.5f,26.5f),new Vector2(29.5f,30.5f),new Vector2(30.5f,34.5f)}},new[]{new[]{4},new[]{3}}),
            ["Thrust"]=new Clip("Thrust",new[]{new[]{new Vector2(28.5f,36.5f),new Vector2(30.5f,36.5f),new Vector2(32.5f,35.5f),new Vector2(36.5f,32.5f),new Vector2(38.5f,33.5f),new Vector2(38.5f,33.5f),new Vector2(37.5f,32.5f),new Vector2(34.5f,35.5f),new Vector2(28.5f,36.5f)},new[]{new Vector2(30.5f,34.5f),new Vector2(32.5f,31.5f),new Vector2(31.5f,34.5f),new Vector2(28.5f,36.5f),new Vector2(26.5f,36.5f),new Vector2(26.5f,36.5f),new Vector2(28.5f,37.5f),new Vector2(33.5f,33.5f),new Vector2(30.5f,34.5f)}},new[]{new[]{4},new[]{7}}),
            ["WideSweep"]=new Clip("WideSweep",new[]{new[]{new Vector2(28.5f,36.5f),new Vector2(29.5f,35.5f),new Vector2(33.5f,33.5f),new Vector2(34.5f,30.5f),new Vector2(34.5f,31.5f),new Vector2(34.5f,30.5f),new Vector2(33.5f,31.5f),new Vector2(32.5f,35.5f),new Vector2(28.5f,36.5f)},new[]{new Vector2(30.5f,34.5f),new Vector2(30.5f,34.5f),new Vector2(33.5f,33.5f),new Vector2(33.5f,31.5f),new Vector2(32.5f,33.5f),new Vector2(33.5f,32.5f),new Vector2(32.5f,34.5f),new Vector2(30.5f,35.5f),new Vector2(30.5f,34.5f)}},new[]{new[]{3},new[]{3}}),
            ["HeavyA"]=new Clip("HeavyA",new[]{new[]{new Vector2(28.5f,36.5f),new Vector2(31.5f,35.5f),new Vector2(30.5f,36.5f),new Vector2(29.5f,27.5f),new Vector2(30.5f,28.5f),new Vector2(29.5f,27.5f),new Vector2(30.5f,29.5f),new Vector2(33.5f,36.5f),new Vector2(28.5f,36.5f)},new[]{new Vector2(30.5f,34.5f),new Vector2(30.5f,34.5f),new Vector2(28.5f,34.5f),new Vector2(25.5f,34.5f),new Vector2(25.5f,34.5f),new Vector2(26.5f,34.5f),new Vector2(27.5f,34.5f),new Vector2(28.5f,34.5f),new Vector2(30.5f,34.5f)}},new[]{new[]{7},new[]{7}}),
            ["HeavyB"]=new Clip("HeavyB",new[]{new[]{new Vector2(28.5f,36.5f),new Vector2(28.5f,37.5f),new Vector2(31.5f,35.5f),new Vector2(30.5f,29.5f),new Vector2(31.5f,30.5f),new Vector2(31.5f,30.5f),new Vector2(31.5f,34.5f),new Vector2(31.5f,36.5f),new Vector2(28.5f,36.5f)},new[]{new Vector2(30.5f,34.5f),new Vector2(31.5f,32.5f),new Vector2(31.5f,34.5f),new Vector2(29.5f,28.5f),new Vector2(27.5f,27.5f),new Vector2(28.5f,28.5f),new Vector2(28.5f,29.5f),new Vector2(29.5f,36.5f),new Vector2(30.5f,34.5f)}},new[]{new[]{6},new[]{6}}),
            ["LeapSlam"]=new Clip("LeapSlam",new[]{new[]{new Vector2(21.5f,35.5f),new Vector2(21.5f,38.5f),new Vector2(18.5f,38.5f),new Vector2(18.5f,28.5f),new Vector2(14.5f,24.5f),new Vector2(14.5f,24.5f),new Vector2(16.5f,25.5f),new Vector2(27.5f,37.5f),new Vector2(32.5f,42.5f),new Vector2(32.5f,42.5f),new Vector2(31.5f,38.5f),new Vector2(31.5f,33.5f),new Vector2(28.5f,36.5f)},new[]{new Vector2(30.5f,34.5f),new Vector2(31.5f,34.5f),new Vector2(31.5f,30.5f),new Vector2(31.5f,26.5f),new Vector2(29.0f,33.0f),new Vector2(28.5f,32.0f),new Vector2(30.5f,24.5f),new Vector2(32.5f,28.5f),new Vector2(32.5f,33.5f),new Vector2(30.5f,38.5f),new Vector2(29.5f,38.5f),new Vector2(30.5f,36.5f),new Vector2(30.5f,34.5f)}},new[]{new[]{8},new[]{9}})
        };

        // When a swing's damage lands, the same clocks ResolvePendingMelee uses.
        static readonly float[] AtContact = { MeleeMotionTiming.Contact };
        static readonly float[] AtThrust = { MeleeMotionTiming.ThrustContact };
        static readonly float[] AtCross = { MeleeMotionTiming.CrossFirstContact, MeleeMotionTiming.CrossSecondContact };

        // The blade's angle off the aim for a body facing right: progress, angle, progress, angle...
        static readonly float[] Rising = { 0f, 0f, .16f, -115f, .24f, -95f, .3f, 0f, .45f, 40f, .7f, 15f, 1f, 0f };
        static readonly float[] Falling = { 0f, 0f, .16f, 115f, .24f, 125f, .3f, 0f, .45f, -40f, .7f, -15f, 1f, 0f };
        static readonly float[] FullTurn = { 0f, 0f, .12f, -80f, .24f, -40f, .3f, 0f, .5f, 140f, .75f, 290f, 1f, 360f };
        static readonly float[] Cross = { 0f, 0f, .1f, -50f, .18f, -90f, .22f, 0f, .34f, 75f, .42f, 90f, .52f, 0f, .7f, -50f, .85f, -25f, 1f, 0f };
        static readonly float[] FlickOut = { 0f, 0f, .18f, -70f, .3f, 0f, .5f, 45f, 1f, 0f };
        static readonly float[] FlickBack = { 0f, 0f, .18f, 70f, .3f, 0f, .5f, -45f, 1f, 0f };
        static readonly float[] Straight = { 0f, 0f, .2f, -12f, .34f, 0f, .5f, 4f, 1f, 0f };
        static readonly float[] DriveIn = { 0f, 0f, .2f, 18f, .34f, 0f, .5f, -5f, 1f, 0f };
        static readonly float[] PointDown = { 0f, 0f, .16f, 55f, .28f, 65f, .34f, 0f, .5f, -12f, 1f, 0f };
        static readonly float[] WideOut = { 0f, 0f, .2f, -120f, .3f, 0f, .5f, 55f, 1f, 0f };
        static readonly float[] HeavyOut = { 0f, 0f, .2f, -135f, .3f, 0f, .5f, 70f, .75f, 30f, 1f, 0f };
        static readonly float[] HeavyBack = { 0f, 0f, .2f, 135f, .3f, 0f, .5f, -70f, .75f, -30f, 1f, 0f };
        static readonly float[] SlamDown = { 0f, 0f, .18f, 100f, .26f, 130f, .3f, 0f, .45f, -28f, 1f, 0f };

        // A swing's pose runs longer than the time to the next swing on purpose: chaining cuts the recovery off, stopping lets it play out.
        static readonly Dictionary<WeaponFamily, Step[]> Combos = new Dictionary<WeaponFamily, Step[]>
        {
            [WeaponFamily.Katana] = new[]
            {
                new Step("UpCut", Rising, AtContact, .40f, 1f, false),
                new Step("DownChop", Falling, AtContact, .40f, 1f, false),
                new Step("Spin", FullTurn, AtContact, .46f, .8f, true),
                new Step("CrossLunge", Cross, AtCross, .56f, 1.6f, true),
            },
            [WeaponFamily.Dagger] = new[]
            {
                new Step("QuickA", FlickOut, AtContact, .30f, 1f, false),
                new Step("QuickB", FlickBack, AtContact, .30f, 1f, false),
                new Step("Spin", FullTurn, AtContact, .36f, .6f, false),
                new Step("DashThrust", DriveIn, AtThrust, .44f, 2.2f, true),
            },
            [WeaponFamily.Spear] = new[]
            {
                new Step("Thrust", Straight, AtThrust, .38f, 1f, false),
                new Step("WideSweep", WideOut, AtContact, .44f, .6f, true),
                new Step("DashThrust", DriveIn, AtThrust, .44f, 1.8f, true),
                new Step("LeapSlam", PointDown, AtThrust, .64f, 1.5f, true),
            },
            [WeaponFamily.Greatsword] = new[]
            {
                new Step("HeavyA", HeavyOut, AtContact, .56f, 1f, true),
                new Step("HeavyB", HeavyBack, AtContact, .56f, .9f, true),
                new Step("LeapSlam", SlamDown, AtContact, .80f, 1.4f, true),
            },
        };

        static readonly string[] Views = { "Ru", "Rd", "Ld", "Lu" };
        static int ViewIndex(int facing) => facing <= 1 ? 0 : facing <= 3 ? 1 : facing <= 5 ? 2 : 3;
        static bool FacesLeft(int facing) => facing >= 4;
        // Index into a clip's drawn views: 0 faces lower right, 1 faces upper right. Left views share them, mirrored.
        static int DrawnView(int facing) { var view = ViewIndex(facing); return view == 1 || view == 2 ? 0 : 1; }

        public static bool Has(WeaponFamily family, int step) => step >= 0 && Combos.TryGetValue(family, out var combo) && step < combo.Length;
        public static int StepCount(WeaponFamily family) => Combos.TryGetValue(family, out var combo) ? combo.Length : 0;
        /// <summary>How long the pose runs. The last blow must land before the next swing can start, however fast attacks have become, or that blow is lost.</summary>
        public static float SwingSeconds(WeaponFamily family, int stepIndex, float secondsToNextSwing)
        {
            var step = Combos[family][stepIndex];
            return Mathf.Min(step.Seconds, secondsToNextSwing / step.ContactProgress[step.ContactProgress.Length - 1]);
        }
        public static float Lunge(WeaponFamily family, int step) => Combos[family][step].Lunge;
        public static bool RaisesDust(WeaponFamily family, int step) => Combos[family][step].Dust;

        static float Curve(float[] keys, float progress)
        {
            for (var i = 2; i < keys.Length; i += 2)
                if (progress <= keys[i]) return Mathf.Lerp(keys[i - 1], keys[i + 1], Mathf.InverseLerp(keys[i - 2], keys[i], progress));
            return keys[keys.Length - 1];
        }

        static float FrameAt(Step step, int view, float progress)
        {
            var contacts = step.Clip.Contacts[view];
            var fromProgress = 0f; var fromFrame = 0f;
            for (var i = 0; i < contacts.Length; i++)
            {
                if (progress < step.ContactProgress[i]) return Mathf.Lerp(fromFrame, contacts[i], Mathf.InverseLerp(fromProgress, step.ContactProgress[i], progress));
                fromProgress = step.ContactProgress[i]; fromFrame = contacts[i];
            }
            return Mathf.Lerp(fromFrame, step.Clip.Hands[view].Length - 1, Mathf.InverseLerp(fromProgress, 1f, progress));
        }

        static Vector2 HandAt(Step step, int facing, int frame)
        {
            var hand = step.Clip.Hands[DrawnView(facing)][frame];
            return FacesLeft(facing) ? new Vector2(48f - hand.x, hand.y) : hand;
        }

        public static Sprite Sample(WeaponFamily family, int stepIndex, int facing, float progress, out Vector2 hand, out float bladeAngle, out int frame)
        {
            var step = Combos[family][stepIndex];
            var view = DrawnView(facing);
            frame = Mathf.Clamp(Mathf.FloorToInt(FrameAt(step, view, progress)), 0, step.Clip.Hands[view].Length - 1);
            hand = HandAt(step, facing, frame);
            bladeAngle = Curve(step.Blade, progress) * (FacesLeft(facing) ? -1f : 1f);
            return NativeCombatEffect.Frames("HeroCombo" + step.Clip.Name + Views[ViewIndex(facing)])[frame];
        }

        public static Vector2 HandFor(WeaponFamily family, int stepIndex, int facing, float progress)
        {
            var step = Combos[family][stepIndex];
            var view = DrawnView(facing);
            return HandAt(step, facing, Mathf.Clamp(Mathf.FloorToInt(FrameAt(step, view, progress)), 0, step.Clip.Hands[view].Length - 1));
        }

        /// <summary>Which way a sweep's trail is mirrored, so it turns the way this blade turns.</summary>
        public static bool TrailMirrored(WeaponFamily family, int stepIndex, bool facingLeft)
        {
            var step = Combos[family][stepIndex];
            return Curve(step.Blade, step.ContactProgress[0] - .05f) < 0f != facingLeft;
        }

        public static void Preload(WeaponFamily family)
        {
            if (!Combos.TryGetValue(family, out var combo)) return;
            foreach (var step in combo)
                foreach (var view in Views) NativeCombatEffect.Frames("HeroCombo" + step.Clip.Name + view);
        }
    }
}
