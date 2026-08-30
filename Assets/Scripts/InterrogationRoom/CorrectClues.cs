using UnityEngine;

namespace InterrogationRoom
{
    //The solution to the case: every clue that is actually true, and which memory it can be found in.
    //Authored by hand in Assets/Resources/CorrectClues.json rather than in the scene, because it is a
    //list of answers that wants to be read and edited as a list, not clicked through in an inspector.
    //
    //The game only ever reads it. What the player has written down lives in CaseFile; this says which
    //of those were right, and what is still out there to be found.
    public static class CorrectClues
    {
        //loaded from Resources by this name, so nothing has to be wired up for it to be found
        public const string ResourceName = "CorrectClues";

        //The shapes below are what JsonUtility reads, so the field names are the JSON keys. Renaming one
        //silently empties that part of the file rather than failing, so they are left alone.
        [System.Serializable]
        public class Document
        {
            public MemoryClues[] memories;
        }

        [System.Serializable]
        public class MemoryClues
        {
            //the same index GameDirectorMemories.StartMemory takes, so "memory 1" means one thing
            public int memoryIndex;
            public string title;
            public Entry[] clues;
        }

        [System.Serializable]
        public class Entry
        {
            //the clue key: template id and picks joined by "|", exactly as the notebook builds them.
            //The director's "List Clue Keys" context menu prints every key the game can produce.
            public string key;
            //for whoever is writing the file, never shown to the player
            public string note;
        }

        private static Document document;
        private static bool triedLoad;

        //Read once and kept. A missing or broken file is not fatal - the case simply has no answers yet,
        //which is exactly the state the game is in before anyone has written them.
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
                    Debug.LogWarning("No Resources/" + ResourceName + ".json, so no clue is known to be correct.");
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

        public static MemoryClues[] Memories
        {
            get
            {
                Document loaded = Loaded;
                return loaded != null && loaded.memories != null ? loaded.memories : new MemoryClues[0];
            }
        }

        //re-read from disk on the next access, for editing the answers with the game running
        public static void Reload()
        {
            triedLoad = false;
            document = null;
        }

        //Whether this exact accusation is one of the true ones. What makes a clue undeletable, so the
        //player cannot throw away a piece of the solution they have already found.
        public static bool IsCorrect(string clueKey)
        {
            if (string.IsNullOrEmpty(clueKey))
            {
                return false;
            }
            foreach (MemoryClues memory in Memories)
            {
                if (memory == null || memory.clues == null)
                {
                    continue;
                }
                foreach (Entry entry in memory.clues)
                {
                    if (entry != null && entry.key == clueKey)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        //how many of this memory's clues the player has not written down yet
        public static int CountMissing(int memoryIndex)
        {
            int missing = 0;
            foreach (MemoryClues memory in Memories)
            {
                if (memory == null || memory.memoryIndex != memoryIndex || memory.clues == null)
                {
                    continue;
                }
                foreach (Entry entry in memory.clues)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.key) && CaseFile.FindClue(entry.key) == null)
                    {
                        missing++;
                    }
                }
            }
            return missing;
        }

        //The earliest memory still holding something back, or -1 when the case is fully written up.
        //Earliest rather than emptiest on purpose: the player is being pointed at where to go next, and
        //sending them to the end of the game because more is missing there than at the start would be
        //pointing them past everything they have not done yet.
        public static int FirstMemoryMissingClues()
        {
            int earliest = -1;
            foreach (MemoryClues memory in Memories)
            {
                if (memory == null || CountMissing(memory.memoryIndex) <= 0)
                {
                    continue;
                }
                if (earliest < 0 || memory.memoryIndex < earliest)
                {
                    earliest = memory.memoryIndex;
                }
            }
            return earliest;
        }

        public static int TotalMissing()
        {
            int missing = 0;
            foreach (MemoryClues memory in Memories)
            {
                if (memory != null)
                {
                    missing += CountMissing(memory.memoryIndex);
                }
            }
            return missing;
        }

        //what this memory is called in the answers file, for saying where the player should look
        public static string TitleOf(int memoryIndex)
        {
            foreach (MemoryClues memory in Memories)
            {
                if (memory != null && memory.memoryIndex == memoryIndex && !string.IsNullOrEmpty(memory.title))
                {
                    return memory.title;
                }
            }
            return "memory " + (memoryIndex + 1);
        }

        //One line saying where to go next, ready to put on screen.
        public static string DescribeMissing()
        {
            int memoryIndex = FirstMemoryMissingClues();
            if (memoryIndex < 0)
            {
                return "Every clue in the case has been written down.";
            }
            int missing = CountMissing(memoryIndex);
            return missing == 1
                ? "There is still a clue to find in " + TitleOf(memoryIndex) + "."
                : "There are still " + missing + " clues to find in " + TitleOf(memoryIndex) + ".";
        }
    }
}
