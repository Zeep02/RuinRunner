using UnityEngine;

namespace RuinRunner
{
    public sealed partial class PrototypeHud
    {
        void DrawProfileReset(float width, float height)
        {
            PrepareTownStyles();
            var panel=new Rect(width/2-330,height/2-180,660,360);
            DrawPanel(panel);
            GUI.Label(new Rect(panel.x+28,panel.y+22,604,44),"슬롯 "+game.ResetProfileSlot+" · 프로필 초기화",titleStyle);
            var summary=game.SaveSlots.GetSummary(game.ResetProfileSlot);
            GUI.Label(new Rect(panel.x+32,panel.y+84,596,92),
                "유산 "+summary.LegacyShards+" · 원정 "+summary.TotalRuns+"회 · 엔딩 "+summary.CampaignVictories+"회\n"+
                "유산·해금·원정 기록이 처음 상태로 돌아갑니다.\n초기화한 진행도는 게임에서 되돌릴 수 없습니다.",compactStyle);
            GUI.Label(new Rect(panel.x+32,panel.y+184,596,46),
                game.ResetProfileChallengeReady?"2/2 · 서버가 확인한 이 슬롯을 최종 초기화합니다.":
                "1/2 · 초기화할 슬롯을 확인한 뒤 계속하세요.",compactStyle);
            bool enabled=GUI.enabled;
            GUI.enabled=enabled&&!game.SessionOperationPending&&!game.BackendBusy;
            if(GUI.Button(new Rect(panel.x+32,panel.y+250,200,50),"취소",buttonStyle))game.CancelProfileReset();
            if(game.ResetProfileChallengeReady)
            {
                if(GUI.Button(new Rect(panel.x+248,panel.y+250,380,50),"이 슬롯의 기록 초기화",buttonStyle))game.ConfirmProfileReset();
            }
            else if(GUI.Button(new Rect(panel.x+248,panel.y+250,380,50),"슬롯 확인 후 계속",buttonStyle))game.PrepareProfileReset();
            GUI.enabled=enabled;
            GUI.Label(new Rect(panel.x+32,panel.y+312,596,28),game.BackendBusy?game.BackendStatus:
                !string.IsNullOrEmpty(game.BackendErrorCode)?game.BackendStatus:game.SessionMessage,compactStyle);
        }
    }
}
