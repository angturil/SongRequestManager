using System;

namespace SongRequestManager
{
    public class RequestUserTracker
    {
        private int numRequestsInQueue = 0;
        private int numTotalRequests = 0;

        public void IncrementRequests() {
            numRequestsInQueue++;
            numTotalRequests++;
        }

        public void DecrementRequestsInQueue() {
            if (numRequestsInQueue > 0)
            {
                numRequestsInQueue--;
            }
        }

        public int GetNumRequestsInQueue() {
            return numRequestsInQueue;
        }

        public int GetNumTotalRequests() {
            return numTotalRequests;
        }

        public DateTime resetTime = DateTime.Now;
    }
}
