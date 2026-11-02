using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RuinRunner.Backend;
using UnityEngine;

namespace RuinRunner
{
    public sealed partial class AutomatedStageValidation
    {
        const string RecoveryOrigin = "http://127.0.0.1:5200/";
        static JObject authorizedRecoveryTicket;
        JObject recoveryTicket;
        BackendPrivateStore recoveryStore;
        readonly JArray recoveryChecks = new JArray();
        int recoveryStep;
        float recoveryActionAt;
        long recoveryExpectedLegacy;
        int recoveryExpectedVictories;

        // This opt-in is only a validation exception to the fresh-profile guard.
        // It never changes saves, grants inventory, or bypasses authentication.
        public static bool AuthorizeRecoveryProfile(SaveSlotService slots)
        {
            authorizedRecoveryTicket = null;
            try
            {
                RecoveryRequire(slots != null && slots.IsValidationProfile);
                var args = Environment.GetCommandLineArgs();
                RecoveryRequire(args.Count(x => x == "--validation-recovery-ticket") == 1 &&
                    args.Count(x => x == "--validation-profile-name") == 1);
                var name = GetCommandLineValue("--validation-profile-name");
                RecoveryRequire(Regex.IsMatch(name ?? "", "^[A-Za-z0-9_-]{1,80}$"));
                var validation = Directory.GetParent(Directory.GetParent(slots.SaveDirectory).FullName).FullName;
                RecoveryRequire(string.Equals(Path.GetFullPath(slots.SaveDirectory),
                    Path.Combine(validation, name, "Saves"), StringComparison.OrdinalIgnoreCase));
                var workspace = Directory.GetParent(Directory.GetParent(validation).FullName).FullName;
                RecoveryRequire(string.Equals(Path.GetFileName(validation), "Validation", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Path.GetFileName(Path.GetDirectoryName(validation)), "Logs", StringComparison.OrdinalIgnoreCase));
                var path = RecoveryBounded(GetCommandLineValue("--validation-recovery-ticket"), validation);
                var ticket = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
                RecoveryRequire(ticket.Value<int>("schemaVersion") == 1 &&
                    ticket.Value<string>("purpose") == "isolated-windows-recovery-v1" &&
                    ticket.Value<string>("validationProfile") == name && ticket.Value<string>("apiOrigin") == RecoveryOrigin &&
                    Environment.GetEnvironmentVariable("RUINRUNNER_API_URL") == RecoveryOrigin);
                string action = ticket.Value<string>("action"), operation = ticket.Value<string>("operationId"), runId = ticket.Value<string>("runId");
                RecoveryRequire((action == "buy" || action == "settle") && Guid.TryParse(operation, out _) &&
                    Guid.TryParse(runId, out _) && Guid.TryParse(ticket.Value<string>("profileId"), out _));
                var exe = RecoveryBounded(ticket.Value<string>("executablePath"), Path.Combine(workspace, "Builds"));
                RecoveryRequire(Path.GetFileName(exe) == "RuinRunner.exe" &&
                    string.Equals(exe, Path.GetFullPath(args[0]), StringComparison.OrdinalIgnoreCase) &&
                    RecoveryHashMatches(exe, ticket.Value<string>("executableSha256")) &&
                    RecoveryHashMatches(Path.Combine(Application.dataPath, "Managed", "RuinRunner.Runtime.dll"), ticket.Value<string>("runtimeSha256")));
                var launchPath = RecoveryBounded(ticket.Value<string>("sourceLaunchPath"), validation);
                var stopPath = RecoveryBounded(ticket.Value<string>("sourceStopPath"), validation);
                var faultPath = RecoveryBounded(ticket.Value<string>("faultSnapshotPath"), validation);
                RecoveryRequire(Path.GetDirectoryName(path) == Path.GetDirectoryName(launchPath) &&
                    Path.GetDirectoryName(stopPath) == Path.GetDirectoryName(launchPath) &&
                    Path.GetDirectoryName(faultPath) == Path.GetDirectoryName(launchPath) &&
                    RecoveryHashMatches(launchPath, ticket.Value<string>("sourceLaunchSha256")) &&
                    RecoveryHashMatches(stopPath, ticket.Value<string>("sourceStopSha256")) &&
                    RecoveryHashMatches(faultPath, ticket.Value<string>("faultSnapshotSha256")));
                var launch = JObject.Parse(File.ReadAllText(launchPath, Encoding.UTF8));
                var stopped = JObject.Parse(File.ReadAllText(stopPath, Encoding.UTF8));
                var frozen = JObject.Parse(File.ReadAllText(faultPath, Encoding.UTF8));
                var proxyPath = RecoveryBounded(ticket.Value<string>("proxyStatePath"), Path.Combine(workspace, "Backend", "Validation", "FaultProxy"));
                var proxy = JObject.Parse(File.ReadAllText(proxyPath, Encoding.UTF8));
                var launchArgs = launch["args"].Values<string>().ToArray();
                RecoveryRequire(launch.Value<bool>("freshValidationProfile") && launch.Value<string>("api") == RecoveryOrigin &&
                    launch.Value<string>("stopAfterResponseDrop") == action && launch.Value<string>("exe") == ticket.Value<string>("executablePath") &&
                    launchArgs.Count(x => x == "--validation-profile-name") == 1 &&
                    launchArgs[Array.IndexOf(launchArgs, "--validation-profile-name") + 1] == name &&
                    !launchArgs.Contains("--validation-recovery-ticket") &&
                    stopped.Value<bool>("stoppedByLauncher") && stopped.Value<int>("pid") == launch.Value<int>("pid") &&
                    stopped.Value<string>("sourceProcessStartUtc") == launch.Value<string>("processStartUtc") &&
                    stopped.Value<string>("operationId") == operation);
                RecoveryRequire(frozen.Value<string>("listen") == RecoveryOrigin &&
                    frozen.Value<string>("upstream") == "http://127.0.0.1:5197/" && frozen.Value<bool>("faultsEnabled") &&
                    frozen["dropped"].Value<string>(action) == operation && proxy["dropped"].Value<string>(action) == operation &&
                    frozen.Value<string>("startedUtc") == proxy.Value<string>("startedUtc"));
                var fault = frozen["operations"].FirstOrDefault(x => x.Value<string>("operationId") == operation && x.Value<string>("action") == action);
                RecoveryRequire(fault != null && fault.Value<int>("droppedResponses") == 1 && fault["upstreamStatuses"].Values<int>().Contains(200));
                var storeRoot = Path.Combine(slots.SaveDirectory, "BackendRecovery");
                var store = new BackendPrivateStore(storeRoot);
                RecoveryRequire(RecoveryHashMatches(RecoveryRecordPath(storeRoot, "operations", operation), ticket.Value<string>("operationJournalSha256")));
                var command = store.Read<PendingOperation>("operations", operation);
                RecoveryRequire(command != null && command.status == "pending" && command.apiOrigin == RecoveryOrigin && command.operationId == operation &&
                    command.method == "POST" && command.path == "v1/runs/" + runId + (action == "buy" ? "/trades" : "/settle"));
                var payload = JObject.Parse(command.payload);
                RecoveryRequire(payload.Value<string>("operationId") == operation && (action == "buy" ?
                    payload.Value<string>("action") == "buy" : payload.Value<string>("outcome") == "victory" && payload.Value<int>("stagesCleared") == 3));
                var run = store.Read<RunView>("runs", RecoveryOrigin + ":" + runId);
                var profile = store.Read<ProfileView>("profiles", RecoveryOrigin + ":" + ticket.Value<string>("profileId"));
                RecoveryRequire(run != null && run.runId == runId && run.status == "active" && run.profileId == ticket.Value<string>("profileId") &&
                    profile != null && profile.profileId == run.profileId && profile.slot == 1 && profile.playerId == command.playerId &&
                    profile.state.totalRuns == 0 && profile.state.legacyMigrationVersion == 0 &&
                    profile.legacyShards == ticket.Value<long>("beforeLegacy") && profile.state.totalRuns == ticket.Value<int>("beforeTotalRuns") &&
                    profile.state.campaignVictories == ticket.Value<int>("beforeCampaignVictories"));
                if (action == "buy")
                {
                    RecoveryRequire(RecoveryHashMatches(RecoveryRecordPath(storeRoot, "economy-queue", RecoveryOrigin + ":" + operation), ticket.Value<string>("queueJournalSha256")));
                    var queue = store.Read<QueuedEconomyOperation>("economy-queue", RecoveryOrigin + ":" + operation);
                    RecoveryRequire(queue != null && queue.status == "pending" && queue.apiOrigin == RecoveryOrigin && queue.runId == runId && queue.operationId == operation);
                }
                authorizedRecoveryTicket = ticket;
                return true;
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                // Never log decrypted command bodies, profile data, or credentials.
                Debug.LogError("[Recovery E2E] Ticket rejected by isolated ownership/pending checks.");
                return false;
            }
        }

