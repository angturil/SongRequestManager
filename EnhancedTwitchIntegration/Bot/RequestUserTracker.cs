using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SongRequestManager
{
    public class RequestUserTracker
    {
        public int numRequests = 0;
        public DateTime resetTime = DateTime.Now;
    }
}
