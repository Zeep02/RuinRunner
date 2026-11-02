using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RuinRunner
{
    public sealed partial class AutomatedStageValidation
    {
        readonly HashSet<string> inspectedStages=new HashSet<string>();
        int inspectionStep;
        float inspectionUntil;
        string inspectionKey;
        // Real map/inventory keys at a safe point let the performance recorder
        // include their creation and close transitions during normal journeys.
        bool DriveInputObservations()
        {
            if(!backendJourney||game.Player==null||!game.IsExpedition||game.IsGameOver||game.IsStageTransitioning||
                game.IsEncounterTransitioning||game.IsServiceOpen||game.IsChoosingAccessory||game.EnemiesAlive>0)return false;
            string key=game.BackendRun?.runId+":"+game.CurrentStageIndex;
            if(inspectionStep==0)
            {
                if(inspectedStages.Contains(key)||game.IsPaused)return false;
                inspectionKey=key;inspectionStep=1;
            }
            StageValidationInput.ReleaseAll();attackHeld=false;currentIntent="inspect map and inventory with normal input";
            if(Time.unscaledTime<inspectionUntil)return true;
            switch(inspectionStep)
            {
                case 1:
                    if(!game.IsMapOpen){Tap(GameAction.Map);return true;}
                    CaptureInputObservation("map");inspectionUntil=Time.unscaledTime+.8f;inspectionStep=2;break;
                case 2:
                    if(game.IsMapOpen){Tap(GameAction.Map);return true;}
                    inspectionStep=3;break;
                case 3:
                    if(!game.IsInventoryOpen){Tap(GameAction.Inventory);return true;}
                    CaptureInputObservation("inventory");inspectionUntil=Time.unscaledTime+.8f;inspectionStep=4;break;
                case 4:
                    if(game.IsInventoryOpen){Tap(GameAction.Inventory);return true;}
                    inspectedStages.Add(inspectionKey);inspectionStep=0;break;
            }
            return true;
        }
        void CaptureInputObservation(string panel)
        {
            Debug.Log("[Normal input] "+panel+" stage="+(game.CurrentStageIndex+1)+" run="+game.BackendRun?.runId);
            if(captureScreenshots&&!string.IsNullOrEmpty(captureDirectory))
                ScreenCapture.CaptureScreenshot(Path.Combine(captureDirectory,"normal-"+panel+"-"+inspectedStages.Count+".png"));
        }
    }
}
