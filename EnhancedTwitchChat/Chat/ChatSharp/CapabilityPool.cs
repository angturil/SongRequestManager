using System.Linq;
using System.Collections.Generic;

namespace ChatSharp
{
    /// <summary>
    /// A list of capabilities supported by the library, along with the enabled and disabled
    /// capabilities after negotiating with the server.
    /// </summary>
    public class CapabilityPool : IEnumerable<IrcCapability>
    {
        private List<IrcCapability> Capabilities { get; set; }

        internal CapabilityPool()
        {
            Capabilities = new List<IrcCapability>();
        }

        /// <summary>
        /// Gets the IrcCapability with the specified name.
        /// </summary>
        public IrcCapability this[string name]
        {
            get
            {
                var cap = Capabilities.FirstOrDefault(c => c.Name == name);
                if (cap == null)
                    throw new KeyNotFoundException();
                return cap;
            }
        }

        internal void Add(string name)
        {
            if (Capabilities.Any(cap => cap.Name == name))
                return;

            Capabilities.Add(new IrcCapability(name));
        }

        internal void AddRange(IEnumerable<string> range)
        {
            foreach (string item in range)
                Add(item);
        }

        internal void Remove(string name)
        {
            Capabilities.Remove(this[name]);
        }

        /// <summary>
        /// Enables the specified capability.
        /// </summary>
        internal void Enable(string name) {
            if (Capabilities.Any(cap => cap.Name == name && cap.IsEnabled) || !Capabilities.Any(cap => cap.Name == name)) 
                return;
            
            this[name].IsEnabled = true;
        }

        /// <summary>
        /// Disables the specified capability.
        /// </summary>
        internal void Disable(string name)
        {
            if (Capabilities.Any(cap => cap.Name == name && !cap.IsEnabled) || !Capabilities.Any(cap => cap.Name == name)) 
                return;

            this[name].IsEnabled = false;
        }

        /// <summary>
        /// Checks if the specified capability is enabled.
        /// </summary>
        internal bool IsEnabled(string name)
        {
            return Capabilities.Any(cap => cap.Name == name && cap.IsEnabled);
        }

        /// <summary>
        /// Gets a list of enabled capabilities after negotiating capabilities with the server.
        /// </summary>
        /// <returns></returns>
        internal IEnumerable<string> Enabled
        {
            get
            {
                return Capabilities.Where(cap => cap.IsEnabled).Select(x => x.Name);
            }
        }

        /// <summary>
        /// Gets a list of disabled capabilities after negotiating capabilities with the server.
        /// </summary>
        /// <returns></returns>
        internal IEnumerable<string> Disabled
        {
            get
            {
                return Capabilities.Where(cap => cap.IsEnabled).Select(x => x.Name);
            }
        }

        internal bool Contains(string name)
        {
            return Capabilities.Any(cap => cap.Name == name);
        }

        internal IrcCapability Get(string name)
        {
            if (Contains(name))
                return this[name];
            throw new KeyNotFoundException();
        }

        internal IrcCapability GetOrAdd(string name)
        {
            if (Contains(name))
                return this[name];

            Add(name);
            return this[name];
        }

        /// <summary>
        /// Enumerates over the capabilities in this collection.
        /// </summary>
        public IEnumerator<IrcCapability> GetEnumerator()
        {
            return Capabilities.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
