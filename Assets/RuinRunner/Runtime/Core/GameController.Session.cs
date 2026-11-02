using System;
using RuinRunner.Backend;
using UnityEngine;

namespace RuinRunner
{
    public sealed partial class GameController
    {
        int sessionEpoch;
        bool sessionDisposed;
        string settlementRunId;
        string projectedSettlementRunId;
        public int ResetProfileSlot { get; private set; }
        public bool ResetProfileChallengeReady => backendFlow?.ResetChallenge != null &&
            backendFlow.ResetChallenge.slot == ResetProfileSlot;
        public bool ResultSettlementConfirmed => legacyFixture ||
            IsGameOver && !string.IsNullOrEmpty(settlementRunId) &&
            BackendRun?.runId == settlementRunId && BackendRun.status != "active" && !BackendHasPending;

        bool SessionMatches(int epoch, string profileId = null, string runId = null) =>
            this != null && !sessionDisposed && epoch == sessionEpoch &&
            (profileId == null || BackendProfile?.profileId == profileId) &&
            (runId == null || BackendRun?.runId == runId);

        void RefreshSettlementProjection()
        {
            if (legacyFixture || !ResultSettlementConfirmed || projectedSettlementRunId == settlementRunId) return;
            LastMetaReward = checked((int)BackendRun.legacyReward);
            MetaProgression = new MetaProgressionService(SaveSlots);
            projectedSettlementRunId = settlementRunId;
            SessionMessage = "원정 정산이 서버에 저장되었습니다.";
        }

        public void RequestProfileReset(int slot)
        {
            if (IsRunActive || SessionOperationPending || BackendBusy || BackendHasPending ||
                !SaveSlots.GetSummary(slot).IsServerProfile) return;
            backendFlow?.CancelReset();
            ResetProfileSlot = slot;
            SessionMessage = "이 슬롯의 유산·해금·원정 기록을 초기화합니다.";
        }

        public async void PrepareProfileReset()
        {
            if (ResetProfileSlot == 0 || IsRunActive || SessionOperationPending || BackendBusy) return;
            int epoch = sessionEpoch;
            int slot = ResetProfileSlot;
            SessionOperationPending = true;
            try
            {
                if (await backendFlow.BeginResetAsync(slot) && SessionMatches(epoch) && ResetProfileSlot == slot)
                    SessionMessage = "확인되었습니다. 아래 최종 초기화 버튼으로 확정하세요.";
            }
            finally { SessionOperationPending = false; }
        }

        public async void ConfirmProfileReset()
        {
            if (!ResetProfileChallengeReady || IsRunActive || SessionOperationPending || BackendBusy) return;
            int epoch = sessionEpoch;
            var challenge = backendFlow.ResetChallenge;
            SessionOperationPending = true;
            try
            {
                if (await backendFlow.ConfirmResetAsync(challenge) && SessionMatches(epoch))
                {
                    ResetProfileSlot = 0;
                    SessionMessage = "프로필을 초기화했습니다. 마을에서 다시 시작할 수 있습니다.";
                }
            }
            finally { SessionOperationPending = false; }
        }

        public void CancelProfileReset()
        {
            if (SessionOperationPending || BackendBusy) return;
            backendFlow?.CancelReset();
            ResetProfileSlot = 0;
        }

        void OnDestroy()
        {
            sessionDisposed = true;
            sessionEpoch++;
            townArtLifetime.Cancel();
            townArtLifetime.Dispose();
            DisposeBackend();
        }
    }
}
