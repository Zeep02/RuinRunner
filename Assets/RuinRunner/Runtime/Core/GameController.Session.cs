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
            if (legacyFixture || !ResultSettlementConfirmed || projectedSettlementRunId == settlementRunId)
                return;
            LastMetaReward = checked((int)BackendRun.legacyReward);
            MetaProgression = new MetaProgressionService(SaveSlots);
            projectedSettlementRunId = settlementRunId;
            SessionMessage = "";
        }

        public void RequestProfileReset(int slot)
        {
            if (IsRunActive || SessionOperationPending || BackendBusy || BackendHasPending ||
                !SaveSlots.GetSummary(slot).IsServerProfile)
                return;
            backendFlow?.CancelReset();
            ResetProfileSlot = slot;
            SessionMessage = "";
        }

        public async void PrepareProfileReset()
        {
            if (ResetProfileSlot == 0 || IsRunActive || SessionOperationPending || BackendBusy)
                return;
            int epoch = sessionEpoch;
            int slot = ResetProfileSlot;
            SessionOperationPending = true;
            try
            {
                if (await backendFlow.BeginResetAsync(slot) && SessionMatches(epoch) && ResetProfileSlot == slot)
                    SessionMessage = "";
            }
            finally
            {
                SessionOperationPending = false;
            }
        }

        public async void ConfirmProfileReset()
        {
            if (!ResetProfileChallengeReady || IsRunActive || SessionOperationPending || BackendBusy)
                return;
            int epoch = sessionEpoch;
            var challenge = backendFlow.ResetChallenge;
            SessionOperationPending = true;
            try
            {
                if (await backendFlow.ConfirmResetAsync(challenge) && SessionMatches(epoch))
                {
                    ResetProfileSlot = 0;
                    SessionMessage = "";
                }
            }
            finally
            {
                SessionOperationPending = false;
            }
        }

        public void CancelProfileReset()
        {
            if (SessionOperationPending || BackendBusy)
                return;
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
