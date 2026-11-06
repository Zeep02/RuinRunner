using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace RuinRunner
{
    public sealed partial class MotionValidation
    {
        private IEnumerator ValidateThrustOptions()
        {
            Require(!string.IsNullOrEmpty(captureDirectory), "isolated capture directory supplied");
            var path = Path.Combine(captureDirectory, "audio-settings-fixture.json");
            if (File.Exists(path)) throw new InvalidOperationException("Preserve earlier audio settings fixture");
            File.WriteAllText(path, "{\"version\":5,\"masterVolume\":0.8,\"musicVolume\":0.5,\"interfaceVolume\":0.4," +
                "\"ambienceVolume\":0.2,\"playerVolume\":0.3,\"monsterVolume\":0.6,\"inventoryVolume\":0.9," +
                "\"bindings\":[{\"action\":3,\"primary\":" + (int)KeyCode.K + ",\"alternate\":0}]}");
            var masterBefore = AudioListener.volume;
            var settings = new GameSettingsService(path, false);
            Require(Mathf.Approximately(settings.GetVolume(AudioChannel.Effects), .6f), "v5 effects volume migrates from three legacy channels");
            settings.ChangeVolume(AudioChannel.Effects, .1f);
            foreach (var channel in new[] { AudioChannel.Player, AudioChannel.Monster, AudioChannel.Inventory, AudioChannel.Effects })
                Require(Mathf.Approximately(settings.GetVolume(channel), .7f), "all effects routed to shared setting: " + channel);
            Require(settings.GetVolume(AudioChannel.Music) == .5f && settings.GetVolume(AudioChannel.Interface) == .4f &&
                settings.GetVolume(AudioChannel.Ambience) == .2f && settings.MasterVolume == .8f, "effects adjustment leaves four other categories intact");
            var reloaded = new GameSettingsService(path, false);
            Require(Mathf.Approximately(reloaded.GetVolume(AudioChannel.Effects), .7f) &&
                reloaded.GetBinding(GameAction.Attack).primary == KeyCode.K, "v6 volume persists and custom key binding survives");
            reloaded.ChangeVolume(AudioChannel.Effects, -10f);
            Require(reloaded.GetVolume(AudioChannel.Effects) == 0, "effects clamps to mute");
            reloaded.ChangeVolume(AudioChannel.Effects, 10f);
            Require(reloaded.GetVolume(AudioChannel.Effects) == 1, "effects clamps to full");
            reloaded.ChangeVolume(AudioChannel.Master, -.2f);
            Require(AudioListener.volume == masterBefore, "settings fixtures never change device audio");
            var channels = (AudioChannel[])typeof(PrototypeHud).GetField("AudioChannels", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Require(channels.Length == 5 && channels[0] == AudioChannel.Master && channels[1] == AudioChannel.Music &&
                channels[2] == AudioChannel.Interface && channels[3] == AudioChannel.Effects && channels[4] == AudioChannel.Ambience,
                "options exposes exactly master / music / interface / effects / ambience");
            var bar = IndividualArt.Texture("ThrustOptions/VolumeTrack");
            Require(bar.width == 1952 && bar.height == 159 && bar.filterMode == FilterMode.Point, "generated bronze track loaded without resampling");
            var rect = new Rect(0, 0, 224, 22);
            Require(VolumeGaugeSkin.FillRect(rect, 0).width == 0 && VolumeGaugeSkin.FillRect(rect, 1).width == 208,
                "volume fill is inset correctly at zero and full");

            var floor = new GameObject("ScaleValidationFloor"); floor.transform.localScale = new Vector3(35, 20, 1);
            PrototypeVisuals.AddSprite(floor, new Color(.29f, .35f, .46f), -100);
            var types = new[] { EnemyType.Grunt, EnemyType.Charger, EnemyType.Leaper, EnemyType.Boss };
            var enemies = new List<EnemyController>();
            for (var i = 0; i < types.Length; i++)
            {
                var enemy = EnemyController.Create(types[i], new Vector2(-3 + i * 2, 2), player, () => true, _ => { }, null);
                enemies.Add(enemy); enemy.enabled = false;
                var visual = enemy.transform.Find("VisualRoot");
                var expected = enemy.Type == EnemyType.Boss ? 1f : .75f;
                Require(Mathf.Abs(visual.lossyScale.x - expected) < .001f && Mathf.Abs(visual.lossyScale.y - expected) < .001f,
                    "uniform visual size, boss unchanged: " + enemy.Type);
                var balance = GameBalance.Current.GetEnemy(enemy.Type);
                var foot = enemy.GetComponent<BoxCollider2D>();
                Require(Vector2.Distance(foot.size, balance.movementFootSize * expected) < .001f,
                    "foot collider follows smaller body: " + enemy.Type);
                var before = enemy.DamageCollider.bounds;
                enemy.ApplyBalance(balance); enemy.UpdateVisualPresentation();
                Require(Vector3.Distance(before.size, enemy.DamageCollider.bounds.size) < .001f,
                    "balance refresh does not multiply shrink scale: " + enemy.Type);
            }
            for (var i = 0; i < 8; i++) yield return null;
            PolishCapture("01-smaller-enemies-player-boss");
            // CaptureScreenshot is deferred until the frame is drawn; keep the subjects alive through it.
            for (var i = 0; i < 3; i++) yield return null;
            foreach (var enemy in enemies) Destroy(enemy.gameObject);
            yield return null;

            player.EquipWeapon(DemoContent.Weapon("trainee_blade"));
            var third = MeleeComboCatalog.Get(player.EquippedWeapon).LightSteps[2];
            var preparation = Mathf.Min(.26f,player.AttackDelay * third.DelayMultiplier) * MeleeMotionTiming.ThrustStart;
            Require(preparation >= .04f && preparation <= .08f, "third-hit preparation is about0.06s without a movement lock");
            StageValidationInput.SetAim(Vector2.right); StageValidationInput.SetHeld(GameAction.Attack, true);
            var timeout = 120;
            while (!player.IsThrustActive && timeout-- > 0) yield return null;
            Require(player.IsThrustActive, "held combo reaches quick third hit");
            StageValidationInput.ReleaseAll();
            var contact = player.MeleeContactsResolved;
            var started = Time.time;
            while (player.MeleeContactsResolved == contact) yield return null;
            Require(Time.time - started < .34f, "shortened third-hit damage arrives promptly");
            var effect = UnityEngine.Object.FindAnyObjectByType<SignatureSlashEffect>();
            Require(effect != null && effect.GetComponentInChildren<SpriteRenderer>() != null, "actual combo spawns the thrust trail");
            var shown = new HashSet<int>();
            while (effect != null)
            {
                if (shown.Add(effect.FrameIndex)) PolishCapture("02-external-thrust-" + effect.FrameIndex);
                yield return null;
            }
            Require(shown.Count >= 2, "thrust advances through contact and breakup then despawns");
            Require(player.MeleeContactsResolved == contact + 1, "external VFX does not duplicate damage");
            for (var i = 0; i < 15; i++) yield return null;
        }
    }
}
