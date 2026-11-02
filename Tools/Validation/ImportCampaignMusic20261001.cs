using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

public static class ImportCampaignMusic20261001
{
    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play Mode before import.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var results = new JArray();
        for (var stage = 1; stage <= 3; stage++)
        foreach (var kind in new[] { "music", "ambience" })
        {
            var path = "Assets/RuinRunner/Resources/Audio/Campaign/stage" + stage + "-" + kind + ".wav";
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var sample = importer.defaultSampleSettings;
            var before = sample.loadType.ToString();
            sample.loadType = AudioClipLoadType.Streaming;
            sample.compressionFormat = AudioCompressionFormat.Vorbis;
            sample.quality = .85f;
            sample.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            sample.preloadAudioData = true;
            importer.defaultSampleSettings = sample;
            importer.forceToMono = false;
            importer.loadInBackground = true;
            importer.SaveAndReimport();
            importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null || clip.channels != 2 || clip.frequency != 44100 || clip.loadType != AudioClipLoadType.Streaming)
                throw new Exception("Invalid imported clip: " + path);
            results.Add(new JObject { { "path", path }, { "guid", AssetDatabase.AssetPathToGUID(path) },
                { "beforeLoadType", before }, { "loadType", clip.loadType.ToString() },
                { "channels", clip.channels }, { "sampleRate", clip.frequency }, { "length", clip.length },
                { "compression", importer.defaultSampleSettings.compressionFormat.ToString() },
                { "quality", importer.defaultSampleSettings.quality }, { "background", importer.loadInBackground } });
        }
        Directory.CreateDirectory("Logs/Validation/2026-10-01-campaign-music");
        File.WriteAllText("Logs/Validation/2026-10-01-campaign-music/import.json", results.ToString(), new System.Text.UTF8Encoding(false));
        return results.ToString();
    }
}
