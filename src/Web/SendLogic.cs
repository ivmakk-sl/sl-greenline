namespace Greenline
{
    // The send decisions that the page messages ask for. No call of the mod to the web UI asks for a result, so the
    // page tells the mod by a page message what it must act on, and PageMessages only sets a request here; the next
    // PotGrid.PushIfDirty reads it and sends. A full send (the page script with the pot data) runs at most once each
    // FullSendGapSeconds real seconds, so a page that keeps losing its script cannot cause a full send each second;
    // a request refused for that is not kept, because the page posts "no script" again at its next check. No game or
    // BepInEx type here, so the tests can link this file.
    public sealed class SendLogic
    {
        public const float FullSendGapSeconds = 5f;

        private bool fullRequested;
        private bool forgetRequested;
        private float lastFullAt = float.NegativeInfinity;

        // "no script": the root page has no page script.
        public void RequestFull() => fullRequested = true;

        // "no frame" or "check ...": the HUD plant list lost the grid, so the next check sends the pot data again.
        public void RequestForget() => forgetRequested = true;

        // Whether to send the page script with the pot data now (real seconds). Clears the request.
        public bool TakeFull(float now)
        {
            if (!fullRequested) return false;
            fullRequested = false;
            if (now - lastFullAt < FullSendGapSeconds) return false;
            lastFullAt = now;
            return true;
        }

        // Whether to forget the last push, so the pot data goes again. Clears the request.
        public bool TakeForget()
        {
            bool forget = forgetRequested;
            forgetRequested = false;
            return forget;
        }
    }
}
