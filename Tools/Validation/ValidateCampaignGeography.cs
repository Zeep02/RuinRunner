using System;
using UnityEngine;
public static class ValidateCampaignGeography
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Validate authored geography outside Play Mode");
        RuinRunner.EditorTools.StageLayoutValidation.Validate();
        return "Campaign geography completed for three stages and seeds 9100..9109; see Console for each seed. Not normal completion evidence.";
    }
}
