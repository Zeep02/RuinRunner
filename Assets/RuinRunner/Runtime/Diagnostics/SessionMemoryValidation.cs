using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Profiling;

namespace RuinRunner
{
    /// <summary>Opt-in forced lifecycle fixture. Never a normal combat or performance result.</summary>
    public sealed class SessionMemoryValidation : MonoBehaviour
    {
        [Serializable]
        private sealed class Sample
        {
            public int cycle, objects, materials, textures, meshes, rooms;
            public long allocatedBytes, managedBytes;
        }
        [Serializable]
        private sealed class Report
        {
            public string policy, result;
            public Sample[] samples;
        }
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Sample> samples = new List<Sample>();
        private GameController game;
        private string folder;
        private IEnumerator sequence;
        private float deadline;
        private void Start()
        {
            Application.runInBackground = true;
            game = GetComponent<GameController>();
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "--validation-capture-dir");
            if (index < 0 || index + 1 >= args.Length || !game.SaveSlots.IsValidationProfile)
                throw new InvalidOperationException("Memory fixture requires isolated profile and output directory");
            folder = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(folder);
            deadline = Time.realtimeSinceStartup + 180;
            sequence = Run();
        }
        private void Update()
        {
            if (sequence == null)
                return;
            try
            {
                if (Time.realtimeSinceStartup > deadline)
                    throw new Exception("Lifecycle fixture timeout");
                if (sequence.MoveNext())
                    return;
                sequence = null;
                Write("PASS");
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                sequence = null;
                Write("FAIL: " + exception.Message);
                Debug.LogError(exception);
                Application.Quit(3);
            }
        }
        private const int LazyEffectTextures = 64;
        private IEnumerator Run()
        {
            // First three cycles warm caches. Compare the same menu state after all
            // deferred Destroy calls, with GC on every sample for comparable retention.
            for (var cycle = 0; cycle < 12; cycle++)
            {
                game.SaveSlots.OpenTransientForTests();
                typeof(GameController).GetMethod("BeginRunForActiveSlot", Private).Invoke(game, null);
                for (var stage = 0; stage < CampaignStage.Count; stage++)
                {
                    var until = Time.realtimeSinceStartup + 2.4f;
                    while (Time.realtimeSinceStartup < until)
                        yield return null;
                    if (game.CurrentStageIndex != stage || game.Player == null)
                        throw new Exception("Stage initialization failed");
                    game.Player.SetStageClearSafety(true);
                    if (stage < CampaignStage.Count - 1)
                        typeof(GameController).GetMethod("BeginNextStage", Private).Invoke(game, null);
                }
                game.ReturnToMainMenu();
                for (var frame = 0; frame < 12; frame++)
                    yield return null;
                if (RuntimeSceneObjects.FindAll<PlayerController>().Length != 0 || RuntimeSceneObjects.FindAll<EnemyController>().Length != 0 ||
                    RuntimeSceneObjects.FindAll<EnemySpawnTelegraph>().Length != 0 || RuntimeSceneObjects.FindAll<StageRuntimeOwnership>().Length != 0)
                    throw new Exception("Session actors/terrain survived return to menu");
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                var row = new Sample
                {
                    cycle = cycle + 1,
                    objects = RuntimeSceneObjects.FindAll<GameObject>().Length,
                    materials = Resources.FindObjectsOfTypeAll<Material>().Length,
                    textures = Resources.FindObjectsOfTypeAll<Texture2D>().Length,
                    meshes = Resources.FindObjectsOfTypeAll<Mesh>().Length,
                    rooms = Resources.FindObjectsOfTypeAll<RoomTemplate>().Length,
                    allocatedBytes = Profiler.GetTotalAllocatedMemoryLong(),
                    managedBytes = GC.GetTotalMemory(false)
                };
                samples.Add(row);
                Debug.Log("[Session Memory] " + JsonUtility.ToJson(row));
            }
            var baseline = samples[2];
            var last = samples[samples.Count - 1];
            // Effect frames load the first time a seed places or uses them and stay cached, so a few more textures keep arriving for many cycles.
            if (last.objects != baseline.objects || last.materials > baseline.materials + 1 || last.textures > baseline.textures + LazyEffectTextures ||
                last.meshes > baseline.meshes + 1 || last.rooms != baseline.rooms)
                throw new Exception("Retained Unity objects grow after warmup");
            if (last.managedBytes > baseline.managedBytes + 2 * 1024 * 1024 || last.allocatedBytes > baseline.allocatedBytes + 8 * 1024 * 1024)
                throw new Exception("Retained memory grew beyond bounded cache allowance");
        }
        private void Write(string result)
        {
            var report = new Report { result = result, samples = samples.ToArray(), policy = "12 forced lifecycle cycles, all three stage loads each. Transient saves, safety and forced stage transitions. Not normal gameplay. Same menu state after deferred destruction; forced managed GC per sample, no UnloadUnusedAssets. First three cycles warm caches; exact actor/object/room retention, at most one cached material/mesh/texture and 2MB managed/8MB Unity allocation growth thereafter." };
            if (folder != null)
                File.WriteAllText(Path.Combine(folder, "memory-lifecycle.json"), JsonUtility.ToJson(report, true));
            Debug.Log("[Session Memory] " + result);
        }
    }
}
