using System;
using System.Collections.Generic;
using System.Linq;

namespace ChatSharp
{
    /// <summary>
    /// A filtered view of the user pool.
    /// </summary>
    public class UserPoolView : IEnumerable<IrcUser>
    {
        private IEnumerable<IrcUser> Users { get; set; }

        internal UserPoolView(IEnumerable<IrcUser> users)
        {
            Users = users;
        }

        /// <summary>
        /// Gets the IrcUser with the specified nick.
        /// </summary>
        public IrcUser this[string nick]
        {
            get
            {
                var user = Users.FirstOrDefault(u => u.Nick == nick);
                if (user == null)
                    throw new KeyNotFoundException();
                return user;
            }
        }

        internal IrcUser this[int index]
        {
            get
            {
                return Users.ToList()[index];
            }
        }

        /// <summary>
        /// Returns true if any user in the pool matches this mask. Note that not all users
        /// in the user pool will be fully populated, even if you set ClientSettings.WhoIsOnJoin 
        /// to true (it takes time to whois everyone in your channels).
        /// </summary>
        public bool ContainsMask(string mask)
        {
            return Users.Any(u => u.Match(mask));
        }

        /// <summary>
        /// Returns true if any user in the pool has the specified nick.
        /// </summary>
        public bool Contains(string nick)
        {
            return Users.Any(u => u.Nick == nick);
        }

        /// <summary>
        /// Returns true if the given IrcUser is in the pool.
        /// </summary>
        public bool Contains(IrcUser user)
        {
            return Users.Any(u => u.Hostmask == user.Hostmask);
        }

        /// <summary>
        /// Enumerates over the users in this collection (with the filter applied).
        /// </summary>
        public IEnumerator<IrcUser> GetEnumerator()
        {
            return Users.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}