        static void RecoveryRequire(bool value) { if (!value) throw new InvalidOperationException("Recovery evidence mismatch."); }
        static string RecoveryBounded(string path, string root)
        {
            var full = Path.GetFullPath(path);
            RecoveryRequire(full.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            return full;
        }
        static bool RecoveryHashMatches(string path, string sha) =>
            !string.IsNullOrEmpty(sha) && string.Equals(BackendPrivateStore.Hash(File.ReadAllBytes(path)), sha, StringComparison.OrdinalIgnoreCase);
        static string RecoveryRecordPath(string root, string category, string key) =>
            Path.Combine(root, category, BackendPrivateStore.Hash(Encoding.UTF8.GetBytes(key)) + ".dpapi");

        bool InitializeRecoveryMode()
        {
            recoveryTicket = authorizedRecoveryTicket;
            authorizedRecoveryTicket = null;
            if (recoveryTicket == null) return false;
            recoveryStore = new BackendPrivateStore(Path.Combine(game.SaveSlots.SaveDirectory, "BackendRecovery"));
            recoveryExpectedLegacy = recoveryTicket.Value<long>("beforeLegacy");
            recoveryExpectedVictories = recoveryTicket.Value<int>("beforeCampaignVictories");
            recoveryChecks.Add("original owned process stopped after upstream success; matching UUID remained pending before reconnect");
            Debug.Log("[Recovery E2E] START action=" + recoveryTicket.Value<string>("action") + "; reconnect through menu/profile handlers, separate from normal campaign proof");
            return true;
        }

        void DriveRecoveryJourney()
        {
            if (Time.unscaledTime - startedAt > 180f) { FinishRecovery(false, "recovery timeout at step " + recoveryStep); return; }
            if (game.SessionOperationPending || game.BackendBusy || game.EconomyOperationPending || Time.unscaledTime < recoveryActionAt)
            { StageValidationInput.ReleaseAll(); return; }
            if (game.BackendHasPending || recoveryStep > 0 && !game.BackendReady)
            { StageValidationInput.ReleaseAll(); game.RetrySessionConnection(); recoveryActionAt = Time.unscaledTime + 3; return; }
            try
            {
                switch (recoveryStep)
                {
                    case 0:
                        game.OpenSaveSlots(); RecoveryNext(1); break;
                    case 1:
                        game.StartSlot(1); RecoveryNext(2); break;
                    case 2:
                        RecoveryRequire(game.IsTown && game.BackendProfile?.profileId == recoveryTicket.Value<string>("profileId"));
                        VerifyRecoveryReceipt();
                        RecoveryRequire(RecoveryTownInventoryOnly());
                        recoveryChecks.Add("same server profile entered town; old expedition item/heal state was not injected");
                        RecoveryCapture("reconnected-town");
                        RecoveryNext(recoveryTicket.Value<string>("action") == "buy" ? 3 : 5); break;
                    case 3:
                        if (!RecoveryWalkToGate()) break;
                        game.ResolveInterruptedExpedition();
                        recoveryChecks.Add("walked to departure gate and invoked the production interrupted-expedition cleanup button");
                        RecoveryNext(4); break;
                    case 4:
                        RecoveryRequire(!game.BackendHasUnfinishedRun && game.BackendRun?.runId == recoveryTicket.Value<string>("runId") && game.BackendRun.status == "abandoned");
                        VerifyRecoveryTotals();
                        RecoveryRequire(RecoveryTownInventoryOnly());
                        recoveryChecks.Add("abandoned cleanup confirmed once; legacy unchanged; no pending work blocks next departure");
                        RecoveryCapture("cleanup-confirmed"); RecoveryNext(5); break;
                    case 5:
                        game.CloseService(); game.ReturnToMainMenu(); RecoveryNext(6); break;
                    case 6:
                        game.OpenSaveSlots(); RecoveryNext(7); break;
                    case 7:
                        game.StartSlot(1); RecoveryNext(8); break;
                    case 8:
                        RecoveryRequire(game.IsTown && !game.BackendHasUnfinishedRun && !game.BackendHasPending && RecoveryTownInventoryOnly());
                        VerifyRecoveryTotals();
                        recoveryChecks.Add("second authoritative profile selection kept the same legacy/run/victory totals and no active prior run");
                        VerifyRecoveryProxy();
                        RecoveryCapture("verified-town");
                        FinishRecovery(true, "pending UUID replayed and authoritative town state confirmed without combat resume"); break;
                }
            }
            catch (Exception e) when (!(e is OutOfMemoryException)) { FinishRecovery(false, "recovery assertion failed at step " + recoveryStep); }
        }

        void RecoveryNext(int next) { recoveryStep = next; recoveryActionAt = Time.unscaledTime + .5f; StageValidationInput.ReleaseAll(); }
        bool RecoveryTownInventoryOnly()
        {
            var items = game.Inventory.AllEquipment().ToArray();
            return items.Length == 1 && items[0].ServerInstanceId == "town-practice" && items[0].IsStartingEquipment;
        }
        void VerifyRecoveryReceipt()
        {
            var operation = recoveryTicket.Value<string>("operationId");
            var runId = recoveryTicket.Value<string>("runId");
            var command = recoveryStore.Read<PendingOperation>("operations", operation);
            RecoveryRequire(command != null && command.status == "confirmed" && command.operationId == operation && command.apiOrigin == RecoveryOrigin);
            if (recoveryTicket.Value<string>("action") == "buy")
            {
                var queue = recoveryStore.Read<QueuedEconomyOperation>("economy-queue", RecoveryOrigin + ":" + operation);
                var receipt = JsonConvert.DeserializeObject<EconomyReceipt>(command.responseJson);
                RecoveryRequire(queue?.status == "confirmed" && queue.receipt?.operationId == operation &&
                    receipt?.operationId == operation && receipt.economy?.runId == runId &&
                    game.BackendRun?.runId == runId && game.BackendHasUnfinishedRun &&
                    game.BackendEconomy?.revision == receipt.economy.revision && game.BackendEconomy.gold == receipt.economy.gold);
                foreach (var granted in receipt.grantedItems ?? Array.Empty<OwnedItem>())
                    RecoveryRequire(game.BackendEconomy.items.Count(x => x.instanceId == granted.instanceId) == 1);
                recoveryChecks.Add("purchase UUID confirmed; durable queue acknowledged; authoritative prior-run gold/revision match receipt and each granted instance occurs once");
            }
            else
            {
                var receipt = JsonConvert.DeserializeObject<SettlementReceipt>(command.responseJson);
                RecoveryRequire(receipt?.run?.runId == runId && receipt.run.status == "victory" &&
                    receipt.profile?.profileId == recoveryTicket.Value<string>("profileId") && !game.BackendHasUnfinishedRun);
                recoveryExpectedLegacy += receipt.run.legacyReward;
                recoveryExpectedVictories++;
                VerifyRecoveryTotals();
                RecoveryRequire(receipt.profile.legacyShards == game.BackendProfile.legacyShards && receipt.profile.state.totalRuns == game.BackendProfile.state.totalRuns);
                recoveryChecks.Add("victory settlement UUID confirmed; one reward and one run/victory increment observed in authoritative profile");
            }
        }
        void VerifyRecoveryTotals()
        {
            var profile = game.BackendProfile;
            RecoveryRequire(profile?.profileId == recoveryTicket.Value<string>("profileId") &&
                profile.legacyShards == recoveryExpectedLegacy && profile.state.totalRuns == recoveryTicket.Value<int>("beforeTotalRuns") + 1 &&
                profile.state.campaignVictories == recoveryExpectedVictories);
        }
        bool RecoveryWalkToGate()
        {
            RecoveryRequire(game.IsTown && game.Player != null);
            if (game.OpenService?.Kind == FieldServiceKind.Expedition) { ReleaseMovement(); return true; }
            var gate = RuntimeSceneObjects.FindAll<FieldServiceObject>().FirstOrDefault(x => x.Kind == FieldServiceKind.Expedition);
            RecoveryRequire(gate != null);
            Vector2 delta = (Vector2)gate.transform.position - (Vector2)game.Player.transform.position;
            if (delta.magnitude > 1.4f) SetMove(delta.normalized);
            else { ReleaseMovement(); Tap(GameAction.Interact); }
            return false;
        }
        void VerifyRecoveryProxy()
        {
            var state = JObject.Parse(File.ReadAllText(recoveryTicket.Value<string>("proxyStatePath"), Encoding.UTF8));
            var row = state["operations"].FirstOrDefault(x => x.Value<string>("operationId") == recoveryTicket.Value<string>("operationId"));
            RecoveryRequire(row != null && row.Value<int>("droppedResponses") == 1 && row.Value<int>("requests") == 2 &&
                row["upstreamStatuses"].Values<int>().SequenceEqual(new[] { 200, 200 }));
            recoveryChecks.Add("proxy observed exactly original request plus one same-UUID retry, both upstream 200, with one dropped response");
        }
        void RecoveryCapture(string name)
        {
            if (captureScreenshots && !Application.isBatchMode && !string.IsNullOrEmpty(captureDirectory))
                ScreenCapture.CaptureScreenshot(Path.Combine(captureDirectory, "recovery-" + name + ".png"));
        }
        void FinishRecovery(bool passed, string reason)
        {
            completionLogged = true; ReleaseMovement(); StageValidationInput.ReleaseAll();
            var report = new JObject {
                ["schemaVersion"] = 1, ["recoveryPassed"] = passed, ["normalCampaign"] = false,
                ["manualUiInput"] = false, ["cheatsOrSuppliedState"] = false, ["reason"] = reason,
                ["action"] = recoveryTicket.Value<string>("action"), ["operationId"] = recoveryTicket.Value<string>("operationId"),
                ["runId"] = recoveryTicket.Value<string>("runId"), ["profileId"] = recoveryTicket.Value<string>("profileId"),
                ["elapsedSeconds"] = Time.unscaledTime - startedAt, ["checks"] = recoveryChecks,
                ["legacy"] = game.BackendProfile?.legacyShards, ["totalRuns"] = game.BackendProfile?.state?.totalRuns,
                ["campaignVictories"] = game.BackendProfile?.state?.campaignVictories,
                ["resultOverlayRestored"] = false, ["combatResumeAttempted"] = false,
                ["confirmationSurface"] = "authoritative server profile selected into Town; previous result overlay is not restored"
            };
            if (!string.IsNullOrWhiteSpace(captureDirectory)) File.WriteAllText(Path.Combine(captureDirectory, "recovery-report.json"), report.ToString(Formatting.Indented), new UTF8Encoding(false));
            Debug.Log((passed ? "[Recovery E2E] PASS " : "[Recovery E2E] FAIL ") + reason);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--validation-quit") >= 0) Application.Quit(passed ? 0 : 2);
        }
    }
}
