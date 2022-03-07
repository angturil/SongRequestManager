using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            numRequestsInQueue--;
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
