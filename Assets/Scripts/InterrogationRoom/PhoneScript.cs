using UnityEngine;

namespace InterrogationRoom
{
    //The words every suspect says on the phone, kept in one place away from the scene. The four lines,
    //the explanation, and which clue breaks them are all here, so the whole script of the phone can be
    //rewritten by editing Assets/Resources/PhoneScript.json without opening Unity.
    //
    //Only the writing lives here. The portrait is a picture, so it stays on the suspect in the scene;
    //this is matched back to that suspect by name. An entry fills in over the scene field by field, so
    //a half written entry still falls back to whatever the suspect was authored with in the scene.
    public static class PhoneScript
    {
        //loaded from Resources by this name, so nothing has to be wired up for it to be found
        public const string ResourceName = "PhoneScript";

        //The lines the phone actually reads, once the document and the scene have been folded together.
        public class Lines
        {
            public string basic;
            public string nonsense;
            public string correct;
            public string[] explanation;
            public string correctClueKey;
            public string specialClueId;
            public string specialClue;
            public bool endsCase;
        }

        //The shapes below are what JsonUtility reads, so the field names are the JSON keys. Renaming one
        //silently empties that part of the file rather than failing, so they are left alone.
        [System.Serializable]
        public class Document
        {
            public Entry[] suspects;
        }

        [System.Serializable]
        public class Entry
        {
            //who this is for: the suspect's display name ("The Mayor") or their prefab name ("Mayor").
            public string name;
            //line 1, the basic call before any clue is put to them
            public string basic;
            //line 2, the brush off for a clue that means nothing to them
            public string nonsense;
            //line 3, the moment the one true clue lands
            public string correct;
            //line 4, them explaining themselves, each entry shown as its own paragraph
            public string[] explanation;
            //the clue that breaks them: a notebook clue key, or another suspect's specialClueId
            public string correctClueKey;
            //what breaking them unlocks: the id the others' correctClueKey can point at, and its text
            public string specialClueId;
            public string specialClue;
            //true for the murderer, whose confession ends the case rather than opening another call.
            //Unlike the lines above there is no telling a false here from a field nobody wrote, so this
            //one can only ever add to the scene: it marks a suspect, it cannot unmark one.
            public bool endsCase;
        }

        private static Document document;
        private static bool triedLoad;

        //Read once and kept. A missing or broken file is not fatal - the phone simply falls back to the
        //lines authored on the suspects in the scene.
        private static Document Loaded
        {
            get
            {
                if (triedLoad)
                {
                    return document;
                }
                triedLoad = true;
                TextAsset asset = Resources.Load<TextAsset>(ResourceName);
                if (asset == null)
                {
                    return null;
                }
                try
                {
                    document = JsonUtility.FromJson<Document>(asset.text);
                }
                catch (System.Exception error)
                {
                    Debug.LogError("Resources/" + ResourceName + ".json could not be read: " + error.Message);
                }
                return document;
            }
        }

        //re-read from disk on the next access, for editing the script with the game running
        public static void Reload()
        {
            triedLoad = false;
            document = null;
        }

        //The entry for a suspect, matched by their display name or prefab name, or null when the
        //document says nothing about them.
        private static Entry Find(SuspectProfile suspect)
        {
            Document loaded = Loaded;
            if (loaded == null || loaded.suspects == null || suspect == null)
            {
                return null;
            }
            foreach (Entry entry in loaded.suspects)
            {
                if (entry == null || string.IsNullOrEmpty(entry.name))
                {
                    continue;
                }
                if (Matches(entry.name, suspect.displayName) || Matches(entry.name, suspect.prefabName))
                {
                    return entry;
                }
            }
            return null;
        }

        private static bool Matches(string a, string b)
        {
            return !string.IsNullOrEmpty(b) && string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);
        }

        //The suspect's lines with the document folded over the scene: every field the document has an
        //answer for wins, and every field it leaves blank falls back to the scene.
        public static Lines Resolve(SuspectProfile suspect)
        {
            Lines lines = new Lines();
            if (suspect != null)
            {
                lines.basic = suspect.basicLine;
                lines.nonsense = suspect.nonsenseLine;
                lines.correct = suspect.correctLine;
                lines.explanation = suspect.explanation;
                lines.correctClueKey = suspect.correctClueKey;
                lines.specialClueId = suspect.specialClueId;
                lines.specialClue = suspect.specialClue;
                lines.endsCase = suspect.endsCase;
            }
            Entry entry = Find(suspect);
            if (entry != null)
            {
                if (!string.IsNullOrEmpty(entry.basic)) lines.basic = entry.basic;
                if (!string.IsNullOrEmpty(entry.nonsense)) lines.nonsense = entry.nonsense;
                if (!string.IsNullOrEmpty(entry.correct)) lines.correct = entry.correct;
                if (entry.explanation != null && entry.explanation.Length > 0) lines.explanation = entry.explanation;
                if (!string.IsNullOrEmpty(entry.correctClueKey)) lines.correctClueKey = entry.correctClueKey;
                if (!string.IsNullOrEmpty(entry.specialClueId)) lines.specialClueId = entry.specialClueId;
                if (!string.IsNullOrEmpty(entry.specialClue)) lines.specialClue = entry.specialClue;
                //a bool has no blank, so the document can only say yes here - see the field's own note
                if (entry.endsCase) lines.endsCase = true;
            }
            return lines;
        }
    }
}
