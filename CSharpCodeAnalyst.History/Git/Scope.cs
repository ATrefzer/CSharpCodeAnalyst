using System.Collections;
using System.Diagnostics;

namespace CSharpCodeAnalyst.History.Git
{
    public sealed class Scope : IEnumerable<KeyValuePair<string, Guid>>
    {
        private readonly Dictionary<string, Guid> _serverPathToId = new Dictionary<string, Guid>();
        private readonly Dictionary<Guid, string> _idToServerPath = new Dictionary<Guid, string>();

        public Scope(Dictionary<string, Guid> seed)
        {
            foreach (var pair in seed)
            {
                _serverPathToId.Add(pair.Key, pair.Value);
                _idToServerPath.Add(pair.Value, pair.Key);
            }
        }

        public Scope(IEnumerable<KeyValuePair<string, Guid>> seed)
        {
            foreach (var pair in seed)
            {
                _serverPathToId.Add(pair.Key, pair.Value);
                _idToServerPath.Add(pair.Value, pair.Key);
            }
        }

        public Scope()
        {
        }

        public Scope Clone()
        {
            // Note: We do not clone the hashsets. Instead we build new ones on the merge commit scope
            var clone = new Scope();
            foreach (var pair in _serverPathToId)
            {
                clone._serverPathToId.Add(pair.Key, pair.Value);
            }

            foreach (var pair in _idToServerPath)
            {
                clone._idToServerPath.Add(pair.Key, pair.Value);
            }

            return clone;
        }

        public HashSet<string> GetAllFiles()
        {
            return new HashSet<string>(_serverPathToId.Keys);
        }
        

        public void Update(string fromServerPath, string toServerPath)
        {
            if (_serverPathToId.TryGetValue(fromServerPath, out var id) is false)
            {
                throw new Exception($"Tracking renaming but from path '{fromServerPath}' is not available");
            }

            // If the target path is already tracked (scope drift) remove the stale entry
            // from both maps first. Otherwise Add would throw.
            Debug.Assert(_serverPathToId.ContainsKey(toServerPath) is false);
            Remove(toServerPath);

            _serverPathToId.Remove(fromServerPath);
            _serverPathToId.Add(toServerPath, id);

            // Keep the reverse map in sync. Otherwise GetServerPath returns the old path
            // after a rename.
            _idToServerPath[id] = toServerPath;
        }

        public void Remove(string serverPath)
        {
            if (serverPath == null)
            {
                return;
            }

            if (_serverPathToId.TryGetValue(serverPath, out var id))
            {
                _serverPathToId.Remove(serverPath);
                _idToServerPath.Remove(id);
            }
        }

        public bool IsKnown(string servePath)
        {
            return _serverPathToId.ContainsKey(servePath);
        }

        public bool IsKnown(Guid id)
        {
            return _idToServerPath.ContainsKey(id);
        }

        public bool IsKnown(KeyValuePair<string, Guid> file) 
        {
            if (_serverPathToId.TryGetValue(file.Key, out var id))
            {
                return id == file.Value;
            }

            return false;
        }


        public string? GetIdOrDefault(string serverPath)
        {
            if (_serverPathToId.TryGetValue(serverPath, out var guid))
            {
                return guid.ToString();
            }

            return null;
        }

        public string GetId(string serverPath)
        {
            return _serverPathToId[serverPath].ToString();
        }

        public string Add(string serverPath)
        {
            // If the path is already tracked (scope drift) remove it from both maps.
            // Removing from the forward map alone would leave a stale reverse entry.
            Debug.Assert(_serverPathToId.ContainsKey(serverPath) is false);
            Remove(serverPath);

            var newId = Guid.NewGuid();
            _serverPathToId.Add(serverPath, newId);
            _idToServerPath.Add(newId, serverPath);
            
            return newId.ToString();
        }

        /// <summary>
        /// Adds a file to the scope with an already known id
        /// </summary>
        public void MergeAdd(string serverPath, Guid id)
        {
            _serverPathToId.Add(serverPath, id);
            _idToServerPath.Add(id, serverPath);
            
            // Can remove in release build
            VerifyScope(); 
        }

        private void VerifyScope()
        {
            Debug.Assert(_serverPathToId.Values.Distinct().Count() == _serverPathToId.Count);
        }

        public IEnumerator<KeyValuePair<string, Guid>> GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, Guid>>) _serverPathToId).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public string GetServerPath(Guid id)
        {
            return _idToServerPath[id];
        }

        public string? GetServerPathOrDefault(Guid id)
        {
            if (_idToServerPath.TryGetValue(id, out var serverPath))
            {
                return serverPath;
            }

            return null;
        }
    }
}