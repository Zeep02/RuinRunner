using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace RuinRunner
{
    /// <summary>Opt-in disk lifecycle checks after an actual automated victory, then in a second process.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class SaveLifecycleValidation : MonoBehaviour
    {
        [Serializable]
        private sealed class Proof
        {
            public int runs;
            public int victories;
            public int campaignVictories;
            public int bestCampaignStage;
            public int kills;
            public int shards;
            public MetaUpgradeType upgrade;
            public int rank;
            public float retryMaxHealth;
            public int firstProcess;
        }

        private GameController game;
        private bool relaunch;
        private IEnumerator sequence;
        private int checks;
        public void Initialize(GameController controller, bool isRelaunch)
        {
            game = controller;
            relaunch = isRelaunch;
        }
        private void Start()
        {
            Application.runInBackground = true;
            sequence = Validate();
        }
        private void Update()
        {
            if (sequence == null)
                return;
            try
            {
                if (sequence.MoveNext())
                    return;
                sequence = null;
                Debug.Log("[Save Lifecycle] PASS phase=" + (relaunch ? "relaunch" : "post-victory") + " checks=" + checks + " profile=" + game.SaveSlots.SaveDirectory);
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Save Lifecycle] FAIL " + exception);
                sequence = null;
                Application.Quit(3);
            }
        }

        private IEnumerator Validate()
        {
            Require(game.SaveSlots.IsValidationProfile, "only isolated validation profiles may be written");
            var proofPath = Path.Combine(Path.GetDirectoryName(game.SaveSlots.SaveDirectory), "lifecycle-proof.json");
            if (relaunch)
            {
                Require(File.Exists(proofPath), "first-process proof exists on disk");
                var proof = JsonUtility.FromJson<Proof>(File.ReadAllText(proofPath));
                Require(proof.firstProcess != System.Diagnostics.Process.GetCurrentProcess().Id, "verification is a new operating-system process");
                var summary = game.SaveSlots.GetSummary(1);
                Require(summary.Exists && summary.TotalRuns == proof.runs && summary.Victories == proof.victories &&
                    summary.TotalKills == proof.kills && summary.LegacyShards == proof.shards &&
                    summary.CampaignVictories == proof.campaignVictories && summary.BestCampaignStage == proof.bestCampaignStage, "slot summary restored from disk");
                game.StartSlot(1);
                for (var i = 0; i < 12; i++)
                    yield return null;
                VerifyReload(proof);
                var deadline = Time.unscaledTime + 5f;
                while (RuntimeSceneObjects.FindAll<EnemySpawnTelegraph>().Length == 0 && Time.unscaledTime < deadline)
                    yield return null;
                Require(RuntimeSceneObjects.FindAll<EnemySpawnTelegraph>().Length > 0, "exercise menu return during active wave telegraph");
                game.ReturnToMainMenu();
                for (var i = 0; i < 3; i++)
                    yield return null;
                Require(!game.IsRunActive && game.Player == null && game.SaveSlots.ActiveSlot == 0, "menu return clears active run and slot");
                Require(RuntimeSceneObjects.FindAll<EnemySpawnTelegraph>().Length == 0, "interrupted wave leaves no spawn indicators");
                game.StartSlot(1);
                for (var i = 0; i < 12; i++)
                    yield return null;
                VerifyReload(proof);
                game.ReturnToMainMenu();
                for (var i = 0; i < 3; i++)
                    yield return null;
                Require(RuntimeSceneObjects.FindAll<PlayerController>().Length == 0 && RuntimeSceneObjects.FindAll<EnemyController>().Length == 0,
                    "second menu return leaves no actors");
                yield break;
            }

            // Allow the actual ending and real-time recorder to finish before the injected retry fixture.
            var endingUntil = Time.unscaledTime + 2f;
            while (Time.unscaledTime < endingUntil)
                yield return null;
            Require(game.IsGameOver && game.IsVictory && game.StagesCleared == CampaignStage.Count, "normal-rule full campaign victory reached before lifecycle fixture");
            var data = game.SaveSlots.ActiveData;
            Require(game.SaveSlots.ActiveSlot == 1 && data.totalRuns == 1 && data.victories == 1 && data.campaignVictories == 1 && data.bestCampaignStage == 3 && data.totalKills > 0,
                "new slot recorded one real automated victory");
            var beforeResult = JsonUtility.FromJson<SaveSlotData>(File.ReadAllText(Path.Combine(game.SaveSlots.SaveDirectory, "save1.json.bak")));
            Require(beforeResult.totalRuns == 0 && beforeResult.legacyShards == new SaveSlotData().legacyShards,
                "victory reward and run counters persisted as one atomic snapshot");
            foreach (var upgrade in data.upgrades)
                Require(upgrade.rank == 0, "first victory used no permanent upgrades");
            var runs = data.totalRuns;
            var victories = data.victories;
            var shards = data.legacyShards;
            var kills = data.totalKills;
            game.ShowStageResult();
            game.ShowStageResult();
            Require(data.totalRuns == runs && data.victories == victories && data.legacyShards == shards, "result cannot award twice");
            game.RestartRun();
            for (var i = 0; i < 12; i++)
                yield return null;
            Require(game.IsRunActive && !game.IsGameOver && game.CurrentRoom == 0, "retry starts first room");
            Require(RuntimeSceneObjects.FindAll<PlayerController>().Length == 1 && RuntimeSceneObjects.FindAll<GameAudioController>().Length == 1,
                "retry has one player and one audio controller");
            Require(data.totalRuns == runs && data.victories == victories, "starting retry does not settle another run");

            // Explicit post-victory lifecycle fixture, NOT another combat clear:
            // invoke the game's defeat event to test accounting/idempotence.
            game.PlayerDied();
            Require(game.IsGameOver && !game.IsVictory && data.totalRuns == runs + 1 && data.victories == victories,
                "defeat event records a loss without an extra victory");
            var lossShards = data.legacyShards;
            var beforeLoss = JsonUtility.FromJson<SaveSlotData>(File.ReadAllText(Path.Combine(game.SaveSlots.SaveDirectory, "save1.json.bak")));
            Require(beforeLoss.totalRuns == runs && beforeLoss.legacyShards == shards,
                "defeat reward and counters persisted as one atomic snapshot");
            game.PlayerDied();
            Require(data.totalRuns == runs + 1 && data.legacyShards == lossShards && data.totalKills == kills, "duplicate defeat event cannot duplicate reward/statistics");
            var saved = new Proof
            {
                runs = data.totalRuns,
                victories = data.victories,
                kills = data.totalKills,
                shards = data.legacyShards,
                campaignVictories = data.campaignVictories,
                bestCampaignStage = data.bestCampaignStage,
                retryMaxHealth = game.Player.MaxHealth,
                firstProcess = System.Diagnostics.Process.GetCurrentProcess().Id
            };
            Require(File.Exists(Path.Combine(game.SaveSlots.SaveDirectory, "save1.json")) &&
                File.Exists(Path.Combine(game.SaveSlots.SaveDirectory, "save1.json.bak")), "save and recovery backup exist on disk");
            Require(!File.Exists(Path.Combine(game.SaveSlots.SaveDirectory, "save1.json.tmp")), "atomic save leaves no temporary file");
            Require(!game.SaveSlots.GetSummary(2).Exists && !game.SaveSlots.GetSummary(3).Exists, "unopened slots remain empty");
            Require(!File.Exists(proofPath), "do not overwrite an earlier lifecycle proof");
            File.WriteAllText(proofPath, JsonUtility.ToJson(saved, true));
            game.ReturnToMainMenu();
            for (var i = 0; i < 3; i++)
                yield return null;
            Require(!game.IsRunActive && game.SaveSlots.ActiveSlot == 0, "first process leaves the main menu cleanly");
        }

        private void VerifyReload(Proof proof)
        {
            Require(game.SaveSlots.ActiveData.totalRuns == proof.runs && game.SaveSlots.ActiveData.totalKills == proof.kills &&
                game.MetaProgression.LegacyShards == proof.shards &&
                game.SaveSlots.ActiveData.campaignVictories == proof.campaignVictories && game.SaveSlots.ActiveData.bestCampaignStage == proof.bestCampaignStage,
                "loaded run preserves progression and counters");
            Require(Mathf.Approximately(game.Player.MaxHealth, proof.retryMaxHealth), "relaunch applies persistent bonus exactly once");
            Require(RuntimeSceneObjects.FindAll<PlayerController>().Length == 1 && RuntimeSceneObjects.FindAll<GameAudioController>().Length == 1,
                "reopened slot has no duplicate player/audio");
        }
        private void Require(bool condition, string description)
        {
            if (!condition)
                throw new InvalidOperationException(description);
            checks++;
            Debug.Log("[Save Lifecycle] OK " + description);
        }
    }
}
