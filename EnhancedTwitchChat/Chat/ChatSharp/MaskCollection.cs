using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ChatSharp
{
    /// <summary>
    /// A collection of masks from a channel list.
    /// </summary>
    public class MaskCollection : IEnumerable<Mask>
    {
        internal MaskCollection()
        {
            Masks = new List<Mask>();
        }

        private List<Mask> Masks { get; set; }

        /// <summary>
        /// Adds a mask to the collection. This only modifies the local mask list, changes are
        /// not flushed to the server.
        /// </summary>
        public void Add(Mask mask)
        {
            Masks.Add(mask);
        }

        /// <summary>
        /// Removes a mask from the collection. This only modifies the local mask list, changes are
        /// not flushed to the server.
        /// </summary>
        public void Remove(Mask mask)
        {
            Masks.Remove(mask);
        }

        /// <summary>
        /// True if this collection includes the given mask.
        /// </summary>
        public bool Contains(Mask mask)
        {
            return Masks.Contains(mask);
        }

        /// <summary>
        /// True if this collection includes any masks that are equal to the given mask.
        /// </summary>
        public bool ContainsMask(Mask mask)
        {
            return Masks.Any(m => m.Value == mask.Value);
        }

        /// <summary>
        /// Returns the mask at the requested index.
        /// </summary>
        public Mask this[int index]
        {
            get
            {
                return Masks[index];
            }
        }

        /// <summary>
        /// True if any mask matches the given user.
        /// </summary>
        public bool ContainsMatch(IrcUser user)
        {
            return Masks.Any(m => user.Match(m.Value));
        }

        /// <summary>
        /// Returns the mask that matches the given user.
        /// </summary>
        public Mask GetMatch(IrcUser user)
        {
            var match = Masks.FirstOrDefault(m => user.Match(m.Value));
            if (match == null)
                throw new KeyNotFoundException("No mask matches the specified user.");
            return match;
        }

        /// <summary>
        /// Enumerates over the masks in this collection.
        /// </summary>
        public IEnumerator<Mask> GetEnumerator()
        {
            return Masks.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
