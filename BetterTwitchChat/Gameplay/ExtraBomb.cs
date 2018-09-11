using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BetterTwitchChat.Gameplay {
    class ExtraBomb : MonoBehaviour {
        NoteController _note = null;
        public void Init(BeatmapObjectSpawnController spawner, NoteController note) {
            _note = note;
            var newNote = new NoteData(note.noteData.id, note.noteData.time, note.noteData.lineIndex, note.noteData.noteLineLayer + 2, note.noteData.noteLineLayer + 2, NoteType.Bomb, NoteCutDirection.Any, note.noteData.timeToNextBasicNote, note.noteData.timeToPrevBasicNote);
            spawner.NoteSpawnCallback(newNote);
        }

        void Update() {
            if (_note && _note.transform.position.z < -5.0f) {
                Plugin.Log("Destroying note!");
                _note.Dissolve(0); //Delete old note
                GameObject.Destroy(_note);
                GameObject.Destroy(this);
            }
        }
    }
}
